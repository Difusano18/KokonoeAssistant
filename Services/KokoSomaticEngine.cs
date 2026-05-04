using System;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoSomaticSnapshot
    {
        public string State { get; set; } = "unknown";
        public string Label { get; set; } = "no somatic signal";
        public string BehaviorHint { get; set; } = "";
        public double Bpm { get; set; }
        public double BaselineBpm { get; set; }
        public double BpmDelta { get; set; }
        public double Strain { get; set; }
        public double Calm { get; set; }
        public double Volatility { get; set; }
        public bool IsElevated => BpmDelta >= 14 || Strain >= 0.65;
        public bool IsVeryElevated => BpmDelta >= 26 || Strain >= 0.82;
        public bool IsLow => Bpm > 0 && BpmDelta <= -10;
    }

    public sealed class KokoSomaticEngine
    {
        public KokoSomaticSnapshot Evaluate(
            KokoHeartEngine? heart,
            KokoEmotionEngine emotion,
            HealthService health,
            DateTime now)
        {
            var bpm = heart?.CurrentBpm ?? 0;
            var baseline = heart?.BaselineBpm ?? 0;
            var delta = bpm > 0 && baseline > 0 ? bpm - baseline : 0;

            var stress = emotion.Stress.TotalLoad();
            var fatigue = emotion.Stress.Fatigue;
            var arousal = Math.Max(0, emotion.Data.PadA);

            var sleepPenalty = 0d;
            var reportedStress = 0d;
            try
            {
                var today = health.GetToday();
                if (today?.SleepHours is > 0 and < 5) sleepPenalty = 0.18;
                if (today?.Stress is >= 7) reportedStress = 0.12;
            }
            catch { }

            var circadianTired = now.Hour is >= 0 and < 6 ? 0.16 : now.Hour >= 23 ? 0.10 : 0;
            var bpmPressure = bpm <= 0 ? 0 : Math.Clamp((delta + 6) / 42.0, 0, 1);
            var strain = Math.Clamp(
                bpmPressure * 0.42 +
                stress * 0.25 +
                arousal * 0.18 +
                reportedStress +
                sleepPenalty +
                circadianTired,
                0,
                1);

            var calm = Math.Clamp(1.0 - strain - Math.Max(0, fatigue - 0.45) * 0.20, 0, 1);
            var volatility = Math.Clamp(Math.Abs(delta) / 35.0 + emotion.Data.Intensity * 0.25, 0, 1);

            var snapshot = new KokoSomaticSnapshot
            {
                Bpm = bpm,
                BaselineBpm = baseline,
                BpmDelta = delta,
                Strain = strain,
                Calm = calm,
                Volatility = volatility
            };

            ApplyState(snapshot, fatigue, now);
            return snapshot;
        }

        public string BuildPromptBlock(KokoSomaticSnapshot snapshot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== SOMATIC STATE ===");
            sb.AppendLine($"state={snapshot.State} label={snapshot.Label}");
            if (snapshot.Bpm > 0)
                sb.AppendLine($"heart=bpm:{snapshot.Bpm:F0} baseline:{snapshot.BaselineBpm:F0} delta:{snapshot.BpmDelta:+0;-0;0}");
            sb.AppendLine($"strain={snapshot.Strain:F2} calm={snapshot.Calm:F2} volatility={snapshot.Volatility:F2}");
            if (!string.IsNullOrWhiteSpace(snapshot.BehaviorHint))
                sb.AppendLine($"behavior_hint={snapshot.BehaviorHint}");
            sb.AppendLine("rule: somatic state is not a medical diagnosis. Treat it as Kokonoe's body/context signal.");
            return sb.ToString();
        }

        private static void ApplyState(KokoSomaticSnapshot snapshot, double fatigue, DateTime now)
        {
            if (snapshot.Bpm <= 0)
            {
                snapshot.State = "unknown";
                snapshot.Label = "no heart signal";
                snapshot.BehaviorHint = "do not infer body state";
                return;
            }

            if (snapshot.IsVeryElevated)
            {
                snapshot.State = "wired";
                snapshot.Label = "high arousal";
                snapshot.BehaviorHint = "short, concrete, more protective; reduce teasing unless user is playful";
                return;
            }

            if (snapshot.IsElevated)
            {
                snapshot.State = "strained";
                snapshot.Label = "elevated strain";
                snapshot.BehaviorHint = "be sharper but useful; avoid long lectures";
                return;
            }

            if (snapshot.IsLow || fatigue > 0.68 || now.Hour is >= 0 and < 6)
            {
                snapshot.State = "tired";
                snapshot.Label = "low charge";
                snapshot.BehaviorHint = "quieter, fewer words, lower initiative";
                return;
            }

            if (snapshot.Calm >= 0.72)
            {
                snapshot.State = "calm";
                snapshot.Label = "stable calm";
                snapshot.BehaviorHint = "normal sarcasm allowed; do not over-check";
                return;
            }

            snapshot.State = "focused";
            snapshot.Label = "active baseline";
            snapshot.BehaviorHint = "task-first, observant, concise";
        }
    }
}
