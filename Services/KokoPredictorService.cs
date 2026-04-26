using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.TimeSeries;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    // ══════════════════════════════════════════════════════════════════════════
    // KOKO PREDICTOR SERVICE
    //
    // Статистичний аналіз і прогноз на базі ML.NET
    // (github.com/dotnet/machinelearning)
    //
    // Компоненти:
    //  1. Anomaly Detection — IIDSpikeDetector для mood/sleep/stress (spike + change point)
    //  2. Time-Series Forecasting — SSA (Singular Spectrum Analysis) для прогнозу настрою
    //  3. Pattern Correlation — знаходить кореляцію між sleep quality і mood
    //  4. Trend Analysis — moving average + trend direction
    //
    // Живиться з HealthService SQLite + KokoPatternEngine samples.
    // Виводить прогноз для BuildContext() в KokoBrainEngine.
    // ══════════════════════════════════════════════════════════════════════════

    public class KokoPredictorService
    {
        // ── ML.NET data schemas ───────────────────────────────────────────

        public class TimeSeriesPoint
        {
            [LoadColumn(0)] public float Value { get; set; }
        }

        public class SpikePrediction
        {
            [VectorType(3)]
            public double[] Prediction { get; set; } = Array.Empty<double>();
        }

        public class ForecastOutput
        {
            public float[] ForecastedValues    { get; set; } = Array.Empty<float>();
            public float[] ConfidenceLowerBound { get; set; } = Array.Empty<float>();
            public float[] ConfidenceUpperBound { get; set; } = Array.Empty<float>();
        }

        // ── Результати аналізу ────────────────────────────────────────────

        public class AnomalyResult
        {
            public string  MetricName  { get; set; } = "";
            public DateTime When       { get; set; }
            public float   Value       { get; set; }
            public float   Score       { get; set; }    // 0..1 — наскільки аномальний
            public bool    IsSpike     { get; set; }    // різкий підйом/падіння
            public bool    IsChangePoint { get; set; }  // зміна базового рівня
            public string  Description { get; set; } = "";
        }

        public class MoodForecast
        {
            public DateTime ForDate    { get; set; }
            public float    Predicted  { get; set; }    // 0..1 (0=bad, 1=good)
            public float    LowerBound { get; set; }
            public float    UpperBound { get; set; }
            public string   Label      { get; set; } = ""; // "хороший" / "поганий" / "невизначено"
            public float    Confidence { get; set; } = 0.5f;
        }

        public class CorrelationResult
        {
            public string  MetricA    { get; set; } = "";
            public string  MetricB    { get; set; } = "";
            public float   Pearson    { get; set; } = 0f;  // -1..+1
            public string  Strength   { get; set; } = "";  // "strong" / "moderate" / "weak"
            public string  Direction  { get; set; } = "";  // "positive" / "negative" / "none"
            public string  Insight    { get; set; } = "";  // людський текст
        }

        public class PredictorReport
        {
            public List<AnomalyResult>     Anomalies        { get; set; } = new();
            public List<MoodForecast>      MoodForecast     { get; set; } = new();
            public List<CorrelationResult> Correlations     { get; set; } = new();
            public string                  TrendSummary     { get; set; } = "";
            public string                  BestDayPredicted { get; set; } = "";
            public DateTime                GeneratedAt      { get; set; } = DateTime.Now;
        }

        // ── State ─────────────────────────────────────────────────────────

        private readonly MLContext     _mlContext;
        private readonly HealthService _health;
        private readonly string        _cachePath;
        private PredictorReport?       _lastReport;
        private DateTime               _lastReportAt = DateTime.MinValue;
        private readonly object        _lock = new();

        public KokoPredictorService(HealthService health, string dataDir)
        {
            _mlContext  = new MLContext(seed: 42);
            _health     = health;
            _cachePath  = Path.Combine(dataDir, "koko-predictor.json");
            _lastReport = TryLoadCache();
        }

        // ══════════════════════════════════════════════════════════════════
        // MAIN ANALYSIS PIPELINE
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Повний аналіз: аномалії + прогноз + кореляції.
        /// Кешується на 4 години щоб не запускати ML кожен раз.
        /// </summary>
        public PredictorReport Analyze(bool forceRefresh = false)
        {
            lock (_lock)
            {
                if (!forceRefresh && _lastReport != null &&
                    (DateTime.Now - _lastReportAt).TotalHours < 4)
                    return _lastReport;

                var report = new PredictorReport();

                try
                {
                    var history = LoadHealthHistory(30);
                    if (history.Count < 7)
                    {
                        report.TrendSummary = "Недостатньо даних для прогнозу (потрібно ≥7 днів)";
                        return _lastReport = report;
                    }

                    // 1. Аномалії
                    report.Anomalies.AddRange(DetectAnomalies(history, "mood",   h => h.MoodScore));
                    report.Anomalies.AddRange(DetectAnomalies(history, "sleep",  h => h.SleepHours));
                    report.Anomalies.AddRange(DetectAnomalies(history, "stress", h => h.StressLevel));
                    report.Anomalies.AddRange(DetectAnomalies(history, "energy", h => h.EnergyLevel));

                    // 2. Прогноз настрою на 3 дні
                    report.MoodForecast = ForecastMood(history, daysAhead: 3);

                    // 3. Кореляції
                    report.Correlations.AddRange(AnalyzeCorrelations(history));

                    // 4. Trend summary
                    report.TrendSummary     = BuildTrendSummary(history, report);
                    report.BestDayPredicted = FindBestDayPrediction(report);

                    _lastReportAt = DateTime.Now;
                    _lastReport   = report;
                    SaveCache(report);
                }
                catch (Exception ex)
                {
                    Log($"Analyze error: {ex.Message}");
                    report.TrendSummary = "Аналіз не вдався";
                }

                return report;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // ANOMALY DETECTION (ML.NET IIDSpikeDetector)
        // ══════════════════════════════════════════════════════════════════

        private List<AnomalyResult> DetectAnomalies(
            List<HealthEntry> history, string metricName, Func<HealthEntry, float> selector)
        {
            var results = new List<AnomalyResult>();
            var values  = history.Select(selector).ToList();
            if (values.Count < 5) return results;

            try
            {
                var data = _mlContext.Data.LoadFromEnumerable(
                    values.Select(v => new TimeSeriesPoint { Value = v }));

                // Spike detection (різкі відхилення)
                var spikeModel = _mlContext.Transforms
                    .DetectIidSpike("Prediction", "Value",
                        confidence: 95, pvalueHistoryLength: Math.Min(values.Count / 2, 30))
                    .Fit(data);

                var spikePredictions = spikeModel.Transform(data);
                var spikeResults     = _mlContext.Data
                    .CreateEnumerable<SpikePrediction>(spikePredictions, reuseRowObject: false)
                    .ToList();

                for (int i = 0; i < spikeResults.Count && i < history.Count; i++)
                {
                    var pred = spikeResults[i].Prediction;
                    if (pred.Length < 3) continue;
                    if (pred[0] == 1) // alert flag
                    {
                        results.Add(new AnomalyResult
                        {
                            MetricName  = metricName,
                            When        = history[i].Date,
                            Value       = values[i],
                            Score       = (float)Math.Abs(pred[1]),
                            IsSpike     = true,
                            Description = BuildAnomalyDescription(metricName, values[i], pred[1]),
                        });
                    }
                }

                // Change point detection
                if (values.Count >= 10)
                {
                    var cpModel = _mlContext.Transforms
                        .DetectIidChangePoint("Prediction", "Value",
                            confidence: 95, changeHistoryLength: Math.Min(values.Count / 3, 20))
                        .Fit(data);

                    var cpPredictions = cpModel.Transform(data);
                    var cpResults     = _mlContext.Data
                        .CreateEnumerable<SpikePrediction>(cpPredictions, reuseRowObject: false)
                        .ToList();

                    for (int i = 0; i < cpResults.Count && i < history.Count; i++)
                    {
                        var pred = cpResults[i].Prediction;
                        if (pred.Length < 3 || pred[0] != 1) continue;
                        // Уникнути дублювання
                        if (results.Any(r => r.When == history[i].Date && r.MetricName == metricName)) continue;
                        results.Add(new AnomalyResult
                        {
                            MetricName   = metricName,
                            When         = history[i].Date,
                            Value        = values[i],
                            Score        = (float)Math.Abs(pred[1]),
                            IsChangePoint= true,
                            Description  = $"{metricName}: зміна базового рівня {history[i].Date:dd.MM}",
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Anomaly detection ({metricName}): {ex.Message}");
            }

            return results;
        }

        private static string BuildAnomalyDescription(string metric, float value, double score)
        {
            var direction = score > 0 ? "різкий підйом" : "різке падіння";
            return metric switch
            {
                "mood"   => $"Настрій: {direction} (значення {value:F1}/10)",
                "sleep"  => $"Сон: {direction} ({value:F1}г)",
                "stress" => $"Стрес: {direction} ({value:F1}/10)",
                "energy" => $"Енергія: {direction} ({value:F1}/10)",
                _        => $"{metric}: {direction} ({value:F2})",
            };
        }

        // ══════════════════════════════════════════════════════════════════
        // MOOD FORECASTING (SSA — Singular Spectrum Analysis)
        // ══════════════════════════════════════════════════════════════════

        private List<MoodForecast> ForecastMood(List<HealthEntry> history, int daysAhead = 3)
        {
            var forecasts = new List<MoodForecast>();
            var values    = history.Select(h => h.MoodScore).ToList();
            if (values.Count < 10) return forecasts;

            try
            {
                var data = _mlContext.Data.LoadFromEnumerable(
                    values.Select(v => new TimeSeriesPoint { Value = v }));

                var forecastModel = _mlContext.Forecasting
                    .ForecastBySsa("ForecastedValues", "Value",
                        windowSize: Math.Min(values.Count / 3, 10),
                        seriesLength: values.Count,
                        trainSize: values.Count,
                        horizon: daysAhead,
                        confidenceLowerBoundColumn: "ConfidenceLowerBound",
                        confidenceUpperBoundColumn: "ConfidenceUpperBound")
                    .Fit(data);

                var engine    = forecastModel.CreateTimeSeriesEngine<TimeSeriesPoint, ForecastOutput>(_mlContext);
                var predicted = engine.Predict();

                for (int i = 0; i < Math.Min(daysAhead, predicted.ForecastedValues.Length); i++)
                {
                    float val   = Math.Clamp(predicted.ForecastedValues[i], 0f, 10f);
                    float lower = predicted.ConfidenceLowerBound.Length > i
                        ? Math.Clamp(predicted.ConfidenceLowerBound[i], 0f, 10f)
                        : val * 0.8f;
                    float upper = predicted.ConfidenceUpperBound.Length > i
                        ? Math.Clamp(predicted.ConfidenceUpperBound[i], 0f, 10f)
                        : val * 1.2f;

                    string label = val >= 7f ? "хороший" : val >= 5f ? "середній" : "поганий";
                    float confidence = 1f - Math.Clamp((upper - lower) / 10f, 0f, 0.9f);

                    forecasts.Add(new MoodForecast
                    {
                        ForDate    = DateTime.Today.AddDays(i + 1),
                        Predicted  = val,
                        LowerBound = lower,
                        UpperBound = upper,
                        Label      = label,
                        Confidence = confidence,
                    });
                }
            }
            catch (Exception ex)
            {
                Log($"Forecast error: {ex.Message}");
            }

            return forecasts;
        }

        // ══════════════════════════════════════════════════════════════════
        // CORRELATION ANALYSIS (Pearson)
        // ══════════════════════════════════════════════════════════════════

        private List<CorrelationResult> AnalyzeCorrelations(List<HealthEntry> history)
        {
            var results = new List<CorrelationResult>();
            if (history.Count < 7) return results;

            var pairs = new[]
            {
                ("sleep",  "mood",   "Сон → Настрій"),
                ("sleep",  "energy", "Сон → Енергія"),
                ("stress", "mood",   "Стрес → Настрій"),
                ("stress", "sleep",  "Стрес → Сон"),
                ("energy", "mood",   "Енергія → Настрій"),
            };

            foreach (var (a, b, label) in pairs)
            {
                var vecA = GetMetric(history, a);
                var vecB = GetMetric(history, b);
                float r  = PearsonCorrelation(vecA, vecB);

                string strength  = Math.Abs(r) >= 0.6f ? "сильна" : Math.Abs(r) >= 0.3f ? "помірна" : "слабка";
                string direction = r > 0.15f ? "позитивна" : r < -0.15f ? "негативна" : "відсутня";
                string insight   = BuildCorrelationInsight(a, b, r);

                if (Math.Abs(r) >= 0.3f) // Повідомляємо лише значущі кореляції
                {
                    results.Add(new CorrelationResult
                    {
                        MetricA   = a,
                        MetricB   = b,
                        Pearson   = r,
                        Strength  = strength,
                        Direction = direction,
                        Insight   = insight,
                    });
                }
            }

            return results;
        }

        private static string BuildCorrelationInsight(string a, string b, float r)
        {
            if (Math.Abs(r) < 0.3f) return "";
            string dir = r > 0 ? "покращує" : "погіршує";
            return (a, b) switch
            {
                ("sleep",  "mood")   => r > 0.5f  ? $"Коли він добре спить — настрій помітно кращий (r={r:F2})" : $"Сон {dir} настрій (r={r:F2})",
                ("sleep",  "energy") => r > 0.5f  ? $"Сон сильно впливає на його енергію (r={r:F2})" : $"Сон {dir} енергію",
                ("stress", "mood")   => r < -0.5f ? $"Стрес помітно знижує настрій (r={r:F2})" : $"Стрес {dir} настрій",
                ("stress", "sleep")  => r < -0.3f ? $"Коли стресує — гірше спить (r={r:F2})" : "",
                ("energy", "mood")   => r > 0.4f  ? $"Висока енергія = кращий настрій (r={r:F2})" : "",
                _ => $"{a}→{b}: r={r:F2}",
            };
        }

        // ══════════════════════════════════════════════════════════════════
        // TREND SUMMARY
        // ══════════════════════════════════════════════════════════════════

        private static string BuildTrendSummary(List<HealthEntry> history, PredictorReport report)
        {
            if (history.Count < 7) return "";

            var sb      = new StringBuilder();
            var recent7 = history.TakeLast(7).ToList();
            var prev7   = history.Count >= 14 ? history.Take(history.Count - 7).TakeLast(7).ToList() : null;

            float avgMoodRecent = recent7.Average(h => h.MoodScore);
            float avgMoodPrev   = prev7?.Count > 0 ? prev7.Average(h => h.MoodScore) : avgMoodRecent;
            float moodDelta     = avgMoodRecent - avgMoodPrev;

            if (Math.Abs(moodDelta) > 0.5f)
                sb.AppendLine(moodDelta > 0
                    ? $"Настрій за тиждень покращився (+{moodDelta:F1})"
                    : $"Настрій за тиждень погіршився ({moodDelta:F1})");

            // Anomaly count
            if (report.Anomalies.Count > 0)
            {
                var recentAnom = report.Anomalies.Where(a => a.When >= DateTime.Now.AddDays(-7)).ToList();
                if (recentAnom.Count > 0)
                    sb.AppendLine($"Аномалій за тиждень: {recentAnom.Count} ({string.Join(", ", recentAnom.Select(a => a.MetricName).Distinct())})");
            }

            // Strongest correlation insight
            var strongCorr = report.Correlations.OrderByDescending(c => Math.Abs(c.Pearson)).FirstOrDefault();
            if (strongCorr != null && !string.IsNullOrEmpty(strongCorr.Insight))
                sb.AppendLine(strongCorr.Insight);

            return sb.ToString().Trim();
        }

        private static string FindBestDayPrediction(PredictorReport report)
        {
            if (!report.MoodForecast.Any()) return "";
            var best = report.MoodForecast.OrderByDescending(f => f.Predicted).First();
            if (best.Label == "поганий") return "";
            return $"Найкращий день для зустрічі: {best.ForDate:dddd dd.MM} (прогноз: {best.Label}, {best.Predicted:F1}/10)";
        }

        // ══════════════════════════════════════════════════════════════════
        // CONTEXT GENERATION FOR LLM
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Стислий рядок прогнозу для BuildContext() в KokoBrainEngine</summary>
        public string GetForecastContext()
        {
            var report = Analyze();
            if (string.IsNullOrEmpty(report.TrendSummary) && !report.MoodForecast.Any())
                return "";

            var sb = new StringBuilder("\n--- ПРОГНОЗ (ML.NET) ---\n");

            if (!string.IsNullOrEmpty(report.TrendSummary))
                sb.AppendLine(report.TrendSummary);

            if (report.MoodForecast.Any())
            {
                var tomorrow = report.MoodForecast.FirstOrDefault();
                if (tomorrow != null)
                    sb.AppendLine($"Прогноз на завтра: настрій {tomorrow.Label} ({tomorrow.Predicted:F1}/10, впевненість {tomorrow.Confidence:P0})");
            }

            if (!string.IsNullOrEmpty(report.BestDayPredicted))
                sb.AppendLine(report.BestDayPredicted);

            var criticalAnom = report.Anomalies
                .Where(a => a.Score > 0.7f && a.When >= DateTime.Now.AddDays(-3))
                .Take(2)
                .ToList();
            foreach (var a in criticalAnom)
                sb.AppendLine($"⚠ Аномалія: {a.Description}");

            return sb.ToString().Trim();
        }

        // ══════════════════════════════════════════════════════════════════
        // DATA LOADING
        // ══════════════════════════════════════════════════════════════════

        private class HealthEntry
        {
            public DateTime Date        { get; set; }
            public float    MoodScore   { get; set; }
            public float    SleepHours  { get; set; }
            public float    EnergyLevel { get; set; }
            public float    StressLevel { get; set; }
        }

        private List<HealthEntry> LoadHealthHistory(int days)
        {
            var entries = new List<HealthEntry>();
            try
            {
                var metrics = _health.GetRecent(days);
                foreach (var m in metrics.OrderBy(m => m.Date))
                {
                    entries.Add(new HealthEntry
                    {
                        Date        = m.Date,
                        MoodScore   = (float)(m.Mood    ?? 5),
                        SleepHours  = (float)(m.SleepHours ?? 7.0),
                        EnergyLevel = (float)(m.Energy  ?? 5),
                        StressLevel = (float)(m.Stress  ?? 3),
                    });
                }
            }
            catch (Exception ex)
            {
                Log($"LoadHealthHistory: {ex.Message}");
            }
            return entries;
        }

        private static float[] GetMetric(List<HealthEntry> history, string name) => name switch
        {
            "mood"   => history.Select(h => h.MoodScore).ToArray(),
            "sleep"  => history.Select(h => h.SleepHours).ToArray(),
            "energy" => history.Select(h => h.EnergyLevel).ToArray(),
            "stress" => history.Select(h => h.StressLevel).ToArray(),
            _        => Array.Empty<float>(),
        };

        // ══════════════════════════════════════════════════════════════════
        // STATISTICS UTILS
        // ══════════════════════════════════════════════════════════════════

        private static float PearsonCorrelation(float[] x, float[] y)
        {
            if (x.Length != y.Length || x.Length < 3) return 0f;
            float meanX = x.Average(), meanY = y.Average();
            float num = 0, denX = 0, denY = 0;
            for (int i = 0; i < x.Length; i++)
            {
                float dx = x[i] - meanX, dy = y[i] - meanY;
                num  += dx * dy;
                denX += dx * dx;
                denY += dy * dy;
            }
            float den = MathF.Sqrt(denX * denY);
            return den < 1e-8f ? 0f : Math.Clamp(num / den, -1f, 1f);
        }

        // ══════════════════════════════════════════════════════════════════
        // PERSISTENCE
        // ══════════════════════════════════════════════════════════════════

        private PredictorReport? TryLoadCache()
        {
            try
            {
                if (File.Exists(_cachePath))
                    return JsonConvert.DeserializeObject<PredictorReport>(File.ReadAllText(_cachePath));
            }
            catch { }
            return null;
        }

        private void SaveCache(PredictorReport report)
        {
            try { File.WriteAllText(_cachePath, JsonConvert.SerializeObject(report, Formatting.Indented)); }
            catch { }
        }

        private static void Log(string msg) =>
            System.Diagnostics.Debug.WriteLine($"[KokoPredictorService] {msg}");
    }
}
