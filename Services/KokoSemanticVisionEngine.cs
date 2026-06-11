using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoUiElementSignal
    {
        public string Kind { get; set; } = "";
        public string Label { get; set; } = "";
        public string Meaning { get; set; } = "";
        public double Confidence { get; set; }
    }

    public sealed class KokoSemanticVisionFrame
    {
        public DateTime At { get; set; }
        public string FlowState { get; set; } = "unknown";
        public string PrimaryIntent { get; set; } = "";
        public string Summary { get; set; } = "";
        public string OcrDigest { get; set; } = "";
        public List<KokoUiElementSignal> UiElements { get; set; } = new();
        public List<string> SemanticSignals { get; set; } = new();
        public bool ShouldAssist { get; set; }
        public bool ShouldResearch { get; set; }
        public string ResearchTopic { get; set; } = "";
        public string AssistHint { get; set; } = "";
        public double Confidence { get; set; }

        public string PromptBlock
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine("SEMANTIC VISION 2.0");
                sb.AppendLine($"flow={FlowState}; intent={NullDash(PrimaryIntent)}; confidence={Confidence:F2}");
                if (!string.IsNullOrWhiteSpace(Summary))
                    sb.AppendLine($"summary={Summary}");
                if (!string.IsNullOrWhiteSpace(OcrDigest))
                    sb.AppendLine($"ocr_semantics={OcrDigest}");
                if (UiElements.Count > 0)
                    sb.AppendLine("ui_elements=" + string.Join(" | ", UiElements.Take(5).Select(e => $"{e.Kind}:{e.Label}=>{e.Meaning}")));
                if (SemanticSignals.Count > 0)
                    sb.AppendLine("signals=" + string.Join("; ", SemanticSignals.Take(8)));
                if (ShouldAssist)
                    sb.AppendLine($"assist_hint={AssistHint}");
                if (ShouldResearch)
                    sb.AppendLine($"curiosity_research_topic={ResearchTopic}");
                sb.AppendLine("Use this privately to understand work flow over time, not as text to quote.");
                return sb.ToString().Trim();
            }
        }

        private static string NullDash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }

    public sealed class KokoSemanticVisionEngine
    {
        public KokoSemanticVisionFrame BuildFrame(
            KokoScreenAwarenessAnalysis analysis,
            ActivityAnalyzer.ActivityState activity,
            KokoScreenSituation situation,
            KokoInternalState state,
            DateTime now)
        {
            if (analysis == null) throw new ArgumentNullException(nameof(analysis));
            if (activity == null) throw new ArgumentNullException(nameof(activity));
            if (situation == null) throw new ArgumentNullException(nameof(situation));
            if (state == null) throw new ArgumentNullException(nameof(state));

            var mode = KokoScreenAwarenessService.NormalizeMode(
                analysis.ScreenMode,
                $"{activity.ActiveWindowTitle} {analysis.SummaryUk} {analysis.ActivityUk} {situation.CurrentTask}");
            var text = JoinNonEmpty(
                activity.ActiveWindowTitle,
                analysis.SummaryUk,
                analysis.ActivityUk,
                analysis.CurrentTask,
                analysis.Progress,
                analysis.Blocker,
                situation.CurrentTask,
                situation.Progress,
                situation.Blocker);

            var frame = new KokoSemanticVisionFrame
            {
                At = now,
                FlowState = InferFlowState(state, situation, analysis, activity, mode, now),
                PrimaryIntent = InferPrimaryIntent(mode, text),
                Summary = BuildSummary(mode, situation, analysis, activity),
                OcrDigest = BuildOcrDigest(text),
                UiElements = ExtractUiElements(text, mode),
                Confidence = EstimateConfidence(analysis, activity, situation)
            };

            AddSemanticSignals(frame, mode, text, situation, analysis, activity);
            frame.ShouldAssist = situation.ShouldAssist ||
                                 ContainsAny(text, "error", "exception", "failed", "failure", "traceback", "timeout", "access denied", "line ") ||
                                 frame.UiElements.Any(e => e.Kind is "error_panel" or "progress_bar" && e.Confidence >= 0.70);
            frame.AssistHint = BuildAssistHint(frame, mode, situation, text);

            frame.ResearchTopic = InferResearchTopic(mode, situation, analysis, activity);
            frame.ShouldResearch = ShouldResearch(now, state, frame, mode, text);
            return frame;
        }

        public void ApplyToState(KokoInternalState state, KokoSemanticVisionFrame frame)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            state.LastSemanticVisionAt = frame.At;
            state.LastSemanticVisionFlow = frame.FlowState;
            state.LastSemanticVisionIntent = frame.PrimaryIntent;
            state.LastSemanticVisionSummary = frame.Summary;
            state.LastSemanticVisionOcr = frame.OcrDigest;
            state.LastSemanticVisionAssistHint = frame.ShouldAssist ? frame.AssistHint : "";
            state.LastSemanticVisionResearchTopic = frame.ShouldResearch ? frame.ResearchTopic : "";
            state.LastSemanticVisionConfidence = frame.Confidence;
            state.SemanticVisionTrace.Add($"{frame.At:HH:mm} {frame.FlowState}/{frame.PrimaryIntent}: {frame.Summary}");
            if (state.SemanticVisionTrace.Count > 80)
                state.SemanticVisionTrace.RemoveRange(0, state.SemanticVisionTrace.Count - 80);
        }

        public static string BuildSomaticToneDirective(KokoSomaticSnapshot snapshot, string screenMode)
        {
            if (snapshot == null) return "unknown";
            var mode = (screenMode ?? "").ToLowerInvariant();
            if (snapshot.Strain >= 0.72 && snapshot.Calm <= 0.35)
                return mode is "coding" or "obsidian" or "browser"
                    ? "quiet_operator: reduce sarcasm, keep actions concrete, no theatrics"
                    : "protective_low_noise: short, grounded, no teasing";
            if (snapshot.State == "tired" || snapshot.Calm < 0.25)
                return "low_power: shorter replies, avoid starting heavy tasks unless asked";
            if (snapshot.Calm >= 0.65 && snapshot.Strain <= 0.35)
                return "playful_clear: light sarcasm allowed, still useful";
            return "balanced: normal Kokonoe edge with practical focus";
        }

        private static string InferFlowState(
            KokoInternalState state,
            KokoScreenSituation situation,
            KokoScreenAwarenessAnalysis analysis,
            ActivityAnalyzer.ActivityState activity,
            string mode,
            DateTime now)
        {
            var currentTask = NormalizeKey(situation.CurrentTask);
            var previousTask = NormalizeKey(state.LastSemanticVisionSummary);
            var previousMode = (state.LastScreenAwarenessMode ?? "").ToLowerInvariant();
            var recent = state.LastSemanticVisionAt > DateTime.MinValue && now - state.LastSemanticVisionAt <= TimeSpan.FromMinutes(30);
            var progress = JoinNonEmpty(situation.Progress, analysis.Progress).ToLowerInvariant();
            var blocker = JoinNonEmpty(situation.Blocker, analysis.Blocker).ToLowerInvariant();

            if (!recent || string.IsNullOrWhiteSpace(previousTask))
                return "new_stream";
            if (!string.Equals(previousMode, mode, StringComparison.OrdinalIgnoreCase) && mode != "unknown")
                return "context_switch";
            if (ContainsAny(progress, "stuck", "blocked", "idle") || ContainsAny(blocker, "error", "failed", "exception", "timeout"))
                return "stuck_loop";
            if (ContainsAny(progress, "fixed", "resolved", "done", "success", "moving") && state.LastSemanticVisionFlow == "stuck_loop")
                return "recovered";
            if (activity.IsActive || activity.PixelDifferencePercentage >= 1.0)
                return "continuing_active";
            return "continuing_static";
        }

        private static string InferPrimaryIntent(string mode, string text)
        {
            var lower = text.ToLowerInvariant();
            if (ContainsAny(lower, "error", "exception", "build failed", "failed", "traceback")) return "debug";
            if (ContainsAny(lower, "commit", "pull request", "branch", "merge")) return "ship_code";
            if (mode == "coding") return "coding";
            if (mode == "obsidian") return "knowledge_work";
            if (mode == "game") return "gameplay";
            if (ContainsAny(lower, "telegram", "discord", "chat")) return "conversation";
            if (mode == "browser") return "research";
            return mode == "unknown" ? "observe" : mode;
        }

        private static string BuildSummary(string mode, KokoScreenSituation situation, KokoScreenAwarenessAnalysis analysis, ActivityAnalyzer.ActivityState activity)
        {
            var task = FirstNonEmpty(situation.CurrentTask, analysis.CurrentTask, analysis.SummaryUk, activity.ActiveWindowTitle, "unknown task");
            var progress = FirstNonEmpty(situation.Progress, analysis.Progress, "unknown");
            var blocker = FirstNonEmpty(situation.Blocker, analysis.Blocker, "");
            return Trim($"{mode}: {task}; progress={progress}" + (string.IsNullOrWhiteSpace(blocker) ? "" : $"; blocker={blocker}"), 260);
        }

        private static List<KokoUiElementSignal> ExtractUiElements(string text, string mode)
        {
            var lower = text.ToLowerInvariant();
            var results = new List<KokoUiElementSignal>();
            AddIf(results, ContainsAny(lower, "button", "btn", "save", "apply", "restart", "start", "stop", "retry"),
                "button", "action controls", "screen has actionable controls", 0.72);
            AddIf(results, ContainsAny(lower, "input", "textbox", "text field", "search", "prompt", "token"),
                "text_field", "editable field", "user can enter or correct data", 0.70);
            AddIf(results, ContainsAny(lower, "progress", "%", "loading", "installing", "building"),
                "progress_bar", "progress indicator", "wait or watch for completion/failure", 0.68);
            AddIf(results, ContainsAny(lower, "error", "exception", "failed", "access denied", "timeout", "crash"),
                "error_panel", "failure state", "needs diagnosis or log inspection", 0.84);
            AddIf(results, mode == "coding" || ContainsAny(lower, "visual studio", "rider", "vscode", ".cs", ".kt", "terminal"),
                "code_surface", "editor/terminal", "code or command output is visible", 0.75);
            AddIf(results, ContainsAny(lower, "telegram", "discord", "chat", "message"),
                "chat_surface", "conversation", "social context may matter", 0.65);
            return results;
        }

        private static void AddSemanticSignals(
            KokoSemanticVisionFrame frame,
            string mode,
            string text,
            KokoScreenSituation situation,
            KokoScreenAwarenessAnalysis analysis,
            ActivityAnalyzer.ActivityState activity)
        {
            if (mode != "unknown") frame.SemanticSignals.Add($"mode:{mode}");
            if (activity.IsActive) frame.SemanticSignals.Add($"pixel_activity:{activity.PixelDifferencePercentage:F1}%");
            if (situation.ShouldAssist) frame.SemanticSignals.Add("situation_requests_assist");
            if (ContainsAny(text, "error", "exception", "failed", "timeout")) frame.SemanticSignals.Add("visible_failure");
            if (ContainsAny(text, "line ", ".cs", ".kt", "stack trace")) frame.SemanticSignals.Add("code_semantics");
            if (analysis.Importance >= 0.70) frame.SemanticSignals.Add($"high_importance:{analysis.Importance:F2}");
        }

        private static string BuildAssistHint(KokoSemanticVisionFrame frame, string mode, KokoScreenSituation situation, string text)
        {
            if (!frame.ShouldAssist) return "";
            if (frame.UiElements.Any(e => e.Kind == "error_panel"))
                return "offer to inspect logs or isolate the failing line; be concrete";
            if (mode == "coding")
                return "offer a direct next debugging step, not generic encouragement";
            if (situation.ShouldAssist)
                return FirstNonEmpty(situation.Reason, situation.RecommendedBehavior, "assist with current visible task");
            return "ask one precise question or offer one low-risk action";
        }

        private static string InferResearchTopic(
            string mode,
            KokoScreenSituation situation,
            KokoScreenAwarenessAnalysis analysis,
            ActivityAnalyzer.ActivityState activity)
        {
            var raw = FirstNonEmpty(situation.Blocker, situation.CurrentTask, analysis.CurrentTask, analysis.SummaryUk, activity.ActiveWindowTitle, "");
            raw = raw.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var topic = Trim(raw, 90);
            if (mode == "coding" && !topic.Contains("coding", StringComparison.OrdinalIgnoreCase))
                topic = "coding issue: " + topic;
            return topic;
        }

        private static bool ShouldResearch(DateTime now, KokoInternalState state, KokoSemanticVisionFrame frame, string mode, string text)
        {
            if (string.IsNullOrWhiteSpace(frame.ResearchTopic))
                return false;
            if (state.LastSemanticVisionAt > DateTime.MinValue &&
                now - state.LastSemanticVisionAt < TimeSpan.FromMinutes(8) &&
                string.Equals(state.LastSemanticVisionResearchTopic, frame.ResearchTopic, StringComparison.OrdinalIgnoreCase))
                return false;

            var researchMode = mode is "coding" or "browser" or "obsidian";
            var blocked = frame.FlowState == "stuck_loop" || ContainsAny(text, "unknown", "error", "exception", "failed", "how to", "docs", "api");
            return researchMode && blocked && frame.Confidence >= 0.45;
        }

        private static double EstimateConfidence(KokoScreenAwarenessAnalysis analysis, ActivityAnalyzer.ActivityState activity, KokoScreenSituation situation)
        {
            var score = 0.35;
            if (!string.IsNullOrWhiteSpace(analysis.SummaryUk)) score += 0.20;
            if (!string.IsNullOrWhiteSpace(situation.CurrentTask)) score += 0.15;
            if (!string.IsNullOrWhiteSpace(activity.ActiveWindowTitle)) score += 0.10;
            if (analysis.Importance > 0) score += Math.Min(0.20, analysis.Importance * 0.20);
            return Math.Clamp(score, 0.0, 1.0);
        }

        private static string BuildOcrDigest(string text)
        {
            var lines = text
                .Split(new[] { '\r', '\n', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length >= 8)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .Select(l => Trim(l, 90));
            return string.Join(" / ", lines);
        }

        private static void AddIf(List<KokoUiElementSignal> list, bool condition, string kind, string label, string meaning, double confidence)
        {
            if (!condition) return;
            list.Add(new KokoUiElementSignal
            {
                Kind = kind,
                Label = label,
                Meaning = meaning,
                Confidence = confidence
            });
        }

        private static string JoinNonEmpty(params string?[] values)
            => string.Join(" ", values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()));

        private static string FirstNonEmpty(params string?[] values)
            => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? "";

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static string NormalizeKey(string? text)
        {
            text = (text ?? "").Trim().ToLowerInvariant();
            if (text.Length > 80) text = text[..80];
            return text;
        }

        private static string Trim(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Trim();
            if (text.Length <= max) return text;
            var cut = text.LastIndexOf(' ', Math.Min(max, text.Length - 1));
            if (cut < max / 2) cut = max;
            return text[..cut].TrimEnd() + "...";
        }
    }
}
