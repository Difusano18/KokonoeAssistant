using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// Context Analyzer - комбінує аналіз екрану та камери
    /// Готує контекст для LLM без фільтрацій
    /// </summary>
    public class ContextAnalyzer
    {
        public class FullContextAnalysis
        {
            public DateTime Timestamp { get; set; } = DateTime.Now;
            public ActivityAnalyzer.ActivityState? ScreenActivity { get; set; }
            public WebcamAnalyzer.WebcamAnalysisResult? WebcamAnalysis { get; set; }
            public string? ActivityPattern { get; set; } // Last 6 frames pattern
            public string? DominantState { get; set; } // Idle/Active
            public string? SummaryForLLM { get; set; }
        }

        private readonly ActivityAnalyzer _activityAnalyzer;
        private readonly WebcamAnalyzer _webcamAnalyzer;
        private readonly MultiMediaBuffer _buffer;

        public ContextAnalyzer(ActivityAnalyzer activityAnalyzer, WebcamAnalyzer webcamAnalyzer, MultiMediaBuffer buffer)
        {
            _activityAnalyzer = activityAnalyzer;
            _webcamAnalyzer = webcamAnalyzer;
            _buffer = buffer;
        }

        public FullContextAnalysis AnalyzeCurrentFrame()
        {
            var analysis = new FullContextAnalysis
            {
                Timestamp = DateTime.Now
            };

            var latestFrame = _buffer.GetLatestFrame();
            if (latestFrame == null)
                return analysis;

            analysis.ScreenActivity = latestFrame.ScreenActivity;
            analysis.WebcamAnalysis = latestFrame.WebcamAnalysis;
            analysis.ActivityPattern = _buffer.GetActivityPattern(6);
            analysis.DominantState = _buffer.GetDominantActivityState(6);
            analysis.SummaryForLLM = GenerateLLMContext(analysis);

            return analysis;
        }

        private string GenerateLLMContext(FullContextAnalysis analysis)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[VISUAL STATE ANALYSIS]");

            // Screen Activity
            if (analysis.ScreenActivity != null)
            {
                sb.AppendLine($"Screen: {(analysis.ScreenActivity.IsActive ? "ACTIVE" : "IDLE")}");
                sb.AppendLine($"  Window: {analysis.ScreenActivity.ActiveWindowTitle ?? "Unknown"}");
                sb.AppendLine($"  Pixel Change: {analysis.ScreenActivity.PixelDifferencePercentage:F1}%");
                sb.AppendLine($"  Time Since Last Change: {analysis.ScreenActivity.TimeSinceLastChange.TotalMinutes:F0}m");
            }

            // Webcam Analysis
            if (analysis.WebcamAnalysis != null)
            {
                sb.AppendLine($"Webcam: {(analysis.WebcamAnalysis.FaceDetected ? "FACE DETECTED" : "No face")}");
                if (analysis.WebcamAnalysis.FaceDetected)
                {
                    sb.AppendLine($"  Expression: {analysis.WebcamAnalysis.ExpressionLevel}");
                    sb.AppendLine($"  Brightness: {(analysis.WebcamAnalysis.Brightness ?? 0):F2}");
                    sb.AppendLine($"  Confidence: {(analysis.WebcamAnalysis.Confidence ?? 0):F2}");
                }
                if (!string.IsNullOrEmpty(analysis.WebcamAnalysis.Details))
                    sb.AppendLine($"  Note: {analysis.WebcamAnalysis.Details}");
            }

            // Pattern
            sb.AppendLine($"Activity Pattern (last 6 frames): {analysis.ActivityPattern}");
            sb.AppendLine($"Dominant State: {analysis.DominantState}");

            // Summary
            sb.AppendLine("[OBSERVATION]");
            string observation = GenerateObservation(analysis);
            sb.AppendLine(observation);

            return sb.ToString();
        }

        private string GenerateObservation(FullContextAnalysis analysis)
        {
            var observations = new List<string>();

            // Screen observations
            if (analysis.ScreenActivity != null)
            {
                if (!analysis.ScreenActivity.IsActive && analysis.ScreenActivity.TimeSinceLastChange.TotalMinutes > 30)
                {
                    observations.Add($"Idle for {(int)analysis.ScreenActivity.TimeSinceLastChange.TotalMinutes} minutes on {analysis.ScreenActivity.ActiveWindowTitle}");
                }
                else if (analysis.ScreenActivity.IsActive)
                {
                    observations.Add($"Active on {analysis.ScreenActivity.ActiveWindowTitle}");
                }
            }

            // Webcam observations
            if (analysis.WebcamAnalysis != null && analysis.WebcamAnalysis.FaceDetected)
            {
                switch (analysis.WebcamAnalysis.ExpressionLevel)
                {
                    case "tired":
                        observations.Add("Face shows signs of fatigue");
                        break;
                    case "stressed":
                        observations.Add("Expression suggests high focus/stress");
                        break;
                    case "focused":
                        observations.Add("Face shows concentration");
                        break;
                }

                if (analysis.WebcamAnalysis.Brightness < 0.25)
                    observations.Add("Low lighting - room is dark");
            }

            // Activity pattern observations
            if (!string.IsNullOrEmpty(analysis.ActivityPattern))
            {
                if (analysis.ActivityPattern.Contains("Idle->Idle->Idle"))
                    observations.Add("Pattern: Prolonged idle state");
                else if (analysis.ActivityPattern.Contains("Active->Active->Active"))
                    observations.Add("Pattern: Consistent activity");
            }

            if (observations.Count == 0)
                return "No significant observations at this moment.";

            return string.Join(" | ", observations);
        }

        /// <summary>
        /// Готує детальний контекст з історією за останній час
        /// </summary>
        public string GenerateExtendedContext(int lastFrameCount = 12)
        {
            if (_buffer == null || _buffer.FrameCount == 0)
                return "[NO VISUAL DATA AVAILABLE]";

            var sb = new StringBuilder();
            sb.AppendLine("[EXTENDED VISUAL CONTEXT - LAST HOUR]");

            var frames = _buffer.Frames.TakeLast(lastFrameCount).ToList();

            // Timeline summary
            sb.AppendLine("Timeline:");
            foreach (var frame in frames)
            {
                var activity = frame.ScreenActivity?.IsActive == true ? "🔴 Active" : "⚫ Idle";
                var face = frame.WebcamAnalysis?.FaceDetected == true ? "👤" : "  ";
                sb.AppendLine($"  {frame.Timestamp:HH:mm} {activity} {face}");
            }

            // Statistics
            sb.AppendLine("\nStatistics:");
            var activeCount = frames.Count(f => f.ScreenActivity?.IsActive == true);
            var faceCount = frames.Count(f => f.WebcamAnalysis?.FaceDetected == true);

            sb.AppendLine($"  Active: {activeCount}/{frames.Count}");
            sb.AppendLine($"  Face visible: {faceCount}/{frames.Count}");

            var avgBrightness = _buffer.GetAverageBrightness(lastFrameCount);
            sb.AppendLine($"  Average brightness: {avgBrightness:F2}");

            // Extract most common window/expression
            var windowTitles = frames
                .Where(f => f.ScreenActivity?.ActiveWindowTitle != null)
                .GroupBy(f => f.ScreenActivity!.ActiveWindowTitle)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (windowTitles != null)
                sb.AppendLine($"  Most common window: {windowTitles.Key} ({windowTitles.Count()} frames)");

            var expressions = frames
                .Where(f => f.WebcamAnalysis != null)
                .GroupBy(f => f.WebcamAnalysis!.ExpressionLevel)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (expressions != null)
                sb.AppendLine($"  Most common expression: {expressions.Key} ({expressions.Count()} frames)");

            return sb.ToString();
        }
    }
}
