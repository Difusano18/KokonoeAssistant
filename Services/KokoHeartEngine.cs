using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KokonoeAssistant.Services.Heart;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public sealed class KokoHeartEngine : IDisposable
    {
        private readonly KokoEmotionEngine _emotion;
        private readonly KokoWearableTelemetryService? _wearable;
        private readonly string _statePath;
        private readonly Random _rng = new();

        private HeartState _state = new();
        private double _currentBpm = 62.0;
        private double _lastReportedBpm;
        private int _inBeat;
        private bool _started;

        private PeriodicTimer? _stateTimer;
        private System.Threading.Timer? _beatTimer;
        private System.Threading.Timer? _saveTimer;
        private CancellationTokenSource? _cts;

        public double CurrentBpm => _currentBpm;
        public double BaselineBpm => _state.BaselineBpm;
        public long TotalBeats => _state.TotalBeats;
        public double BpmDelta => _currentBpm - _state.BaselineBpm;
        public KokoWearableStressFrame WearableStress => EvaluateWearableStress();

        public event Action<double>? Beat;
        public event Action<double>? BpmChanged;

        public KokoHeartEngine(KokoEmotionEngine emotion, string dataDir, KokoWearableTelemetryService? wearable = null)
        {
            _emotion = emotion;
            _wearable = wearable;
            Directory.CreateDirectory(dataDir);
            _statePath = Path.Combine(dataDir, "koko-heart.json");
        }

        public void Start()
        {
            if (_started) return;
            LoadState();
            _currentBpm = _state.LastBpm;
            _lastReportedBpm = _currentBpm;

            _cts = new CancellationTokenSource();
            _stateTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            _ = RunStateLoopAsync(_cts.Token);

            _beatTimer = new System.Threading.Timer(OnBeatTick, null,
                (int)(60_000.0 / Math.Max(_currentBpm, 30.0)), Timeout.Infinite);

            _saveTimer = new System.Threading.Timer(_ => _ = SaveAsync(), null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            _started = true;
            Debug.WriteLine($"[Heart] started, baseline={_state.BaselineBpm:0.0} last={_state.LastBpm:0.0}");
        }

        public void Stop()
        {
            if (!_started) return;
            try { _cts?.Cancel(); } catch { }
            _stateTimer?.Dispose(); _stateTimer = null;
            _beatTimer?.Dispose();  _beatTimer = null;
            _saveTimer?.Dispose();  _saveTimer = null;
            _started = false;
        }

        public void Dispose()
        {
            Stop();
            try { SaveAsync().GetAwaiter().GetResult(); }
            catch (Exception ex) { Debug.WriteLine($"[Heart] final save failed: {ex}"); }
        }

        private async Task RunStateLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _stateTimer != null
                       && await _stateTimer.WaitForNextTickAsync(ct))
                {
                    try { OnStateTick(); }
                    catch (Exception ex) { Debug.WriteLine($"[Heart] state tick failed: {ex}"); }
                }
            }
            catch (OperationCanceledException) { }
        }

        private void OnStateTick()
        {
            if (TryApplyWearableHeartRate())
                return;

            double padA         = _emotion.Data.PadA;
            double intensity    = _emotion.Data.Intensity;
            double acuteStress  = _emotion.Stress.AcuteStress;
            double chronicStress = _emotion.Stress.ChronicStress;
            double fatigue      = _emotion.Stress.Fatigue;

            var target = HeartMath.ComputeTarget(
                _state.BaselineBpm, padA, intensity,
                acuteStress, chronicStress, fatigue,
                DateTime.Now);

            _currentBpm = HeartMath.Smooth(_currentBpm, target);
            _state.BaselineBpm = HeartMath.AdaptBaseline(_state.BaselineBpm, _currentBpm, acuteStress);

            var displayBpm = _currentBpm + HeartMath.Respiratory(DateTime.UtcNow);
            if (Math.Abs(displayBpm - _lastReportedBpm) > 0.5)
            {
                _lastReportedBpm = displayBpm;
                BpmChanged?.Invoke(displayBpm);
            }
        }

        private bool TryApplyWearableHeartRate()
        {
            if (_wearable == null) return false;

            var wearable = _wearable.State;
            if (!wearable.IsFresh(DateTime.UtcNow) || wearable.CurrentBpm <= 0)
                return false;

            _currentBpm += (wearable.CurrentBpm - _currentBpm) * 0.35;
            if (wearable.BaselineBpm > 0)
                _state.BaselineBpm += (wearable.BaselineBpm - _state.BaselineBpm) * 0.08;

            var displayBpm = _currentBpm + HeartMath.Respiratory(DateTime.UtcNow) * 0.35;
            if (Math.Abs(displayBpm - _lastReportedBpm) > 0.5)
            {
                _lastReportedBpm = displayBpm;
                BpmChanged?.Invoke(displayBpm);
            }

            return true;
        }

        public KokoWearableStressFrame EvaluateWearableStress(DateTime? nowUtc = null)
        {
            if (_wearable == null) return new KokoWearableStressFrame();

            var now = nowUtc ?? DateTime.UtcNow;
            var state = _wearable.State;
            var recent = _wearable.RecentSamples
                .Where(s => now - s.TimestampUtc.ToUniversalTime() <= TimeSpan.FromMinutes(20))
                .OrderBy(s => s.TimestampUtc)
                .ToList();

            if (!state.IsFresh(now) || recent.Count == 0)
                return new KokoWearableStressFrame { State = "stale", PromptHint = "ignore wearable stress until fresh samples return" };

            var bpmValues = recent.Where(s => s.HeartRateBpm.HasValue).Select(s => s.HeartRateBpm!.Value).ToList();
            var hrvValues = recent.Where(s => s.HrvRmssdMs.HasValue).Select(s => s.HrvRmssdMs!.Value).ToList();
            var motion = recent.Where(s => s.Motion.HasValue).Select(s => s.Motion!.Value).DefaultIfEmpty(state.Motion ?? 0).Average();
            var avgBpm = bpmValues.DefaultIfEmpty(state.CurrentBpm).Average();
            var firstBpm = bpmValues.FirstOrDefault(avgBpm);
            var lastBpm = bpmValues.LastOrDefault(avgBpm);
            var trend = lastBpm - firstBpm;
            var baseline = state.BaselineBpm > 0 ? state.BaselineBpm : _state.BaselineBpm;
            var hrv = hrvValues.DefaultIfEmpty(state.HrvRmssdMs ?? 0).Average();

            var score = state.StressScore * 0.45;
            if (baseline > 0 && avgBpm >= baseline + 12) score += 0.22;
            if (trend >= 8) score += 0.12;
            if (hrv > 0 && hrv < 24) score += 0.16;
            if (motion <= 0.10 && state.OnWrist) score += 0.05;
            if (state.SleepState is "probably_asleep" or "drowsy_or_resting") score -= 0.20;
            score = Math.Clamp(score, 0, 1);

            var frame = new KokoWearableStressFrame
            {
                Score = score,
                AverageBpm = avgBpm,
                BpmTrend = trend,
                HrvRmssdMs = hrv > 0 ? hrv : null,
                Motion = motion,
                State = score >= 0.72 ? "high_stress" :
                    score >= 0.52 ? "strained" :
                    state.SleepState is "probably_asleep" or "drowsy_or_resting" ? "resting" :
                    "stable"
            };
            frame.PromptHint = frame.State switch
            {
                "high_stress" => "suggest a short concrete break only if context allows; reduce teasing and long explanations",
                "strained" => "keep replies shorter, more concrete, and avoid unnecessary pressure",
                "resting" => "lower initiative; avoid waking or pestering",
                _ => "normal tone; no health commentary unless relevant"
            };
            frame.Reason = $"avg_bpm={avgBpm:F0}; trend={trend:+0;-0;0}; hrv={(frame.HrvRmssdMs.HasValue ? frame.HrvRmssdMs.Value.ToString("F0") : "-")}; motion={motion:F2}; telemetry_stress={state.StressScore:F2}";
            return frame;
        }

        private void OnBeatTick(object? _unused)
        {
            if (Interlocked.CompareExchange(ref _inBeat, 1, 0) != 0) return;
            try
            {
                _state.TotalBeats++;
                Beat?.Invoke(_currentBpm);

                var acute = _emotion.Stress.AcuteStress;
                var interval = HeartMath.NextBeatIntervalMs(_currentBpm, acute, _rng);
                interval = Math.Clamp(interval, 250, 2000);
                _beatTimer?.Change((int)interval, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Heart] beat tick failed: {ex}");
            }
            finally { Interlocked.Exchange(ref _inBeat, 0); }
        }

        private void LoadState()
        {
            try
            {
                if (!File.Exists(_statePath)) { _state = new HeartState(); return; }
                var json = File.ReadAllText(_statePath);
                var loaded = JsonConvert.DeserializeObject<HeartState>(json);
                _state = loaded ?? new HeartState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Heart] load failed: {ex}");
                _state = new HeartState();
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                _state.LastBpm = _currentBpm;
                _state.LastSavedUtc = DateTime.UtcNow;
                var json = JsonConvert.SerializeObject(_state, Formatting.Indented);
                var tmp = _statePath + ".tmp";
                await File.WriteAllTextAsync(tmp, json);
                if (File.Exists(_statePath))
                    File.Replace(tmp, _statePath, null);
                else
                    File.Move(tmp, _statePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Heart] save failed: {ex}");
            }
        }
    }

    public sealed class KokoWearableStressFrame
    {
        public string State { get; set; } = "unknown";
        public double Score { get; set; }
        public double AverageBpm { get; set; }
        public double BpmTrend { get; set; }
        public double? HrvRmssdMs { get; set; }
        public double Motion { get; set; }
        public string Reason { get; set; } = "";
        public string PromptHint { get; set; } = "";
    }
}
