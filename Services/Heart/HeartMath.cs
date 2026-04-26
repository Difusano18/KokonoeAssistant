using System;

namespace KokonoeAssistant.Services.Heart
{
    public static class HeartMath
    {
        public const double MinBpm = 45;
        public const double MaxBpm = 135;
        public const double SmoothingFactor = 0.08;

        public static double Circadian(DateTime localNow)
        {
            double hour = localNow.TimeOfDay.TotalHours;
            return -6.0 * Math.Cos((hour - 4.0) * Math.PI / 12.0);
        }

        public static double Respiratory(DateTime utcNow)
        {
            double t = utcNow.Ticks / 1e7;
            return 3.0 * Math.Sin(t * 2.0 * Math.PI / 4.0);
        }

        public static double ComputeTarget(
            double baseline,
            double padA, double intensity,
            double acuteStress, double chronicStress, double fatigue,
            DateTime localNow)
        {
            double target = baseline
                + Math.Max(0, padA) * 35.0
                + intensity * 12.0
                + acuteStress * 25.0
                + chronicStress * 10.0
                - fatigue * 6.0
                + Circadian(localNow);
            return Math.Clamp(target, MinBpm, MaxBpm);
        }

        public static double Smooth(double current, double target)
            => current + (target - current) * SmoothingFactor;

        public static double NextBeatIntervalMs(double bpm, double acuteStress, Random rng)
        {
            double baseInterval = 60_000.0 / bpm;
            double hrvAmplitude = 1.0 - acuteStress * 0.7;
            double jitter = 1.0 + (rng.NextDouble() * 0.16 - 0.08) * hrvAmplitude;
            return baseInterval * jitter;
        }

        public static double AdaptBaseline(double baseline, double currentBpm, double acuteStress)
        {
            if (acuteStress >= 0.1) return baseline;
            return baseline + (currentBpm - baseline) * 0.0001;
        }
    }
}
