using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public sealed class KokoRelationshipState
    {
        public float Trust { get; set; } = 0.45f;
        public float Intimacy { get; set; } = 0.32f;
        public float Friction { get; set; } = 0.12f;
        public float Protectiveness { get; set; } = 0.22f;
        public float Curiosity { get; set; } = 0.50f;
        public float Stability { get; set; } = 0.55f;
        public string LastAftertaste { get; set; } = "neutral";
        public string LastShiftReason { get; set; } = "";
        public List<KokoRelationshipEvent> RecentEvents { get; set; } = new();
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

        public float BondScore =>
            Clamp01(Trust * 0.26f + Intimacy * 0.24f + Stability * 0.18f +
                    Protectiveness * 0.12f + Curiosity * 0.10f - Friction * 0.20f);

        private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
    }

    public sealed class KokoRelationshipEvent
    {
        public DateTime When { get; set; } = DateTime.Now;
        public string Kind { get; set; } = "";
        public string Reason { get; set; } = "";
        public string Aftertaste { get; set; } = "";
        public float Trust { get; set; }
        public float Intimacy { get; set; }
        public float Friction { get; set; }
        public float Protectiveness { get; set; }
    }

    public sealed class KokoConversationReflection
    {
        public string Learned { get; set; } = "";
        public string Reflection { get; set; } = "";
        public string Remember { get; set; } = "";
        public string UserTone { get; set; } = "neutral";
        public string Aftertaste { get; set; } = "neutral";
        public string FollowUpQuestion { get; set; } = "";
        public float Importance { get; set; } = 0.5f;
        public float TrustDelta { get; set; }
        public float IntimacyDelta { get; set; }
        public float FrictionDelta { get; set; }
        public float ProtectivenessDelta { get; set; }
        public float CuriosityDelta { get; set; }
        public float StabilityDelta { get; set; }
    }

    public sealed class KokoRelationshipEngine
    {
        private readonly string _path;
        private readonly object _lock = new();
        private KokoRelationshipState _state;

        public KokoRelationshipState State
        {
            get { lock (_lock) return _state; }
        }

        public KokoRelationshipEngine(string dataDir)
        {
            Directory.CreateDirectory(dataDir);
            _path = Path.Combine(dataDir, "koko-relationship.json");
            _state = Load();
        }

        public void ObserveUserTone(string tone, bool crisis)
        {
            lock (_lock)
            {
                switch (tone)
                {
                    case "crisis":
                    case "vulnerable":
                        _state.Protectiveness += crisis ? 0.12f : 0.06f;
                        _state.Intimacy += 0.035f;
                        _state.Trust += 0.015f;
                        _state.LastAftertaste = crisis ? "alarmed" : "protective";
                        break;
                    case "angry":
                        _state.Friction += 0.05f;
                        _state.Stability -= 0.025f;
                        _state.LastAftertaste = "tense";
                        break;
                    case "positive":
                        _state.Trust += 0.025f;
                        _state.Intimacy += 0.025f;
                        _state.Friction -= 0.02f;
                        _state.LastAftertaste = "warmer";
                        break;
                    case "seeking":
                        _state.Curiosity += 0.025f;
                        _state.Trust += 0.01f;
                        _state.LastAftertaste = "engaged";
                        break;
                    default:
                        _state.Stability += 0.006f;
                        _state.LastAftertaste = "steady";
                        break;
                }

                _state.LastShiftReason = $"tone:{tone}";
                AddEvent($"tone:{tone}", crisis ? "crisis/vulnerable signal" : "observed user tone", _state.LastAftertaste);
                NormalizeAndSave();
            }
        }

        public void ApplyReflection(KokoConversationReflection reflection)
        {
            lock (_lock)
            {
                _state.Trust += ClampDelta(reflection.TrustDelta);
                _state.Intimacy += ClampDelta(reflection.IntimacyDelta);
                _state.Friction += ClampDelta(reflection.FrictionDelta);
                _state.Protectiveness += ClampDelta(reflection.ProtectivenessDelta);
                _state.Curiosity += ClampDelta(reflection.CuriosityDelta);
                _state.Stability += ClampDelta(reflection.StabilityDelta);

                if (!string.IsNullOrWhiteSpace(reflection.Aftertaste))
                    _state.LastAftertaste = reflection.Aftertaste.Trim();
                _state.LastShiftReason = "reflection";
                AddEvent("reflection", reflection.Reflection, _state.LastAftertaste);
                NormalizeAndSave();
            }
        }

        public IReadOnlyList<KokoRelationshipEvent> GetRecentEvents(int count = 8)
        {
            lock (_lock)
            {
                return _state.RecentEvents
                    .OrderByDescending(e => e.When)
                    .Take(count)
                    .ToList();
            }
        }

        public string BuildPromptBlock()
        {
            lock (_lock)
            {
                return $"""
=== RELATIONSHIP STATE ===
trust={_state.Trust:F2} intimacy={_state.Intimacy:F2} friction={_state.Friction:F2}
protectiveness={_state.Protectiveness:F2} curiosity={_state.Curiosity:F2} stability={_state.Stability:F2}
bond_score={_state.BondScore:F2} aftertaste={_state.LastAftertaste}
recent_events={string.Join(" | ", _state.RecentEvents.TakeLast(4).Select(e => $"{e.When:dd.MM HH:mm}:{e.Kind}:{e.Aftertaste}"))}
rule: this is persistent relationship state. Let it influence tone subtly; do not recite numbers.
""";
            }
        }

        private void AddEvent(string kind, string reason, string aftertaste)
        {
            _state.RecentEvents.Add(new KokoRelationshipEvent
            {
                Kind = kind,
                Reason = Trim(reason, 180),
                Aftertaste = aftertaste,
                Trust = _state.Trust,
                Intimacy = _state.Intimacy,
                Friction = _state.Friction,
                Protectiveness = _state.Protectiveness
            });

            if (_state.RecentEvents.Count > 40)
                _state.RecentEvents.RemoveRange(0, _state.RecentEvents.Count - 40);
        }

        private void NormalizeAndSave()
        {
            _state.Trust = Clamp01(_state.Trust);
            _state.Intimacy = Clamp01(_state.Intimacy);
            _state.Friction = Clamp01(_state.Friction);
            _state.Protectiveness = Clamp01(_state.Protectiveness);
            _state.Curiosity = Clamp01(_state.Curiosity);
            _state.Stability = Clamp01(_state.Stability);
            _state.LastUpdatedUtc = DateTime.UtcNow;
            Save();
        }

        private KokoRelationshipState Load()
        {
            try
            {
                if (!File.Exists(_path)) return new KokoRelationshipState();
                return JsonConvert.DeserializeObject<KokoRelationshipState>(File.ReadAllText(_path))
                    ?? new KokoRelationshipState();
            }
            catch { return new KokoRelationshipState(); }
        }

        private void Save()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_state, Formatting.Indented);
                File.WriteAllText(_path, json);
            }
            catch { }
        }

        private static float ClampDelta(float value) => Math.Clamp(value, -0.15f, 0.15f);
        private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
        private static string Trim(string? text, int max)
        {
            text ??= "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max].TrimEnd() + "...";
        }
    }
}
