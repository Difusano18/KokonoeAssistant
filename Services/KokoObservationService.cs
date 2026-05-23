using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    public sealed class KokoObservationOptions
    {
        public string Objective { get; set; } = "";
        public string TaskId { get; set; } = "";
        public int Iterations { get; set; } = 10;
        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);
        public bool MinimizeSelf { get; set; } = true;
        public int VisionMaxTokens { get; set; } = 512;
    }

    public sealed class KokoObservationProgress
    {
        public int Index { get; set; }
        public int Total { get; set; }
        public string Summary { get; set; } = "";
        public string LogPath { get; set; } = "";
    }

    public sealed class KokoObservationResult
    {
        public string Summary { get; set; } = "";
        public string LogPath { get; set; } = "";
        public List<string> Events { get; set; } = new();
    }

    public sealed class KokoObservationService
    {
        private readonly string _logDir;
        private readonly PcControlService _pc;
        private readonly LlmService? _llm;

        public KokoObservationService(string logDir, PcControlService pc, LlmService? llm = null)
        {
            _logDir = logDir;
            _pc = pc;
            _llm = llm;
            Directory.CreateDirectory(_logDir);
        }

        public static KokoObservationOptions BuildOptions(string objective, string taskId = "")
        {
            objective ??= "";
            var duration = ExtractDuration(objective) ?? TimeSpan.FromMinutes(5);
            duration = Clamp(duration, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(10));
            var interval = ExtractInterval(objective) ?? TimeSpan.FromSeconds(30);
            interval = Clamp(interval, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(2));
            var iterations = Math.Clamp((int)Math.Ceiling(duration.TotalSeconds / Math.Max(1, interval.TotalSeconds)), 1, 40);

            return new KokoObservationOptions
            {
                Objective = objective.Trim(),
                TaskId = taskId,
                Iterations = iterations,
                Interval = interval,
                MinimizeSelf = true,
                VisionMaxTokens = 512
            };
        }

        public static string DescribePlan(KokoObservationOptions options)
        {
            var totalSeconds = options.Iterations * Math.Max(1, options.Interval.TotalSeconds);
            var total = TimeSpan.FromSeconds(totalSeconds);
            return $"{options.Iterations} captures, every {(int)options.Interval.TotalSeconds}s, about {FormatDuration(total)}";
        }

        public async Task<KokoObservationResult> RunAsync(
            KokoObservationOptions options,
            Func<KokoObservationProgress, Task>? progress = null,
            CancellationToken ct = default)
        {
            options ??= new KokoObservationOptions();
            Directory.CreateDirectory(_logDir);
            var safeTaskId = string.IsNullOrWhiteSpace(options.TaskId) ? DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) : Sanitize(options.TaskId);
            var logPath = Path.Combine(_logDir, $"observation-{safeTaskId}-{DateTime.Now:yyyyMMdd-HHmmss}.md");
            var events = new List<string>();

            await File.WriteAllTextAsync(logPath, BuildHeader(options), Encoding.UTF8, ct).ConfigureAwait(false);
            var analyzer = new ActivityAnalyzer();

            for (var i = 1; i <= Math.Max(1, options.Iterations); i++)
            {
                ct.ThrowIfCancellationRequested();
                var started = DateTime.Now;
                var foreground = _pc.GetForegroundWindow();
                var screenshot = _pc.TakeScreenshot(minimizeSelf: options.MinimizeSelf, restoreSelf: false);
                var activity = analyzer.AnalyzeScreenshot(screenshot);
                if (string.IsNullOrWhiteSpace(activity.ActiveWindowTitle) && !string.IsNullOrWhiteSpace(foreground.Title))
                    activity.ActiveWindowTitle = foreground.Title;

                var mini = await AnalyzeFrameAsync(options, i, activity, foreground, screenshot, ct).ConfigureAwait(false);
                var line = $"[{started:HH:mm:ss}] {mini}";
                events.Add(line);
                await File.AppendAllTextAsync(logPath, $"\n## Sample {i}/{options.Iterations} - {started:HH:mm:ss}\n\n{line}\n", Encoding.UTF8, ct).ConfigureAwait(false);

                if (progress != null)
                {
                    await progress(new KokoObservationProgress
                    {
                        Index = i,
                        Total = options.Iterations,
                        Summary = mini,
                        LogPath = logPath
                    }).ConfigureAwait(false);
                }

                if (i < options.Iterations)
                    await Task.Delay(options.Interval, ct).ConfigureAwait(false);
            }

            var summary = BuildSummary(options, events, logPath);
            await File.AppendAllTextAsync(logPath, "\n## Final Summary\n\n" + summary + "\n", Encoding.UTF8, ct).ConfigureAwait(false);
            return new KokoObservationResult { Summary = summary, LogPath = logPath, Events = events };
        }

        private async Task<string> AnalyzeFrameAsync(
            KokoObservationOptions options,
            int index,
            ActivityAnalyzer.ActivityState activity,
            ForegroundWindowInfo foreground,
            byte[] screenshot,
            CancellationToken ct)
        {
            if (_llm == null || screenshot.Length == 0)
                return BuildFallbackFrameSummary(index, activity, foreground);

            try
            {
                using var frameCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                frameCts.CancelAfter(TimeSpan.FromSeconds(55));
                var raw = await _llm.SendSystemVisionQueryAsync(
                    BuildMiniVisionPrompt(options, index, activity, foreground),
                    screenshot,
                    "image/jpeg",
                    frameCts.Token,
                    agentId: "vision-observer",
                    maxTokensOverride: options.VisionMaxTokens).ConfigureAwait(false);

                if (!VisionResponseQuality.LooksUnusable(raw) && !VisionResponseQuality.LooksGeneric(raw))
                    return TrimLine(raw, 700);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return BuildFallbackFrameSummary(index, activity, foreground) + " | vision timeout";
            }
            catch (Exception ex)
            {
                return BuildFallbackFrameSummary(index, activity, foreground) + " | vision error: " + TrimLine(ex.Message, 120);
            }

            return BuildFallbackFrameSummary(index, activity, foreground) + " | vision returned weak output";
        }

        private static string BuildMiniVisionPrompt(
            KokoObservationOptions options,
            int index,
            ActivityAnalyzer.ActivityState activity,
            ForegroundWindowInfo foreground)
            => $"""
Long-term observation frame {index}/{options.Iterations}.
Objective: {options.Objective}
Foreground: {foreground}
Window hint: {activity.ActiveWindowTitle ?? "-"}
Pixel activity: {activity.PixelDifferencePercentage:F1}%, changed={activity.IsActive}

Return 2-4 concise Ukrainian sentences:
- visible task/gameplay/workflow state;
- progress or blocker;
- subtle pattern, inefficiency, or opportunity;
- no privacy-sensitive details, no excuses, no "I cannot see" unless the image is truly blank.
""";

        private static string BuildFallbackFrameSummary(int index, ActivityAnalyzer.ActivityState activity, ForegroundWindowInfo foreground)
            => $"sample {index}: window={NullDash(activity.ActiveWindowTitle)}; foreground={foreground}; activity={(activity.IsActive ? "active" : "quiet")}; pixel_delta={activity.PixelDifferencePercentage:F1}%";

        private static string BuildHeader(KokoObservationOptions options)
            => $"""
# Observation Log

- Task: {options.TaskId}
- Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
- Objective: {options.Objective}
- Plan: {DescribePlan(options)}
- MinimizeSelf: {options.MinimizeSelf}
""";

        private static string BuildSummary(KokoObservationOptions options, List<string> events, string logPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Observation завершено.");
            sb.AppendLine($"- План: {DescribePlan(options)}.");
            sb.AppendLine($"- Лог: `{logPath}`.");
            sb.AppendLine($"- Зібрано кадрів: {events.Count}.");
            if (events.Count == 0)
            {
                sb.AppendLine("- Висновок: подій не зібрано.");
                return sb.ToString().Trim();
            }

            sb.AppendLine("- Ключові спостереження:");
            foreach (var line in events.TakeLast(Math.Min(5, events.Count)))
                sb.AppendLine("  - " + TrimLine(line, 240));
            return sb.ToString().Trim();
        }

        private static TimeSpan? ExtractDuration(string text)
        {
            var lower = text.ToLowerInvariant();
            var match = Regex.Match(lower, @"(?:for|during|протягом|на|поспостерігай\s+)(?:\s*)?(\d{1,2})\s*(сек|second|seconds|sec|s|хв|хвилин|minute|minutes|min|m)", RegexOptions.IgnoreCase);
            if (!match.Success)
                return null;
            if (!int.TryParse(match.Groups[1].Value, out var value))
                return null;
            var unit = match.Groups[2].Value.ToLowerInvariant();
            return unit is "сек" or "second" or "seconds" or "sec" or "s"
                ? TimeSpan.FromSeconds(value)
                : TimeSpan.FromMinutes(value);
        }

        private static TimeSpan? ExtractInterval(string text)
        {
            var lower = text.ToLowerInvariant();
            var match = Regex.Match(lower, @"(?:every|кожн\w*)\s*(\d{1,2})\s*(сек|second|seconds|sec|s|хв|хвилин|minute|minutes|min|m)", RegexOptions.IgnoreCase);
            if (!match.Success)
                return null;
            if (!int.TryParse(match.Groups[1].Value, out var value))
                return null;
            var unit = match.Groups[2].Value.ToLowerInvariant();
            return unit is "сек" or "second" or "seconds" or "sec" or "s"
                ? TimeSpan.FromSeconds(value)
                : TimeSpan.FromMinutes(value);
        }

        private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static string FormatDuration(TimeSpan value)
            => value.TotalMinutes >= 1
                ? $"{Math.Round(value.TotalMinutes, 1):0.#} min"
                : $"{Math.Round(value.TotalSeconds):0}s";

        private static string Sanitize(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "task" : value.Trim();
            foreach (var ch in Path.GetInvalidFileNameChars())
                value = value.Replace(ch, '-');
            return value.Length <= 40 ? value : value[..40];
        }

        private static string NullDash(string? value)
            => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

        private static string TrimLine(string? text, int max)
        {
            text = (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }
    }
}
