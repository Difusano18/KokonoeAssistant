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

        public event Action<double>? Beat;
        public event Action<double>? BpmChanged;

        public KokoHeartEngine(KokoEmotionEngine emotion, string dataDir)
        {
            _emotion = emotion;
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
}
