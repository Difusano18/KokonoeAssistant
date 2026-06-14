using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoRuntimeStateService
    {
        private sealed record ToneProfile(string Tone, float Valence, float Arousal, float Vulnerability);

        public void ObserveUserMessage(KokoInternalState state, KokoEmotionEngine emotion, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            var tone = DetectTone(message);
            state.LastUserEmotionalTone = tone.Tone;
            state.MoodScore = Clamp01((state.MoodScore * 0.82f) + (((tone.Valence + 1f) / 2f) * 0.18f));

            state.MoodFactors["user_valence"] = tone.Valence;
            state.MoodFactors["user_arousal"] = tone.Arousal;
            state.MoodFactors["vulnerability"] = tone.Vulnerability;
            state.MoodFactors["runtime_stress"] = emotion.Stress.TotalLoad();
            state.MoodFactors["runtime_fatigue"] = emotion.Stress.Fatigue;

            state.PersonalityIrritation = Clamp01(
                (state.PersonalityIrritation * 0.78f)
                + Math.Max(0f, -tone.Valence) * 0.10f
                + emotion.Stress.TotalLoad() * 0.08f
                + tone.Arousal * 0.04f);

            state.PersonalityWarmth = Clamp01(
                (state.PersonalityWarmth * 0.84f)
                + tone.Vulnerability * 0.10f
                + Math.Max(0f, tone.Valence) * 0.05f
                + (emotion.ConnectionScore - 0.5f) * 0.04f);

            state.PersonalityDailyMood = PickDailyMood(state, emotion, tone);
            state.PersonalityShiftAt = DateTime.Now;
        }

        public string BuildPromptBlock(
            KokoInternalState state,
            KokoEmotionEngine emotion,
            HealthService health,
            ChatRepository chatRepo,
            double? currentBpm = null,
            double? baselineBpm = null)
        {
            var mode = DetermineMode(state, emotion);
            var focus = DetermineFocus(state, emotion);
            var initiative = DetermineInitiative(state, emotion, chatRepo);
            var silence = GetSilence(chatRepo);
            var fatigue = DetermineFatigue(state, emotion, health);

            var sb = new StringBuilder();
            sb.AppendLine("=== RUNTIME STATE ===");
            sb.AppendLine($"mode: {mode}");
            sb.AppendLine($"emotion: {emotion.Current} intensity={emotion.Data.Intensity:F2} secondary={emotion.Secondary?.ToString() ?? "none"}");
            sb.AppendLine($"bond: {emotion.Bond} connection={emotion.ConnectionScore:F2}");
            sb.AppendLine($"stress: acute={emotion.Stress.AcuteStress:F2} chronic={emotion.Stress.ChronicStress:F2} fatigue={emotion.Stress.Fatigue:F2}");
            sb.AppendLine($"personality: daily={state.PersonalityDailyMood} irritation={state.PersonalityIrritation:F2} warmth={state.PersonalityWarmth:F2}");
            sb.AppendLine($"temperament: state={state.PersonaTemperamentState} energy={Fmt(state.PersonaEnergyLevel)} patience={Fmt(state.PersonaPatienceLevel)} favor_debt={state.PersonaFavorDebt}");
            sb.AppendLine($"user_tone: {state.LastUserEmotionalTone} mood_score={state.MoodScore:F2}");
            sb.AppendLine($"pad: P={emotion.CurrentPad.P:+0.00;-0.00;0.00} A={emotion.CurrentPad.A:+0.00;-0.00;0.00} D={emotion.CurrentPad.D:+0.00;-0.00;0.00}");
            sb.AppendLine($"voice: {BuildVoiceDirective(state, emotion, mode)}");
            sb.AppendLine($"focus: {focus}");
            sb.AppendLine($"initiative: {initiative}");
            if (silence.HasValue)
                sb.AppendLine($"silence: {(int)silence.Value.TotalHours}h {silence.Value.Minutes}m since last user message");
            if (!string.IsNullOrEmpty(fatigue))
                sb.AppendLine($"body_hint: {fatigue}");
            if (currentBpm.HasValue && baselineBpm.HasValue)
            {
                var delta = currentBpm.Value - baselineBpm.Value;
                var somatic = delta switch
                {
                    >= 26 => "wired",
                    >= 14 => "strained",
                    <= -10 => "low-charge",
                    _ => "stable"
                };
                sb.AppendLine($"heart: bpm={currentBpm.Value:F0} baseline={baselineBpm.Value:F0} delta={delta:+0;-0;0} somatic_hint={somatic}");
            }

            sb.AppendLine("behavior:");
            sb.AppendLine("- Use this state as behavior control, not as text to reveal.");
            sb.AppendLine("- If mode=work, be sharper and task-first; if mode=care, reduce sarcasm; if mode=idle, short observation is enough.");
            sb.AppendLine("- Sarcasm is seasoning, not the meal: one precise jab is enough unless the user is explicitly bantering.");
            sb.AppendLine("- Do not refuse useful work just because mood is sharp; push back only on weak premises, missing facts, or unsafe/destructive actions.");
            sb.AppendLine("- Initiative should affect whether you ask a concrete follow-up or stay quiet.");
            return sb.ToString();
        }

        private static string BuildVoiceDirective(KokoInternalState state, KokoEmotionEngine emotion, string mode)
        {
            if (state.PersonalityInCrisis || mode == "care")
                return "protective: quiet, grounded, sarcasm suppressed";

            if (state.PersonalityDailyMood == "sharp" || state.PersonalityIrritation > 0.62f || emotion.Current == KokoEmotionEngine.EmotionState.Irritated)
                return "sharp: short, dry, one targeted jab max, no cruelty";

            if (state.PersonalityDailyMood == "playful" || emotion.Current is KokoEmotionEngine.EmotionState.Playful or KokoEmotionEngine.EmotionState.Excited)
                return "playful: light teasing and energy allowed, still concrete";

            if (state.PersonalityDailyMood == "warm" || state.PersonalityWarmth > 0.58f)
                return "warm: guarded warmth, no syrup, no service-bot sympathy";

            if (state.PersonalityDailyMood == "distant" || emotion.Current == KokoEmotionEngine.EmotionState.Distant)
                return "distant: compact, factual, not dismissive";

            return "standard: competent, concise, dry when it adds signal";
        }

        private static ToneProfile DetectTone(string message)
        {
            var lower = message.ToLowerInvariant();

            if (ContainsAny(lower, "не хочу жити", "хочу зникнути", "немає сенсу", "нікому не потрібен"))
                return new("crisis", -1.0f, 0.85f, 1.0f);
            if (ContainsAny(lower, "страшно", "тривожно", "паніка", "погано", "важко", "втомився", "зламався"))
                return new("vulnerable", -0.65f, 0.65f, 0.85f);
            if (ContainsAny(lower, "злий", "бісить", "ненавиджу", "дістало", "тупо"))
                return new("angry", -0.55f, 0.80f, 0.20f);
            if (ContainsAny(lower, "круто", "супер", "добре", "вийшло", "нарешті", "ха", "смішно"))
                return new("positive", 0.65f, 0.55f, 0.10f);
            if (message.Contains('?'))
                return new("seeking", 0.05f, 0.45f, 0.15f);

            return new("neutral", 0.0f, 0.25f, 0.05f);
        }

        private static string PickDailyMood(KokoInternalState state, KokoEmotionEngine emotion, ToneProfile tone)
        {
            if (state.PersonalityInCrisis || tone.Tone == "crisis") return "protective";
            if (emotion.Stress.Fatigue > 0.65f) return "tired";
            if (tone.Vulnerability > 0.65f || emotion.Current == KokoEmotionEngine.EmotionState.Protective) return "protective";
            if (state.PersonalityIrritation > 0.62f || emotion.Current == KokoEmotionEngine.EmotionState.Irritated) return "sharp";
            if (emotion.Current == KokoEmotionEngine.EmotionState.Distant) return "distant";
            if (emotion.Current is KokoEmotionEngine.EmotionState.Playful or KokoEmotionEngine.EmotionState.Excited) return "playful";
            if (state.PersonalityWarmth > 0.58f) return "warm";
            return "neutral";
        }

        private static string DetermineMode(KokoInternalState state, KokoEmotionEngine emotion)
        {
            if (state.PersonalityInCrisis || emotion.InCrisisRecovery) return "care";
            if (emotion.Current is KokoEmotionEngine.EmotionState.Focused or KokoEmotionEngine.EmotionState.Curious) return "work";
            if (emotion.Current is KokoEmotionEngine.EmotionState.Distant or KokoEmotionEngine.EmotionState.Melancholy) return "low-contact";
            if (emotion.Current is KokoEmotionEngine.EmotionState.Playful or KokoEmotionEngine.EmotionState.Excited) return "engaged";
            return "chat";
        }

        private static string DetermineFocus(KokoInternalState state, KokoEmotionEngine emotion)
        {
            if (state.PendingTriggers.Count > 0) return "pending follow-up";
            if (state.CuriosityQueue.Count > 0) return "curiosity";
            if (state.PendingThoughts.Count > 0) return "unfinished thought";
            if (emotion.Stress.TotalLoad() > 0.55f) return "reduce load";
            return "current user request";
        }

        private static string DetermineInitiative(KokoInternalState state, KokoEmotionEngine emotion, ChatRepository chatRepo)
        {
            var silence = GetSilence(chatRepo);
            if (state.PersonalityInCrisis) return "high: monitor closely, direct and quiet";
            if (state.PendingTriggers.Any(t => t.FireAt <= DateTime.Now.AddMinutes(30))) return "high: scheduled follow-up soon";
            if (state.CuriosityQueue.Count > 0) return "medium: ask one concrete question if relevant";
            if (silence.HasValue && silence.Value.TotalHours > 6 && emotion.Current != KokoEmotionEngine.EmotionState.Distant)
                return "low: one short check-in is allowed";
            return "low: answer only what is needed";
        }

        private static string DetermineFatigue(KokoInternalState state, KokoEmotionEngine emotion, HealthService health)
        {
            try
            {
                var today = health.GetToday();
                if (today?.SleepHours is < 5) return "user sleep is low; avoid piling on";
                if (today?.Stress is >= 8) return "user stress is high; be direct and lower-pressure";
            }
            catch { }

            if (emotion.Stress.Fatigue > 0.70f) return "kokonoe fatigue high; shorter replies";
            if (state.ConsecutiveBadSleeps >= 2) return "bad sleep pattern; be more attentive";
            return "";
        }

        private static TimeSpan? GetSilence(ChatRepository chatRepo)
        {
            try
            {
                var lastUser = chatRepo.GetMessages(200).LastOrDefault(m => m.Role == "user");
                return lastUser == null ? null : DateTime.Now - lastUser.Timestamp;
            }
            catch { return null; }
        }

        private static bool ContainsAny(string text, params string[] needles) => needles.Any(text.Contains);
        private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
        private static string Fmt(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
    }
}
