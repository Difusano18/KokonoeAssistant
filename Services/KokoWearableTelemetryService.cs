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
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string DeviceId { get; set; } = "unknown";
        public string Source { get; set; } = "wearable";
        public double? HeartRateBpm { get; set; }
        public double? IbiMs { get; set; }
        public double? HrvRmssdMs { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? LocationAccuracyM { get; set; }
        public double? Motion { get; set; }
        public bool? OnWrist { get; set; }
        public string Activity { get; set; } = "";
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
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Motion { get; set; }
        public bool OnWrist { get; set; }
        public string Activity { get; set; } = "";
        public string PresenceState { get; set; } = "unknown";
        public string SleepState { get; set; } = "unknown";
        public double SleepConfidence { get; set; }
        public string Summary { get; set; } = "wearable offline";
        public bool IsFresh(DateTime nowUtc) => LastSampleUtc > DateTime.MinValue && nowUtc - LastSampleUtc <= TimeSpan.FromMinutes(3);
    }

    public sealed class KokoWearableTelemetryService
    {
        private readonly object _lock = new();
        private readonly string _dataDir;
        private readonly string _samplesPath;
        private readonly string _statePath;
        private readonly Queue<KokoWearableSample> _recent = new();
        private KokoWearableState _state = new();

        public KokoWearableTelemetryService(string dataDir)
        {
            _dataDir = dataDir;
            Directory.CreateDirectory(_dataDir);
            _samplesPath = Path.Combine(_dataDir, "wearable-telemetry.jsonl");
            _statePath = Path.Combine(_dataDir, "wearable-state.json");
            LoadState();
        }

        public KokoWearableState State
        {
            get { lock (_lock) return CloneState(_state); }
        }

        public IReadOnlyList<KokoWearableSample> RecentSamples
        {
            get { lock (_lock) return _recent.ToArray(); }
        }

        public KokoWearableState Ingest(KokoWearableSample sample)
        {
            sample ??= new KokoWearableSample();
            if (sample.TimestampUtc.Kind == DateTimeKind.Local)
                sample.TimestampUtc = sample.TimestampUtc.ToUniversalTime();
            if (sample.TimestampUtc.Kind == DateTimeKind.Unspecified)
                sample.TimestampUtc = DateTime.SpecifyKind(sample.TimestampUtc, DateTimeKind.Utc);
            sample.DeviceId = string.IsNullOrWhiteSpace(sample.DeviceId) ? "unknown" : sample.DeviceId.Trim();
            sample.Source = string.IsNullOrWhiteSpace(sample.Source) ? "wearable" : sample.Source.Trim();
            if (sample.HeartRateBpm is <= 25 or >= 230) sample.HeartRateBpm = null;

            lock (_lock)
            {
                _recent.Enqueue(sample);
                while (_recent.Count > 720)
                    _recent.Dequeue();

                ApplySample(sample);
                AppendSample(sample);
                SaveState();
                return CloneState(_state);
            }
        }

        public string BuildPromptBlock(DateTime? nowUtc = null)
        {
            var now = nowUtc ?? DateTime.UtcNow;
            KokoWearableState state;
            lock (_lock) state = CloneState(_state);

            var freshness = state.IsFresh(now) ? "fresh" : "stale";
            return $"""
WEARABLE TELEMETRY
freshness={freshness}
device={NullDash(state.DeviceId)} on_wrist={state.OnWrist}
heart={state.CurrentBpm:F0} bpm baseline={state.BaselineBpm:F0} delta={state.BpmDelta:+0;-0;0}
sleep={state.SleepState} confidence={state.SleepConfidence:F2}
presence={state.PresenceState} activity={NullDash(state.Activity)}
location={(state.Latitude.HasValue && state.Longitude.HasValue ? $"{state.Latitude.Value.ToString("F5", CultureInfo.InvariantCulture)},{state.Longitude.Value.ToString("F5", CultureInfo.InvariantCulture)}" : "-")}
summary={state.Summary}
rule: wearable telemetry is context, not a medical diagnosis. Use it to reduce dumb follow-ups and detect likely sleep/return states.
""";
        }

        private void ApplySample(KokoWearableSample sample)
        {
            var nowUtc = sample.TimestampUtc;
            _state.LastSampleUtc = nowUtc;
            _state.DeviceId = sample.DeviceId;
            _state.OnWrist = sample.OnWrist ?? _state.OnWrist;
            _state.Activity = string.IsNullOrWhiteSpace(sample.Activity) ? _state.Activity : sample.Activity.Trim();
            _state.Motion = sample.Motion ?? _state.Motion;
            _state.HrvRmssdMs = sample.HrvRmssdMs ?? _state.HrvRmssdMs;

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
            _state.PresenceState = _state.OnWrist ? "wearing_watch" : "off_wrist_or_unknown";
            _state.Summary = $"{_state.SleepState}; bpm {avgBpm:F0}; motion {avgMotion:F2}; wrist {onWristRatio:P0}";
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

        private static KokoWearableState CloneState(KokoWearableState state)
            => JsonConvert.DeserializeObject<KokoWearableState>(JsonConvert.SerializeObject(state)) ?? new();

        private static string NullDash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }
}
