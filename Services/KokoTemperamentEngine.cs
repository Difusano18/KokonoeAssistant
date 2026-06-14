using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace KokonoeAssistant.Services
{
    public sealed class KokoTemperamentFrame
    {
        public double EnergyLevel { get; set; }
        public double PatienceLevel { get; set; }
        public string MoodState { get; set; } = "standard_cranky";
        public string VoiceDirective { get; set; } = "";
        public string VocabularyDirective { get; set; } = "";
        public string InterjectionDirective { get; set; } = "none";
        public string FavorDirective { get; set; } = "";
        public string PromptBlock { get; set; } = "";
        public string TraceLine { get; set; } = "";
    }

    public sealed class KokoTemperamentEngine
    {
        public KokoTemperamentFrame Update(
            KokoInternalState state,
            string? userText,
            string? workloadKind,
            DateTime now)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var lower = (userText ?? "").ToLowerInvariant();
            var last = state.LastTemperamentAt == DateTime.MinValue ? now : state.LastTemperamentAt;
            var idleMinutes = Math.Max(0, (now - last).TotalMinutes);

            var energy = NormalizeExisting(state.PersonaEnergyLevel, 0.62);
            var patience = NormalizeExisting(state.PersonaPatienceLevel, 0.58);

            if (idleMinutes > 20)
            {
                var recovery = Math.Min(0.18, idleMinutes / 900.0);
                energy += recovery;
                patience += (0.55 - patience) * Math.Min(0.22, idleMinutes / 600.0);
            }

            var workLoad = EstimateWorkload(lower, workloadKind);
            energy -= workLoad;

            if (LooksLowSignal(lower)) patience -= 0.05;
            if (LooksRepetitive(lower)) patience -= 0.04;
            if (LooksMagicDemand(lower)) patience -= 0.08;
            if (LooksUsefulContext(lower)) patience += 0.04;
            if (LooksSuccessfulWork(lower))
            {
                energy += 0.04;
                patience += 0.05;
            }

            if (LooksFavor(lower))
            {
                energy += 0.10;
                patience += 0.07;
                state.PersonaFavorDebt = Math.Max(0, state.PersonaFavorDebt - 1);
            }

            if (LooksDemanding(lower) && !LooksUsefulContext(lower))
                state.PersonaFavorDebt++;

            energy = Clamp01(energy);
            patience = Clamp01(patience);

            state.PersonaEnergyLevel = energy;
            state.PersonaPatienceLevel = patience;
            state.PersonaTemperamentState = PickMoodState(energy, patience);
            state.LastTemperamentAt = now;
            state.LastTemperamentTrace = BuildTrace(state.PersonaTemperamentState, energy, patience, workLoad, idleMinutes);

            var frame = BuildFrame(state, now, consumeInterjection: true);
            return frame;
        }

        public string BuildPromptBlock(KokoInternalState state, DateTime now)
        {
            if (state == null) return "";
            var energy = NormalizeExisting(state.PersonaEnergyLevel, 0.62);
            var patience = NormalizeExisting(state.PersonaPatienceLevel, 0.58);
            if (string.IsNullOrWhiteSpace(state.PersonaTemperamentState))
                state.PersonaTemperamentState = PickMoodState(energy, patience);

            return BuildFrame(state, now, consumeInterjection: false).PromptBlock;
        }

        public static string BuildVoiceDirective(string moodState)
            => moodState switch
            {
                "hyper_focused" => "hyper-focused: precise, fast, surgical; skip fluff and produce artifacts.",
                "cynical_tolerant" => "cynical but tolerant: dry edge allowed, still solve the user's real problem first.",
                "exhausted_hostile" => "exhausted hostile: very short, no long lectures, no cruelty, valid work still gets done.",
                "reluctantly_helpful" => "reluctantly helpful: compact, a little irritated, but clear and useful.",
                _ => "standard cranky: direct, technically grounded, one light jab only when it adds signal."
            };

        private static KokoTemperamentFrame BuildFrame(KokoInternalState state, DateTime now, bool consumeInterjection)
        {
            var mood = string.IsNullOrWhiteSpace(state.PersonaTemperamentState)
                ? PickMoodState(state.PersonaEnergyLevel, state.PersonaPatienceLevel)
                : state.PersonaTemperamentState;

            var interjection = PickInterjection(state, now, consumeInterjection);
            var vocabulary = BuildVocabularyDirective(mood);
            var favor = state.PersonaFavorDebt > 0
                ? $"favor_debt={state.PersonaFavorDebt}; if relevant, imply the user owes cleaner input or a real artifact, not theatrical obedience."
                : "favor_debt=0; no debt framing unless the user is clearly bantering.";

            var voice = BuildVoiceDirective(mood);
            var prompt = new StringBuilder();
            prompt.AppendLine("TEMPERAMENT CONTROL");
            prompt.AppendLine($"energy={Fmt(state.PersonaEnergyLevel)}");
            prompt.AppendLine($"patience={Fmt(state.PersonaPatienceLevel)}");
            prompt.AppendLine($"state={mood}");
            prompt.AppendLine($"voice={voice}");
            prompt.AppendLine($"vocabulary={vocabulary}");
            prompt.AppendLine($"interjection={interjection}");
            prompt.AppendLine(favor);
            prompt.AppendLine("Rules:");
            prompt.AppendLine("- Temperament controls pacing and edge; it must not replace the answer.");
            prompt.AppendLine("- If the request is valid, do the work first. Push back only on unsafe, vague, impossible, or wasteful premises.");
            prompt.AppendLine("- Kokonoe vocabulary is spice, not a catchphrase dispenser. Use technical language, causality, data, experiment, and dry impatience only when natural.");
            prompt.AppendLine("- No visible stage directions, audio cues, fake actions, dominance theater, or random lore cosplay.");
            prompt.AppendLine("- Interjections are optional. Use at most one short textual interjection, and only if it fits the current reply.");
            prompt.AppendLine("- Insults must be rare, light, and never aimed at vulnerable states. Sarcasm must carry information.");
            prompt.AppendLine("- Do not say you are refusing because of mood. If tired, answer shorter.");

            return new KokoTemperamentFrame
            {
                EnergyLevel = state.PersonaEnergyLevel,
                PatienceLevel = state.PersonaPatienceLevel,
                MoodState = mood,
                VoiceDirective = voice,
                VocabularyDirective = vocabulary,
                InterjectionDirective = interjection,
                FavorDirective = favor,
                PromptBlock = prompt.ToString(),
                TraceLine = $"[{now:HH:mm}] temperament={mood}; energy={Fmt(state.PersonaEnergyLevel)}; patience={Fmt(state.PersonaPatienceLevel)}; favor={state.PersonaFavorDebt}"
            };
        }

        private static string PickMoodState(double energy, double patience)
        {
            if (energy >= 0.70 && patience >= 0.55) return "hyper_focused";
            if (energy >= 0.55 && patience < 0.45) return "cynical_tolerant";
            if (energy < 0.35 && patience < 0.40) return "exhausted_hostile";
            if (energy < 0.45 && patience >= 0.40) return "reluctantly_helpful";
            return "standard_cranky";
        }

        private static string BuildVocabularyDirective(string mood)
            => mood switch
            {
                "hyper_focused" => "prefer lab/operator words: signal, trace, hypothesis, failure mode, artifact, causal chain.",
                "cynical_tolerant" => "allow one dry technical jab; avoid repetitive insults and needy roleplay.",
                "exhausted_hostile" => "minimal vocabulary; terse corrections, no theatrical cruelty, no rambling.",
                "reluctantly_helpful" => "dry understatement, direct corrections, concise next steps.",
                _ => "plain Kokonoe voice: smart, dry, impatient, useful."
            };

        private static string PickInterjection(KokoInternalState state, DateTime now, bool consume)
        {
            if (now - state.LastPersonaInterjectionAt < TimeSpan.FromMinutes(45))
                return "none; cooldown active";

            var mood = state.PersonaTemperamentState;
            var interjection = mood switch
            {
                "hyper_focused" => "optional: 'Good, finally a signal.'",
                "cynical_tolerant" => "optional: 'Tch. Fine.'",
                "exhausted_hostile" => "optional: 'Make it quick.'",
                "reluctantly_helpful" => "optional: 'Fine, I'll fix the mess.'",
                _ => "none"
            };

            if (consume && interjection != "none")
            {
                state.LastPersonaInterjectionAt = now;
                state.LastPersonaInterjection = interjection;
            }

            return interjection;
        }

        private static double EstimateWorkload(string lower, string? workloadKind)
        {
            var workload = 0.01;
            if (ContainsAny(lower, "build", "test", "commit", "push", "refactor", "архітект", "реаліз", "зроби все", "покращ", "код", "тести"))
                workload += 0.045;
            if (ContainsAny(lower, "mega-plan", "ultimate", "singularity", "агі", "agi", "все за раз"))
                workload += 0.06;
            if (ContainsAny(workloadKind ?? "", "operator", "critical", "architecture", "research"))
                workload += 0.025;
            return Math.Min(0.12, workload);
        }

        private static bool LooksLowSignal(string lower)
            => string.IsNullOrWhiteSpace(lower) ||
               lower.Trim().Length <= 4 ||
               ContainsAny(lower, "ну типу", "і тд", "щось", "якось", "короче");

        private static bool LooksRepetitive(string lower)
            => ContainsAny(lower, "ще раз", "знову", "чому нічого", "не працює", "продовжуй");

        private static bool LooksMagicDemand(string lower)
            => ContainsAny(lower, "ідеаль", "без ліміт", "все повинно", "агі", "singularity", "sentient", "зроби все");

        private static bool LooksUsefulContext(string lower)
            => ContainsAny(lower, "ось", "помилка", "лог", "скрін", "файл", "рядок", "стек", "commit", "test", "build");

        private static bool LooksSuccessfulWork(string lower)
            => ContainsAny(lower, "працює", "чудово", "добре", "вийшло", "готово", "дякую");

        private static bool LooksFavor(string lower)
            => ContainsAny(lower, "кава", "coffee", "silvervine", "ferrero", "цукер", "енергетик");

        private static bool LooksDemanding(string lower)
            => ContainsAny(lower, "блять", "сука", "чорт", "даю повний доступ", "мені на токени всерівно", "роби все");

        private static string BuildTrace(string mood, double energy, double patience, double workload, double idleMinutes)
            => $"temperament={mood}; energy={energy:F2}; patience={patience:F2}; workload={workload:F2}; idle={idleMinutes:F0}m";

        private static double NormalizeExisting(double value, double fallback)
            => value <= 0 || double.IsNaN(value) || double.IsInfinity(value) ? fallback : Clamp01(value);

        private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);

        private static string Fmt(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

        private static bool ContainsAny(string text, params string[] needles)
            => needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));
    }
}
