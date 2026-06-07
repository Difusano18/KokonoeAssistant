using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWearableSample
    {
        public string SampleId { get; set; } = "";
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string DeviceId { get; set; } = "unknown";
        public string Source { get; set; } = "wearable";
        public double? HeartRateBpm { get; set; }
        public double? IbiMs { get; set; }
        public double? HrvRmssdMs { get; set; }
        public double? SpO2Percent { get; set; }
        public double? SkinTemperatureC { get; set; }
        public double? PpgSignalQuality { get; set; }
        public bool? EcgAvailable { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? LocationAccuracyM { get; set; }
        public string SemanticLocation { get; set; } = "";
        public double? Motion { get; set; }
        public bool? OnWrist { get; set; }
        public string Activity { get; set; } = "";
        public double? BatteryPercent { get; set; }
        public bool? Charging { get; set; }
        public string Note { get; set; } = "";
    }

    public sealed class KokoWearableState
    {
        public DateTime LastSampleUtc { get; set; } = DateTime.MinValue;
        public string DeviceId { get; set; } = "";
        public double CurrentBpm { get; set; }
        public double BaselineBpm { get; set; }
        public double BpmDelta => CurrentBpm > 0 && BaselineBpm > 0 ? CurrentBpm - BaselineBpm : 0;
        public double? HrvRmssdMs { get; set; }
        public double? SpO2Percent { get; set; }
        public double? SkinTemperatureC { get; set; }
        public double? PpgSignalQuality { get; set; }
        public bool? EcgAvailable { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string SemanticLocation { get; set; } = "";
        public double? Motion { get; set; }
        public bool OnWrist { get; set; }
        public string Activity { get; set; } = "";
        public double? BatteryPercent { get; set; }
        public bool? Charging { get; set; }
        public string PresenceState { get; set; } = "unknown";
        public string SleepState { get; set; } = "unknown";
        public double SleepConfidence { get; set; }
        public double StressScore { get; set; }
        public int LiveStressScore { get; set; }
        public string RecoveryState { get; set; } = "unknown";
        public string SuggestedInitiative { get; set; } = "";
        public string ContextSignal { get; set; } = "";
        public string ContextHint { get; set; } = "";
        public string Summary { get; set; } = "wearable offline";
        public bool IsFresh(DateTime nowUtc) => LastSampleUtc > DateTime.MinValue && nowUtc - LastSampleUtc <= TimeSpan.FromMinutes(3);
    }

    public sealed class KokoWearableIngestResult
    {
        public KokoWearableState State { get; set; } = new();
        public bool Accepted { get; set; }
        public bool Duplicate { get; set; }
        public string EventKind { get; set; } = "";
        public string EventReason { get; set; } = "";
    }

    public sealed class KokoWearableTelemetryService
    {
        private readonly object _lock = new();
        private readonly object _logLock = new();
        private readonly string _dataDir;
        private readonly string _samplesPath;
        private readonly string _statePath;
        private readonly string _logPath;
        private readonly Queue<KokoWearableSample> _recent = new();
        private readonly Queue<string> _recentSampleIds = new();
        private readonly HashSet<string> _recentSampleIdSet = new(StringComparer.OrdinalIgnoreCase);
        private KokoWearableState _state = new();

        public event Action<KokoWearableIngestResult, KokoWearableSample>? SampleAccepted;

        public KokoWearableTelemetryService(string dataDir)
        {
            _dataDir = dataDir;
            Directory.CreateDirectory(_dataDir);
            _samplesPath = Path.Combine(_dataDir, "wearable-telemetry.jsonl");
            _statePath = Path.Combine(_dataDir, "wearable-state.json");
            var logDir = Path.Combine(_dataDir, "logs");
            Directory.CreateDirectory(logDir);
            _logPath = Path.Combine(logDir, "telemetry.log");
            LoadState();
            LoadRecentSampleIds();
            WriteLog("[WEARABLE] telemetry service started");
        }

        public KokoWearableState State
        {
            get { lock (_lock) return CloneState(_state); }
        }

        public IReadOnlyList<KokoWearableSample> RecentSamples
        {
            get { lock (_lock) return _recent.ToArray(); }
        }

        public string LogPath => _logPath;

        public IReadOnlyList<string> RecentLogLines(int maxLines = 80)
        {
            try
            {
                if (!File.Exists(_logPath)) return Array.Empty<string>();
                return File.ReadLines(_logPath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .TakeLast(Math.Clamp(maxLines, 1, 500))
                    .ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wearable] log read failed: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        public void WriteLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            try
            {
                lock (_logLock)
                {
                    File.AppendAllText(_logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message.Trim()}{Environment.NewLine}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wearable] log write failed: {ex.Message}");
            }
            KokoSystemLog.Write("WEARABLE", message);
        }

        public KokoWearableState Ingest(KokoWearableSample sample)
            => IngestDetailed(sample).State;

        public KokoWearableIngestResult IngestDetailed(KokoWearableSample sample)
        {
            sample ??= new KokoWearableSample();
            if (sample.TimestampUtc.Kind == DateTimeKind.Local)
                sample.TimestampUtc = sample.TimestampUtc.ToUniversalTime();
            if (sample.TimestampUtc.Kind == DateTimeKind.Unspecified)
                sample.TimestampUtc = DateTime.SpecifyKind(sample.TimestampUtc, DateTimeKind.Utc);
            sample.SampleId = (sample.SampleId ?? "").Trim();
            sample.DeviceId = string.IsNullOrWhiteSpace(sample.DeviceId) ? "unknown" : sample.DeviceId.Trim();
            sample.Source = string.IsNullOrWhiteSpace(sample.Source) ? "wearable" : sample.Source.Trim();
            if (sample.HeartRateBpm == 0)
                WriteLog($"[WEARABLE] Sensor returned 0: Device={sample.DeviceId}");
            if (sample.HeartRateBpm is <= 25 or >= 230) sample.HeartRateBpm = null;
            if (sample.SpO2Percent is <= 50 or > 100) sample.SpO2Percent = null;
            if (sample.SkinTemperatureC is < 25 or > 45) sample.SkinTemperatureC = null;
            if (sample.PpgSignalQuality is < 0 or > 1) sample.PpgSignalQuality = null;
            if (sample.BatteryPercent is < 0 or > 100) sample.BatteryPercent = null;

            KokoWearableIngestResult result;
            lock (_lock)
            {
                if (IsDuplicateLocked(sample.SampleId))
                {
                    return new KokoWearableIngestResult
                    {
                        State = CloneState(_state),
                        Accepted = false,
                        Duplicate = true
                    };
                }

                RememberSampleIdLocked(sample.SampleId);
                _recent.Enqueue(sample);
                while (_recent.Count > 720)
                    _recent.Dequeue();

                var previous = CloneState(_state);
                ApplySample(sample);
                var eventKind = ClassifySomaticEvent(previous, _state, sample);
                var eventReason = BuildSomaticEventReason(eventKind, previous, _state, sample);
                AppendSample(sample);
                SaveState();
                WriteLog($"[WEARABLE] Sample Accepted: BPM={FormatNullable(sample.HeartRateBpm)}, Motion={FormatNullable(sample.Motion)}, Device={sample.DeviceId}");
                if (!string.IsNullOrWhiteSpace(eventKind))
                    WriteLog($"[WEARABLE] Somatic Event: {eventKind} {eventReason}");
                result = new KokoWearableIngestResult
                {
                    State = CloneState(_state),
                    Accepted = true,
                    Duplicate = false,
                    EventKind = eventKind,
                    EventReason = eventReason
                };
            }
            try { SampleAccepted?.Invoke(result, sample); } catch (Exception ex) { WriteLog($"[WEARABLE] sample event handler failed: {ex.Message}"); }
            return result;
        }

        public string BuildPromptBlock(DateTime? nowUtc = null)
        {
            var now = nowUtc ?? DateTime.UtcNow;
            KokoWearableState state;
            lock (_lock) state = CloneState(_state);

            var verifiedFresh = state.IsFresh(now) && !LooksLikeDiagnosticTelemetry(state.DeviceId);
            var freshness = verifiedFresh ? "fresh" : "stale";
            var heartLine = verifiedFresh && state.CurrentBpm > 0
                ? $"heart={state.CurrentBpm:F0} bpm baseline={state.BaselineBpm:F0} delta={state.BpmDelta:+0;-0;0}"
                : "heart=unavailable baseline=unavailable delta=unavailable";
            return $"""
WEARABLE TELEMETRY
freshness={freshness}
device={NullDash(state.DeviceId)} on_wrist={state.OnWrist}
{heartLine}
sleep={state.SleepState} confidence={state.SleepConfidence:F2}
stress={state.StressScore:F2} live_stress_score={state.LiveStressScore}/100 recovery={state.RecoveryState} initiative={NullDash(state.SuggestedInitiative)}
context_signal={NullDash(state.ContextSignal)} context_hint={NullDash(state.ContextHint)}
presence={state.PresenceState} activity={NullDash(state.Activity)}
location={(state.Latitude.HasValue && state.Longitude.HasValue ? $"{state.Latitude.Value.ToString("F5", CultureInfo.InvariantCulture)},{state.Longitude.Value.ToString("F5", CultureInfo.InvariantCulture)}" : "-")} semantic_location={NullDash(state.SemanticLocation)}
hrv={(state.HrvRmssdMs.HasValue ? $"{state.HrvRmssdMs.Value:F0}ms" : "-")} spo2={(state.SpO2Percent.HasValue ? $"{state.SpO2Percent.Value:F0}%" : "-")} skin_temp={(state.SkinTemperatureC.HasValue ? $"{state.SkinTemperatureC.Value:F1}C" : "-")} ppg_quality={(state.PpgSignalQuality.HasValue ? state.PpgSignalQuality.Value.ToString("F2", CultureInfo.InvariantCulture) : "-")} battery={(state.BatteryPercent.HasValue ? $"{state.BatteryPercent.Value:F0}%" : "-")}
summary={state.Summary}
rule: wearable telemetry is context, not a medical diagnosis. Use it to reduce dumb follow-ups, detect likely sleep/return/stress states, and propose low-risk breaks only when useful.
""";
        }

        private static bool LooksLikeDiagnosticTelemetry(string? value)
        {
            var text = (value ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(text)) return false;
            return text.Contains("smoke") ||
                   text.Contains("mock") ||
                   text.Contains("dummy") ||
                   text.Contains("emulated") ||
                   text.Contains("simulator") ||
                   text.Contains("test-watch");
        }

        private void ApplySample(KokoWearableSample sample)
        {
            var nowUtc = sample.TimestampUtc;
            _state.LastSampleUtc = nowUtc;
            _state.DeviceId = sample.DeviceId;
            _state.OnWrist = sample.OnWrist ?? _state.OnWrist;
            _state.Activity = string.IsNullOrWhiteSpace(sample.Activity) ? _state.Activity : sample.Activity.Trim();
            _state.SemanticLocation = string.IsNullOrWhiteSpace(sample.SemanticLocation) ? _state.SemanticLocation : sample.SemanticLocation.Trim();
            _state.Motion = sample.Motion ?? _state.Motion;
            _state.HrvRmssdMs = sample.HrvRmssdMs ?? _state.HrvRmssdMs;
            _state.SpO2Percent = sample.SpO2Percent ?? _state.SpO2Percent;
            _state.SkinTemperatureC = sample.SkinTemperatureC ?? _state.SkinTemperatureC;
            _state.PpgSignalQuality = sample.PpgSignalQuality ?? _state.PpgSignalQuality;
            _state.EcgAvailable = sample.EcgAvailable ?? _state.EcgAvailable;
            _state.BatteryPercent = sample.BatteryPercent ?? _state.BatteryPercent;
            _state.Charging = sample.Charging ?? _state.Charging;

            if (sample.Latitude.HasValue && sample.Longitude.HasValue)
            {
                _state.Latitude = sample.Latitude;
                _state.Longitude = sample.Longitude;
            }

            if (sample.HeartRateBpm.HasValue)
            {
                _state.CurrentBpm = sample.HeartRateBpm.Value;
                _state.BaselineBpm = _state.BaselineBpm <= 0
                    ? sample.HeartRateBpm.Value
                    : _state.BaselineBpm * 0.985 + sample.HeartRateBpm.Value * 0.015;
            }

            InferState(nowUtc);
        }

        private void InferState(DateTime nowUtc)
        {
            var local = nowUtc.ToLocalTime();
            var recent = _recent.Where(s => nowUtc - s.TimestampUtc <= TimeSpan.FromMinutes(20)).ToList();
            var previousSleepState = _state.SleepState;
            var avgBpm = recent.Where(s => s.HeartRateBpm.HasValue).Select(s => s.HeartRateBpm!.Value).DefaultIfEmpty(_state.CurrentBpm).Average();
            var avgMotion = recent.Where(s => s.Motion.HasValue).Select(s => s.Motion!.Value).DefaultIfEmpty(_state.Motion ?? 0).Average();
            var onWristRatio = recent.Count == 0 ? (_state.OnWrist ? 1 : 0) : recent.Count(s => s.OnWrist == true) / (double)recent.Count;
            var night = local.Hour >= 23 || local.Hour < 8;
            var lowMotion = avgMotion <= 0.16;
            var lowBpm = _state.BaselineBpm > 0 && avgBpm <= _state.BaselineBpm - 4;

            var confidence = 0d;
            if (night) confidence += 0.28;
            if (lowMotion) confidence += 0.30;
            if (lowBpm) confidence += 0.22;
            if (onWristRatio >= 0.75) confidence += 0.14;
            if ((_state.HrvRmssdMs ?? 0) >= 35) confidence += 0.06;
            confidence = Math.Clamp(confidence, 0, 1);

            _state.SleepConfidence = confidence;
            _state.SleepState = confidence >= 0.72 ? "probably_asleep" :
                confidence >= 0.48 ? "drowsy_or_resting" :
                night && lowMotion ? "quiet_night" :
                "awake";

            var bpmDeviation = _state.BaselineBpm > 0 ? avgBpm - _state.BaselineBpm : 0;
            var bpmDeviationStress = _state.BaselineBpm > 0
                ? Math.Clamp((bpmDeviation + 4) / 32.0, 0, 1)
                : 0;
            var stress = bpmDeviationStress * 0.34;
            if (_state.BaselineBpm > 0 && avgBpm >= _state.BaselineBpm + 14) stress += 0.28;
            if ((_state.HrvRmssdMs ?? 999) < 22) stress += 0.24;
            if ((_state.SpO2Percent ?? 100) < 93) stress += 0.16;
            if (avgMotion <= 0.10 && !night && _state.OnWrist) stress += 0.10;
            if ((_state.PpgSignalQuality ?? 1) < 0.45) stress -= 0.12;
            _state.StressScore = Math.Clamp(stress, 0, 1);
            _state.LiveStressScore = (int)Math.Round(_state.StressScore * 100);
            _state.RecoveryState = _state.SleepConfidence >= 0.6 ? "resting" :
                _state.StressScore >= 0.62 ? "strained" :
                (_state.HrvRmssdMs ?? 0) >= 45 && avgBpm <= _state.BaselineBpm + 4 ? "recovered" :
                "neutral";
            _state.SuggestedInitiative = _state.SleepState == "probably_asleep" ? "stay_quiet" :
                _state.StressScore >= 0.68 ? "suggest_short_break" :
                avgMotion <= 0.08 && !night ? "suggest_movement" :
                "";
            _state.PresenceState = _state.OnWrist ? "wearing_watch" : "off_wrist_or_unknown";
            _state.ContextSignal = BuildContextSignal(previousSleepState, _state.SleepState, avgMotion, avgBpm, local);
            _state.ContextHint = _state.ContextSignal switch
            {
                "likely_just_woke" => "acknowledge wake transition lightly; avoid demanding tasks immediately",
                "post_activity" => "user may be physically activated; keep responses practical and low-friction",
                "resting" => "lower initiative and avoid unnecessary pings",
                "quiet_work" => "likely sedentary focus; concise task-first replies",
                _ => ""
            };
            _state.Summary = $"{_state.SleepState}; {_state.RecoveryState}; live stress {_state.LiveStressScore}/100; bpm {avgBpm:F0}; motion {avgMotion:F2}; wrist {onWristRatio:P0}";
        }

        private static string BuildContextSignal(string previousSleep, string currentSleep, double avgMotion, double avgBpm, DateTime local)
        {
            if (previousSleep == "probably_asleep" && currentSleep == "awake")
                return "likely_just_woke";
            if (avgMotion >= 0.55 && avgBpm >= 95)
                return "post_activity";
            if (currentSleep is "probably_asleep" or "drowsy_or_resting")
                return "resting";
            if (avgMotion <= 0.12 && local.Hour is >= 9 and <= 22)
                return "quiet_work";
            return "";
        }

        private static string ClassifySomaticEvent(KokoWearableState previous, KokoWearableState current, KokoWearableSample sample)
        {
            if (!sample.HeartRateBpm.HasValue)
                return "";

            var baseline = current.BaselineBpm > 0 ? current.BaselineBpm : previous.BaselineBpm;
            var delta = baseline > 0 ? sample.HeartRateBpm.Value - baseline : 0;
            var bpmJump = previous.CurrentBpm > 0 ? sample.HeartRateBpm.Value - previous.CurrentBpm : 0;

            if (current.SleepState is "probably_asleep" or "drowsy_or_resting")
                return "";
            if (current.StressScore >= 0.70 || delta >= 18 || bpmJump >= 16)
                return "stress_spike";
            if (current.RecoveryState == "strained" && current.StressScore >= 0.62)
                return "high_strain";
            if (delta <= -10 && (current.Motion ?? 0) <= 0.12)
                return "fatigue_drop";
            return "";
        }

        private static string BuildSomaticEventReason(string eventKind, KokoWearableState previous, KokoWearableState current, KokoWearableSample sample)
        {
            if (string.IsNullOrWhiteSpace(eventKind))
                return "";

            var jump = previous.CurrentBpm > 0 && sample.HeartRateBpm.HasValue
                ? sample.HeartRateBpm.Value - previous.CurrentBpm
                : 0;
            return $"bpm={current.CurrentBpm:F0}; baseline={current.BaselineBpm:F0}; delta={current.BpmDelta:+0;-0;0}; jump={jump:+0;-0;0}; stress={current.LiveStressScore}/100; recovery={current.RecoveryState}; motion={(current.Motion.HasValue ? current.Motion.Value.ToString("F2", CultureInfo.InvariantCulture) : "-")}";
        }

        private void LoadState()
        {
            try
            {
                if (!File.Exists(_statePath)) return;
                _state = JsonConvert.DeserializeObject<KokoWearableState>(File.ReadAllText(_statePath)) ?? new();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wearable] load failed: {ex.Message}");
                _state = new KokoWearableState();
            }
        }

        private void LoadRecentSampleIds()
        {
            try
            {
                if (!File.Exists(_samplesPath)) return;
                foreach (var line in File.ReadLines(_samplesPath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var sample = JsonConvert.DeserializeObject<KokoWearableSample>(line);
                    if (!string.IsNullOrWhiteSpace(sample?.SampleId))
                        RememberSampleIdLocked(sample.SampleId.Trim());
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wearable] sample id cache load failed: {ex.Message}");
            }
        }

        private void SaveState()
        {
            try { File.WriteAllText(_statePath, JsonConvert.SerializeObject(_state, Formatting.Indented)); }
            catch (Exception ex) { Debug.WriteLine($"[Wearable] save failed: {ex.Message}"); }
        }

        private void AppendSample(KokoWearableSample sample)
        {
            try { File.AppendAllText(_samplesPath, JsonConvert.SerializeObject(sample, Formatting.None) + Environment.NewLine); }
            catch (Exception ex) { Debug.WriteLine($"[Wearable] append failed: {ex.Message}"); }
        }

        private bool IsDuplicateLocked(string sampleId)
            => !string.IsNullOrWhiteSpace(sampleId) && _recentSampleIdSet.Contains(sampleId);

        private void RememberSampleIdLocked(string sampleId)
        {
            if (string.IsNullOrWhiteSpace(sampleId)) return;
            _recentSampleIds.Enqueue(sampleId);
            _recentSampleIdSet.Add(sampleId);
            while (_recentSampleIds.Count > 1440)
            {
                var removed = _recentSampleIds.Dequeue();
                _recentSampleIdSet.Remove(removed);
            }
        }

        private static KokoWearableState CloneState(KokoWearableState state)
            => JsonConvert.DeserializeObject<KokoWearableState>(JsonConvert.SerializeObject(state)) ?? new();

        private static string NullDash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

        private static string FormatNullable(double? value)
            => value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : "--";
    }
}
