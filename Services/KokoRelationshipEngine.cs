using System;
using System.IO;
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
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

        public float BondScore =>
            Clamp01(Trust * 0.26f + Intimacy * 0.24f + Stability * 0.18f +
                    Protectiveness * 0.12f + Curiosity * 0.10f - Friction * 0.20f);

        private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
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
                NormalizeAndSave();
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
rule: this is persistent relationship state. Let it influence tone subtly; do not recite numbers.
""";
            }
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
    }
}
