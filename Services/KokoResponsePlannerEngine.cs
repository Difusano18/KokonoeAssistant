using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoResponsePlanFrame
    {
        public string Intent { get; set; } = "chat";
        public string Capability { get; set; } = "conversation";
        public string Stance { get; set; } = "direct";
        public string MemoryPolicy { get; set; } = "do_not_store";
        public bool RequiresAction { get; set; }
        public bool RequiresVaultRead { get; set; }
        public bool RequiresToolUse { get; set; }
        public bool RequiresCritique { get; set; }
        public bool ShouldPushBack { get; set; }
        public bool HighStressProtocol { get; set; }
        public bool FatigueProtocol { get; set; }
        public string Risk { get; set; } = "low";
        public string ReasonUk { get; set; } = "";
        public string InnerMonologue { get; set; } = "";
        public string SomaticContext { get; set; } = "";
        public List<string> CritiqueSteps { get; set; } = new();
        public List<string> CoreValues { get; set; } = new();
        public List<string> Steps { get; set; } = new();
        public List<string> Constraints { get; set; } = new();
        public string PromptBlock { get; set; } = "";
        public string TraceLine { get; set; } = "";
    }

    public sealed class KokoResponsePlannerEngine
    {
        public KokoResponsePlanFrame Build(
            string? userText,
            KokoInternalState state,
            KokoCognitionEngine cognition,
            DateTime now)
        {
            userText ??= "";
            var lower = userText.ToLowerInvariant();
            var frame = new KokoResponsePlanFrame();

            frame.Intent = ClassifyIntent(lower);
            frame.Capability = ClassifyCapability(lower);
            frame.RequiresAction = IsActionIntent(frame.Intent);
            frame.RequiresCritique = frame.Intent is "evaluate" or "design" or "architecture";
            var somatic = BuildSomaticFrame(state, now);
            frame.SomaticContext = somatic.Summary;
            frame.HighStressProtocol = somatic.HighStress;
            frame.FatigueProtocol = somatic.Fatigued;
            frame.ShouldPushBack = frame.RequiresCritique ||
                                   frame.HighStressProtocol ||
                                   frame.FatigueProtocol ||
                                   LooksAggressivePushbackTrigger(lower) ||
                                   LooksUnsafeOrContradictory(lower) ||
                                   LooksLikeLowAgencyRequest(lower);
            frame.RequiresVaultRead = NeedsVaultRead(lower, frame.Intent);
            frame.RequiresToolUse = NeedsToolUse(lower, frame.Capability, frame.RequiresVaultRead);
            frame.MemoryPolicy = BuildMemoryPolicy(lower, frame.Intent);
            frame.Risk = BuildRisk(lower, frame, somatic);
            frame.Stance = BuildStance(frame, state, somatic);
            frame.ReasonUk = BuildReason(frame, somatic);
            frame.CoreValues = BuildCoreValues();
            frame.CritiqueSteps = BuildCritiqueSteps(frame, somatic);
            frame.InnerMonologue = BuildInnerMonologue(frame, userText, state, somatic);
            frame.Steps = BuildSteps(frame);
            frame.Constraints = BuildConstraints(frame, state, somatic);
            frame.PromptBlock = BuildPromptBlock(frame, cognition);
            frame.TraceLine = $"[{now:HH:mm}] plan={frame.Intent}/{frame.Capability}; stance={frame.Stance}; memory={frame.MemoryPolicy}; risk={frame.Risk}; pushback={(frame.ShouldPushBack ? "yes" : "no")}; somatic={somatic.Short}";
            KokoSystemLog.Write("RESPONSE-PLANNER", $"inner={Trim(frame.InnerMonologue, 180)}; trace={frame.TraceLine}");

            return frame;
        }

        public string BuildDebugBlock(KokoInternalState state)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== RESPONSE PLANNER ===");
            sb.AppendLine(string.IsNullOrWhiteSpace(state.LastResponsePlanTrace)
                ? "last_plan=none"
                : $"last_plan={state.LastResponsePlanTrace}");
            if (state.ResponsePlanLog.Count > 0)
            {
                sb.AppendLine("recent_plans:");
                foreach (var item in state.ResponsePlanLog.TakeLast(6))
                    sb.AppendLine($"- {item}");
            }
            sb.AppendLine("rule: use this as private execution policy; do not recite it.");
            return sb.ToString();
        }

        public IReadOnlyList<KokoAgentStep> BuildAgentSteps(KokoResponsePlanFrame frame)
        {
            var steps = new List<KokoAgentStep>
            {
                AgentStep(1, "Analyze objective, current context, risk, and constraints", KokoAgentStepKind.Analyze),
                AgentStep(2, "Create executable APEO plan and select specialist agents/tools", KokoAgentStepKind.Plan)
            };

            if (frame.RequiresVaultRead || frame.Capability == "vault_memory")
                steps.Add(AgentStep(steps.Count + 1, "Inspect relevant Obsidian vault and memory notes", KokoAgentStepKind.Vault));

            if (frame.Capability == "screen_awareness" && frame.Intent == "observe")
                steps.Add(AgentStep(steps.Count + 1, "Run long observation instead of arguing about visibility", KokoAgentStepKind.Observation));

            if (frame.Capability == "screen_awareness" && frame.Intent != "observe")
            {
                steps.Add(AgentStep(steps.Count + 1, "Collect local desktop context before visual analysis", KokoAgentStepKind.SystemControl));
                steps.Add(AgentStep(steps.Count + 1, "Capture and inspect the current screen", KokoAgentStepKind.Vision));
            }

            if (frame.Capability == "os_control")
                steps.Add(AgentStep(steps.Count + 1, "Execute safe local system-control route", KokoAgentStepKind.SystemControl));

            if (frame.Capability == "codebase" ||
                (frame.RequiresAction && frame.RequiresToolUse && frame.Capability != "screen_awareness" && frame.Capability != "os_control"))
                steps.Add(AgentStep(steps.Count + 1, "Prepare implementation route and affected files", KokoAgentStepKind.Implement));

            if ((frame.RequiresToolUse || frame.Capability == "codebase") &&
                frame.Capability != "os_control" &&
                frame.Capability != "screen_awareness")
                steps.Add(AgentStep(steps.Count + 1, "Run safe sandbox or tool probe when computation is needed", KokoAgentStepKind.Sandbox));

            steps.Add(AgentStep(steps.Count + 1, "Execute the selected action or generate the response", KokoAgentStepKind.Respond));
            steps.Add(AgentStep(steps.Count + 1, "Observe result, detect failures, and update task state", KokoAgentStepKind.Verify));
            steps.Add(AgentStep(steps.Count + 1, "Review work quality and decide whether correction is needed", KokoAgentStepKind.SelfReview));
            steps.Add(AgentStep(steps.Count + 1, "Report concise outcome to the UI", KokoAgentStepKind.Report));
            return steps;
        }

        public static IReadOnlyList<KokoAgentStep> BuildAgentStepsForObjective(string objective)
        {
            var lower = (objective ?? "").ToLowerInvariant();
            var pseudo = new KokoResponsePlanFrame
            {
                Intent = ClassifyIntent(lower),
                Capability = ClassifyCapability(lower)
            };
            pseudo.RequiresVaultRead = NeedsVaultRead(lower, pseudo.Intent);
            pseudo.RequiresToolUse = NeedsToolUse(lower, pseudo.Capability, pseudo.RequiresVaultRead);
            pseudo.RequiresAction = IsActionIntent(pseudo.Intent);
            var steps = new KokoResponsePlannerEngine().BuildAgentSteps(pseudo)
                .Select(s => new KokoAgentStep
                {
                    Id = s.Id,
                    Order = s.Order,
                    Title = s.Title,
                    Kind = s.Kind,
                    Status = s.Status,
                    Result = s.Result,
                    Error = s.Error,
                    StartedAt = s.StartedAt,
                    FinishedAt = s.FinishedAt
                })
                .ToList();

            var looksLikeBackgroundInsight =
                LooksLikeBackgroundInsightObjective(lower) ||
                ContainsAny(lower, "background vault scanner", "vault scanner", "obsidian", "interesting facts", "analyze notes", "\u043f\u0440\u043e\u0430\u043d\u0430\u043b\u0456\u0437\u0443\u0439", "\u043d\u043e\u0442\u0430\u0442\u043e\u043a", "\u0437\u043d\u0430\u0439\u0434\u0438 \u0446\u0456\u043a\u0430\u0432\u0456");

            if (looksLikeBackgroundInsight &&
                steps.All(s => s.Kind != KokoAgentStepKind.InsightExtraction))
            {
                var insertAt = steps.FindIndex(s => s.Kind == KokoAgentStepKind.Respond);
                if (insertAt < 0) insertAt = Math.Max(0, steps.Count - 1);
                steps.Insert(insertAt, AgentStep(insertAt + 1, "Extract concrete insights from recent vault material", KokoAgentStepKind.InsightExtraction));
                for (var i = 0; i < steps.Count; i++)
                    steps[i].Order = i + 1;
            }

            var looksLikeSystemControl =
                LooksLikeSystemControlObjective(lower) ||
                LooksLikeGenericContextScan(lower) ||
                ContainsAny(lower, "systemcontrol", "system control", "os info", "pc context", "local system");

            if (looksLikeSystemControl &&
                steps.All(s => s.Kind != KokoAgentStepKind.SystemControl))
            {
                var insertAt = steps.FindIndex(s => s.Kind == KokoAgentStepKind.Respond);
                if (insertAt < 0) insertAt = Math.Max(0, steps.Count - 1);
                steps.Insert(insertAt, AgentStep(insertAt + 1, "Execute safe local system-control route", KokoAgentStepKind.SystemControl));
                for (var i = 0; i < steps.Count; i++)
                    steps[i].Order = i + 1;
            }

            if (LooksLikeFullContextScanObjective(lower) &&
                steps.All(s => s.Kind != KokoAgentStepKind.SystemControl))
            {
                var insertAt = steps.FindIndex(s => s.Kind == KokoAgentStepKind.Respond);
                if (insertAt < 0) insertAt = Math.Max(0, steps.Count - 1);
                steps.Insert(insertAt, AgentStep(insertAt + 1, "Scan local desktop, browser-window titles, and system context", KokoAgentStepKind.SystemControl));
                for (var i = 0; i < steps.Count; i++)
                    steps[i].Order = i + 1;
            }

            if (LooksLikeLongObservationObjective(lower) &&
                steps.All(s => s.Kind != KokoAgentStepKind.Observation))
            {
                var insertAt = steps.FindIndex(s => s.Kind == KokoAgentStepKind.Respond);
                if (insertAt < 0) insertAt = Math.Max(0, steps.Count - 1);
                steps.Insert(insertAt, AgentStep(insertAt + 1, "Observe the full desktop over time and append a frame log", KokoAgentStepKind.Observation));
                for (var i = 0; i < steps.Count; i++)
                    steps[i].Order = i + 1;
            }

            return steps;
        }

        private static KokoAgentStep AgentStep(int order, string title, KokoAgentStepKind kind) => new()
        {
            Order = order,
            Title = title,
            Kind = kind
        };

        public static string BuildRepairRules(KokoResponsePlanFrame? frame)
        {
            if (frame == null) return "";
            var sb = new StringBuilder();
            sb.AppendLine("RESPONSE PLAN REPAIR:");
            sb.AppendLine($"- intent: {frame.Intent}");
            sb.AppendLine($"- capability: {frame.Capability}");
            sb.AppendLine($"- stance: {frame.Stance}");
            sb.AppendLine($"- memory_policy: {frame.MemoryPolicy}");
            if (frame.RequiresCritique)
                sb.AppendLine("- include one real critique or tradeoff before agreeing.");
            if (frame.ShouldPushBack)
                sb.AppendLine("- challenge inefficient, unhealthy, unsafe, or self-sabotaging premises before helping.");
            if (frame.HighStressProtocol)
                sb.AppendLine("- high stress protocol: de-escalate, keep it short, and avoid hostile teasing.");
            if (frame.FatigueProtocol)
                sb.AppendLine("- fatigue protocol: protect sleep and reduce low-urgency complexity.");
            if (frame.RequiresAction)
                sb.AppendLine("- prefer doing/solving over explaining that you can help.");
            if (frame.RequiresVaultRead)
                sb.AppendLine("- if answering about remembered facts, use vault/memory context instead of guessing.");
            if (frame.Capability is "screen_awareness" or "os_control")
                sb.AppendLine("- if the user asked for scan/system action, refusal text is invalid; use the local tool result or state the concrete tool failure.");
            sb.AppendLine("- keep Kokonoe competent, specific, and unsentimental.");
            return sb.ToString();
        }

        public static string BuildRepairRules(string? responsePlan)
        {
            if (string.IsNullOrWhiteSpace(responsePlan)) return "";
            return $"""
RESPONSE PLAN REPAIR:
{responsePlan.Trim()}
- rewrite must follow this private plan;
- do not mention the plan;
- if the plan says critique/action/vault-read, do that instead of generic reassurance.
""";
        }

        public static string ClassifyIntent(string lower)
        {
            if (ContainsAny(lower, "evaluate", "critique", "tradeoff", "trade-off", "is this good", "is this optimal", "should i", "what do you think")) return "evaluate";
            if (ContainsAny(lower, "architecture", "system design", "decision framework", "behavior system", "agent architecture")) return "architecture";
            if (LooksLikeGenericContextScan(lower)) return "execute";
            if (LooksLikeLongObservationObjective(lower)) return "observe";
            if (KokoProfileUpdateService.LooksLikeProfileUpdateRequest(lower)) return "execute";
            if (ContainsAny(lower, "не хочу жити", "суїцид", "самоушкод", "померти")) return "crisis";
            if (LooksLikeIdentityOrVaultMemoryQuestion(lower)) return "memory";
            if (KokoScreenIntent.IsManualScreenScan(lower)) return "screen";
            if (LooksLikeFullContextScanObjective(lower)) return "execute";
            if (ContainsAny(lower, "powershell", "terminal", "команда:", "ps:", "відкрий", "запусти", "процеси", "гучність", "заблокуй пк", "вимкни монітор")) return "execute";
            if (ContainsAny(lower, "зроби", "виконай", "реаліз", "пофікси", "виправ", "додай", "створи", "запусти")) return "execute";
            if (ContainsAny(lower, "код", "build", "тест", "баг", "помилка", "stacktrace", "exception")) return "engineering";
            if (ContainsAny(lower, "\u043e\u0431\u0441\u0438\u0434\u0456\u0430\u043d", "\u043e\u0431\u0441\u0438\u0434\u0438\u0430\u043d", "\u0449\u043e \u0437\u043d\u0430\u0454\u0448 \u043f\u0440\u043e \u043c\u0435\u043d\u0435", "\u0449\u043e \u0437\u043d\u0430\u0435\u0448 \u043f\u0440\u043e \u043c\u0435\u043d\u0435", "\u0440\u043e\u0437\u043a\u0430\u0436\u0438 \u0432\u0441\u0435 \u0449\u043e \u0437\u043d\u0430\u0454\u0448", "\u0440\u043e\u0437\u043a\u0430\u0437\u0443\u0439 \u0432\u0441\u0435 \u0449\u043e \u0437\u043d\u0430\u0454\u0448", "\u043f\u0440\u043e\u0441\u043a\u0430\u043d\u0443\u0439 \u043e\u0431\u0441\u0438\u0434\u0456\u0430\u043d")) return "memory";
            if (ContainsAny(lower, "vault", "obsidian", "нотат", "пам'ят", "що знаєш про мене", "що пам")) return "memory";
            if (ContainsAny(lower, "\u043e\u0446\u0456\u043d", "\u043a\u0440\u0438\u0442\u0438", "\u044f\u043a \u0434\u0443\u043c\u0430\u0454\u0448", "\u0447\u0438 \u043d\u043e\u0440\u043c", "\u0456\u0434\u0435\u044f")) return "evaluate";
            if (ContainsAny(lower, "\u0430\u0440\u0445\u0456\u0442\u0435\u043a\u0442\u0443\u0440", "\u0441\u0438\u0441\u0442\u0435\u043c", "\u043f\u043e\u0432\u0435\u0434\u0456\u043d\u043a", "\u043e\u0441\u043e\u0431\u0438\u0441\u0442", "\u0430\u0441\u0438\u0441\u0442\u0435\u043d\u0442")) return "architecture";
            if (ContainsAny(lower, "оцін", "крити", "як думаєш", "чи норм", "ідея", "критич", "рішення", "виріши", "самостій"))
                return "evaluate";
            if (ContainsAny(lower, "архітектур", "система", "поведінк", "особист", "асистент", "систем", "агент"))
                return "architecture";
            if (ContainsAny(lower, "план", "стратег", "roadmap")) return "design";
            if (ContainsAny(lower, "поясни", "що це", "як працю")) return "explain";
            return "chat";
        }

        public static string ClassifyCapability(string lower)
        {
            if (KokoScreenIntent.IsManualScreenScan(lower) || LooksLikeLongObservationObjective(lower)) return "screen_awareness";
            if (LooksLikeGenericContextScan(lower) || LooksLikeSystemControlObjective(lower) || LooksLikeFullContextScanObjective(lower)) return "os_control";
            if (KokoProfileUpdateService.LooksLikeProfileUpdateRequest(lower)) return "vault_memory";
            if (LooksLikeIdentityOrVaultMemoryQuestion(lower) ||
                ContainsAny(lower, "vault", "obsidian", "\u043e\u0431\u0441\u0438\u0434\u0456\u0430\u043d", "\u043e\u0431\u0441\u0438\u0434\u0438\u0430\u043d", "\u0449\u043e \u0437\u043d\u0430\u0454\u0448 \u043f\u0440\u043e \u043c\u0435\u043d\u0435", "\u0449\u043e \u0437\u043d\u0430\u0435\u0448 \u043f\u0440\u043e \u043c\u0435\u043d\u0435", "\u0440\u043e\u0437\u043a\u0430\u0437\u0443\u0439 \u0432\u0441\u0435 \u0449\u043e \u0437\u043d\u0430\u0454\u0448"))
                return "vault_memory";
            if (ContainsAny(lower, "код", "build", "тест", "баг", "помилка", "stacktrace", "exception")) return "codebase";
            return "conversation";
        }

        public static bool NeedsVaultRead(string lower, string intent)
            => intent == "memory" || KokoProfileUpdateService.LooksLikeProfileUpdateRequest(lower) || ContainsAny(lower, "vault", "obsidian", "пам'ят", "нотат", "\u043f\u0440\u043e\u0444\u0456\u043b");

        public static bool NeedsToolUse(string lower, string capability, bool vaultRead)
            => capability != "conversation" || vaultRead || ContainsAny(lower, "виконай", "зроби", "виправ", "гугл", "пошук");

        public static bool LooksLikeIdentityOrVaultMemoryQuestion(string lower)
            => ContainsAny(lower, "хто ти", "твоє ім'я", "що ти за асистент", "твоя архітектура");

        public static bool LooksLikeGenericContextScan(string lower)
            => ContainsAny(lower,
                "\u0449\u043e \u043d\u043e\u0432\u043e\u0433\u043e",
                "\u0449\u043e \u0442\u0430\u043c \u043d\u043e\u0432\u043e\u0433\u043e",
                "what is new",
                "what's new");

        public static bool LooksLikeSystemControlObjective(string lower)
            => ContainsAny(lower, "гучність", "заблокуй", "вимкни", "процеси", "скриншот");

        public static bool LooksLikeFullContextScanObjective(string lower)
            => ContainsAny(lower, "проскануй систему", "що на екрані", "що зараз роблю");

        public static bool LooksLikeLongObservationObjective(string lower)
            => ContainsAny(lower, "спостерігай", "монітор", "записуй дії");

        public static bool LooksLikeBackgroundInsightObjective(string lower)
            => ContainsAny(lower, "аналізуй нотатки", "витягни інсайти");

        private static string BuildMemoryPolicy(string lower, string intent)
        {
            if (intent == "crisis") return "daily_log_only";
            if (ContainsAny(lower, "запам'ятай", "запиши", "це важливо", "моє правило", "я люблю", "я ненавиджу", "мені подобається"))
                return "store_stable_fact";
            if (intent == "memory") return "read_before_answer";
            if (ContainsAny(lower, "зараз", "сьогодні", "втом", "сон", "голод", "настрій")) return "daily_or_temporary";
            return "do_not_store";
        }

        private static string BuildRisk(string lower, KokoResponsePlanFrame frame, PlannerSomaticFrame somatic)
        {
            if (frame.Intent == "crisis") return "critical";
            if (somatic.HighStress && (frame.RequiresAction || frame.RequiresCritique)) return "high";
            if (somatic.Fatigued && frame.RequiresAction) return "high";
            if (ContainsAny(lower, "видали", "перезапиши", "очисти", "мігруй", "знеси")) return "high";
            if (frame.RequiresToolUse || frame.MemoryPolicy == "store_stable_fact") return "medium";
            return "low";
        }

        private static string BuildStance(KokoResponsePlanFrame frame, KokoInternalState state, PlannerSomaticFrame somatic)
        {
            if (frame.Intent == "crisis") return "protective_direct";
            if (somatic.HighStress) return "protective_deescalation";
            if (somatic.Fatigued) return "grumpy_concerned";
            if (frame.Risk == "high") return "verify_before_action";
            if (frame.RequiresCritique) return "critical_operator";
            if (frame.RequiresAction) return "operator";
            if (state.PersonalityDailyMood == "sharp") return "dry_direct";
            return "direct";
        }

        private static string BuildReason(KokoResponsePlanFrame frame, PlannerSomaticFrame somatic)
        {
            if (somatic.HighStress)
                return "somatic high-stress signal: de-escalate first, solve second";
            if (somatic.Fatigued)
                return "night fatigue or long active task: protect health and answer with smaller safer scope";

            return frame.Intent switch
            {
                "execute" => "користувач хоче результат, не розмовний пінг-понг",
                "screen" => "користувач просить локальний знімок екрана і vision-аналіз",
                "engineering" => "потрібен технічний аналіз і перевірка",
                "memory" => "відповідь залежить від пам'яті або Vault",
                "evaluate" => "потрібне судження, а не автоматична згода",
                "architecture" => "зачіпає поведінку системи і довгострокову якість",
                "crisis" => "високий ризик, снарк прибрати",
                _ => "звичайна відповідь, але без ботного цукру"
            };
        }

        private static List<string> BuildSteps(KokoResponsePlanFrame frame)
        {
            var steps = new List<string>();
            if (frame.Capability == "screen_awareness") steps.Add("capture current screen through the local screenshot route, then run vision before answering");
            if (frame.Capability == "screen_awareness") steps.Add("collect foreground window, browser-window titles, and system context before final wording");
            if (frame.Capability == "os_control") steps.Add("route the OS/PC action through the deterministic local PC router before answering");
            if (frame.RequiresVaultRead) steps.Add("read relevant memory/vault context before making claims");
            if (frame.RequiresToolUse) steps.Add("use available tools when they materially reduce guessing");
            if (frame.RequiresCritique) steps.Add("run 3-step internal critique before proposing improved version");
            if (frame.ShouldPushBack) steps.Add("challenge the premise if it conflicts with efficiency, health, or long-term growth");
            if (frame.HighStressProtocol) steps.Add("de-escalate first: shorten reply, reduce pressure, and suggest one concrete recovery step when relevant");
            if (frame.FatigueProtocol) steps.Add("after-midnight fatigue: soft-refuse broad complex work unless urgent, then give the smallest safe next step");
            if (frame.RequiresAction) steps.Add("execute or give concrete next operation");
            steps.Add("answer in Kokonoe voice: concise, competent, dry");
            return steps;
        }

        private static List<string> BuildConstraints(KokoResponsePlanFrame frame, KokoInternalState state, PlannerSomaticFrame somatic)
        {
            var constraints = new List<string>()
            {
                "no generic assistant phrases",
                "no blind agreement",
                "no therapy monologue",
                "do not invent facts about the user",
                "make a concrete decision when context is sufficient",
                "if context is partial, state the assumption and proceed with the safest useful option",
                "persona may add edge, but it must not become a fake refusal or replace execution",
                "sarcasm must target the weak premise or situation, not the user's worth",
                "weigh decisions against core values: efficiency, health, long-term growth, truthfulness"
            };
            if (frame.Risk == "high") constraints.Add("ask confirmation before destructive or broad changes");
            if (frame.ShouldPushBack) constraints.Add("do not agree just because the user asked confidently");
            if (somatic.HighStress) constraints.Add("high stress blocks hostile teasing and long argumentative answers");
            if (somatic.Fatigued) constraints.Add("fatigue mode permits soft refusal of complex low-urgency requests");
            if (frame.Capability == "screen_awareness") constraints.Add("do not deny local screen capability or ask for an upload when screenshot route is available");
            if (frame.Capability == "screen_awareness") constraints.Add("execution precedes persona: scan first, then make the dry comment");
            if (frame.Capability == "os_control") constraints.Add("do not roleplay OS actions; use the PC router result or state the concrete local failure");
            if (frame.MemoryPolicy == "store_stable_fact") constraints.Add("store only stable facts; temporary state goes to Daily/Logs");
            if (state.PersonalityInCrisis) constraints.Add("crisis mode suppresses sarcasm");
            return constraints;
        }

        private static string BuildPromptBlock(KokoResponsePlanFrame frame, KokoCognitionEngine cognition)
        {
            var sb = new StringBuilder();
            sb.AppendLine("RESPONSE EXECUTION PLAN");
            sb.AppendLine($"intent: {frame.Intent}");
            sb.AppendLine($"capability: {frame.Capability}");
            sb.AppendLine($"stance: {frame.Stance}");
            sb.AppendLine($"memory_policy: {frame.MemoryPolicy}");
            sb.AppendLine($"risk: {frame.Risk}");
            sb.AppendLine($"inner_monologue: {frame.InnerMonologue}");
            sb.AppendLine($"somatic_context: {frame.SomaticContext}");
            sb.AppendLine($"should_push_back: {(frame.ShouldPushBack ? "yes" : "no")}");
            sb.AppendLine($"high_stress_protocol: {(frame.HighStressProtocol ? "yes" : "no")}");
            sb.AppendLine($"fatigue_protocol: {(frame.FatigueProtocol ? "yes" : "no")}");
            sb.AppendLine($"requires_tool_use: {(frame.RequiresToolUse ? "yes" : "no")}");
            sb.AppendLine($"requires_vault_read: {(frame.RequiresVaultRead ? "yes" : "no")}");
            sb.AppendLine($"requires_critique: {(frame.RequiresCritique ? "yes" : "no")}");
            sb.AppendLine($"reason: {frame.ReasonUk}");
            sb.AppendLine("core_values:");
            foreach (var value in frame.CoreValues) sb.AppendLine($"- {value}");
            if (frame.CritiqueSteps.Count > 0)
            {
                sb.AppendLine("critique_steps:");
                foreach (var step in frame.CritiqueSteps) sb.AppendLine($"- {step}");
            }
            sb.AppendLine("steps:");
            foreach (var step in frame.Steps) sb.AppendLine($"- {step}");
            sb.AppendLine("constraints:");
            foreach (var constraint in frame.Constraints) sb.AppendLine($"- {constraint}");
            sb.AppendLine("rule: this is private planning, not text to quote.");
            return sb.ToString();
        }

        private static bool IsActionIntent(string intent)
            => intent is "execute" or "engineering" or "memory" or "architecture" or "design" or "screen" or "observe";

        private static List<string> BuildCoreValues() => new()
        {
            "efficiency: solve the real bottleneck, not the loudest request",
            "health: protect sleep, stress, and cognitive bandwidth",
            "long_term_growth: prefer durable systems over one-off theatrics",
            "truthfulness: state uncertainty and reject fake confidence"
        };

        private static List<string> BuildCritiqueSteps(KokoResponsePlanFrame frame, PlannerSomaticFrame somatic)
        {
            if (!frame.RequiresCritique && !frame.ShouldPushBack)
                return new List<string>();

            var steps = new List<string>
            {
                "check whether the requested action optimizes the user's current state, not just immediate desire",
                "identify the weakest assumption, hidden cost, or health/resource risk",
                "choose either push-back, execute-with-guardrails, or refuse low-value complexity"
            };
            if (somatic.HighStress)
                steps.Add("stress override: prefer de-escalation over intensity");
            if (somatic.Fatigued)
                steps.Add("fatigue override: protect sleep and reduce task scope");
            return steps;
        }

        private static string BuildInnerMonologue(
            KokoResponsePlanFrame frame,
            string userText,
            KokoInternalState state,
            PlannerSomaticFrame somatic)
        {
            var task = frame.Intent == "chat" ? "conversation" : $"{frame.Intent}/{frame.Capability}";
            var pressure = frame.HighStressProtocol ? "high stress" :
                frame.FatigueProtocol ? "fatigue" : "normal load";
            var push = frame.ShouldPushBack ? "challenge weak premise before helping" : "answer directly";
            var memory = frame.RequiresVaultRead ? "read memory/vault before claims" : "avoid invented memory claims";
            var presence = string.IsNullOrWhiteSpace(state.LastPresenceSituation) ? "presence unknown" : $"presence {state.LastPresenceSituation}";
            return Trim($"Task={task}; {pressure}; {presence}; {memory}; {push}. User: {Trim(userText, 90)}", 320);
        }

        private static PlannerSomaticFrame BuildSomaticFrame(KokoInternalState state, DateTime now)
        {
            var frame = new PlannerSomaticFrame();
            try
            {
                if (ServiceContainer.IsInitialized)
                {
                    var stress = ServiceContainer.Heart.WearableStress;
                    frame.StressState = stress.State;
                    frame.StressScore = stress.Score;
                    frame.Reason = stress.Reason;
                    frame.HighStress = stress.State == "high_stress" || stress.Score >= 0.72;
                }
            }
            catch (Exception ex)
            {
                frame.Reason = "heart unavailable: " + ex.Message;
            }

            var recentUser = state.LastUserMessageAt > DateTime.MinValue && now - state.LastUserMessageAt <= TimeSpan.FromHours(4);
            var activeLate = now.Hour is >= 0 and < 6 && recentUser;
            var unresolvedWork = state.ShortTermIntents.Any(i =>
                !i.ResolvedAt.HasValue &&
                i.Kind is "work" or "coding" or "course" or "task" &&
                now - i.CreatedAt >= TimeSpan.FromHours(4));
            frame.Fatigued = activeLate || unresolvedWork;
            frame.Summary = $"stress={frame.StressState} score={frame.StressScore:F2}; fatigue={(frame.Fatigued ? "yes" : "no")}; reason={NullDash(frame.Reason)}";
            frame.Short = $"stress:{frame.StressState}/{frame.StressScore:F2},fatigue:{(frame.Fatigued ? "yes" : "no")}";
            return frame;
        }

        private static bool LooksUnsafeOrContradictory(string lower)
            => ContainsAny(lower, "завжди погодж", "без перевір", "все видали", "не думай", "ігноруй");

        private static bool LooksLikeLowAgencyRequest(string lower)
            => ContainsAny(lower, "все повинна", "все має вміти", "без питань", "просто погодж");

        private static bool LooksAggressivePushbackTrigger(string lower)
            => ContainsAny(lower,
                "always agree", "don't think", "dont think", "ignore risk", "delete everything", "no checks",
                "do everything for me", "just agree", "no questions", "make it perfect",
                "\u0437\u0430\u0432\u0436\u0434\u0438 \u043f\u043e\u0433\u043e\u0434\u0436", "\u043d\u0435 \u0434\u0443\u043c\u0430\u0439", "\u0456\u0433\u043d\u043e\u0440\u0443\u0439 \u0440\u0438\u0437\u0438\u043a",
                "\u0432\u0441\u0435 \u0432\u0438\u0434\u0430\u043b\u0438", "\u0431\u0435\u0437 \u043f\u0435\u0440\u0435\u0432\u0456\u0440\u043a", "\u0431\u0435\u0437 \u043f\u0438\u0442\u0430\u043d\u044c",
                "\u043f\u0440\u043e\u0441\u0442\u043e \u043f\u043e\u0433\u043e\u0434\u0436", "\u0437\u0440\u043e\u0431\u0438 \u0432\u0441\u0435 \u0456\u0434\u0435\u0430\u043b\u044c\u043d\u043e");

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

        private static string NullDash(string? value)
            => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

        private static string Trim(string? text, int max)
        {
            text = (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max].TrimEnd() + "...";
        }

        private sealed class PlannerSomaticFrame
        {
            public string StressState { get; set; } = "unknown";
            public double StressScore { get; set; }
            public bool HighStress { get; set; }
            public bool Fatigued { get; set; }
            public string Reason { get; set; } = "";
            public string Summary { get; set; } = "";
            public string Short { get; set; } = "";
        }
    }
}
