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
        public double WearableStressScore { get; set; }
        public string WearableStressState { get; set; } = "";
        public string WearableStressHint { get; set; } = "";
        public bool IsElevated => BpmDelta >= 14 || Strain >= 0.65;
        public bool IsVeryElevated => BpmDelta >= 26 || Strain >= 0.82;
        public bool IsLow => Bpm > 0 && BpmDelta <= -10;
    }

    public sealed class KokoSomaticInput
    {
        public double Bpm { get; set; }
        public double BaselineBpm { get; set; }
        public double Stress { get; set; }
        public double Fatigue { get; set; }
        public double Arousal { get; set; }
        public double SleepHours { get; set; }
        public int? ReportedStress { get; set; }
        public double WearableStressScore { get; set; }
        public string WearableStressState { get; set; } = "";
        public string WearableStressHint { get; set; } = "";
        public DateTime Now { get; set; } = DateTime.Now;
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
            var wearableStress = heart?.WearableStress ?? new KokoWearableStressFrame();

            double sleepHours = 0;
            int? reportedStress = null;
            try
            {
                var today = health.GetToday();
                sleepHours = today?.SleepHours ?? 0;
                reportedStress = today?.Stress;
            }
            catch (Exception suppressedEx60) { KokoSystemLog.Write("SOMATICENGINE-CATCH", "Evaluate failed near source line 60: " + suppressedEx60); }

            return Evaluate(new KokoSomaticInput
            {
                Bpm = bpm,
                BaselineBpm = baseline,
                Stress = emotion.Stress.TotalLoad(),
                Fatigue = emotion.Stress.Fatigue,
                Arousal = Math.Max(0, emotion.Data.PadA),
                SleepHours = sleepHours,
                ReportedStress = reportedStress,
                WearableStressScore = wearableStress.Score,
                WearableStressState = wearableStress.State,
                WearableStressHint = wearableStress.PromptHint,
                Now = now
            });
        }

        public KokoSomaticSnapshot Evaluate(KokoSomaticInput input)
        {
            var bpm = input.Bpm;
            var baseline = input.BaselineBpm;
            var delta = bpm > 0 && baseline > 0 ? bpm - baseline : 0;

            var sleepPenalty = input.SleepHours is > 0 and < 5 ? 0.18 : 0d;
            var reportedStress = input.ReportedStress is >= 7 ? 0.12 : 0d;
            var circadianTired = input.Now.Hour is >= 0 and < 6 ? 0.16 : input.Now.Hour >= 23 ? 0.10 : 0;
            var bpmPressure = bpm <= 0 ? 0 : Math.Clamp((delta + 6) / 42.0, 0, 1);
            var strain = Math.Clamp(
                bpmPressure * 0.42 +
                input.Stress * 0.25 +
                input.Arousal * 0.18 +
                input.WearableStressScore * 0.20 +
                reportedStress +
                sleepPenalty +
                circadianTired,
                0,
                1);

            var calm = Math.Clamp(1.0 - strain - Math.Max(0, input.Fatigue - 0.45) * 0.20, 0, 1);
            var volatility = Math.Clamp(Math.Abs(delta) / 35.0 + input.Arousal * 0.20, 0, 1);

            var snapshot = new KokoSomaticSnapshot
            {
                Bpm = bpm,
                BaselineBpm = baseline,
                BpmDelta = delta,
                Strain = strain,
                Calm = calm,
                Volatility = volatility,
                WearableStressScore = input.WearableStressScore,
                WearableStressState = input.WearableStressState,
                WearableStressHint = input.WearableStressHint
            };

            ApplyState(snapshot, input.Fatigue, input.Now);
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
            if (!string.IsNullOrWhiteSpace(snapshot.WearableStressState))
                sb.AppendLine($"wearable_stress={snapshot.WearableStressState} score={snapshot.WearableStressScore:F2}");
            if (!string.IsNullOrWhiteSpace(snapshot.WearableStressHint))
                sb.AppendLine($"wearable_hint={snapshot.WearableStressHint}");
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
