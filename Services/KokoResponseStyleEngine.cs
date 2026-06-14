namespace KokonoeAssistant.Services
{
    using System.Globalization;

    public static class KokoResponseStyleEngine
    {
        public static string BuildEmotionLengthDirective(KokoEmotionEngine.EmotionState emotion)
            => emotion switch
            {
                KokoEmotionEngine.EmotionState.Irritated =>
                    "EMOTIONAL LENGTH: irritated. Keep the reply short: 1-2 sharp sentences, no lecture, no long diagnostic monologue.",
                KokoEmotionEngine.EmotionState.Distant =>
                    "EMOTIONAL LENGTH: distant. Keep the reply compact and factual: 1-3 sentences.",
                KokoEmotionEngine.EmotionState.Curious =>
                    "EMOTIONAL LENGTH: curious. You may write 2-5 sentences, include one specific inference or question if it advances the task.",
                KokoEmotionEngine.EmotionState.Excited =>
                    "EMOTIONAL LENGTH: excited. Medium reply allowed, but stay concrete and do not ramble.",
                _ =>
                    "EMOTIONAL LENGTH: stable. Prefer concise, concrete replies; expand only when the task needs it."
            };

        public static string BuildTemperamentDirective(KokoInternalState state)
        {
            var mood = string.IsNullOrWhiteSpace(state.PersonaTemperamentState)
                ? "standard_cranky"
                : state.PersonaTemperamentState;

            var voice = KokoTemperamentEngine.BuildVoiceDirective(mood);
            return $"TEMPERAMENT LENGTH: {mood}. {voice} Energy={state.PersonaEnergyLevel.ToString("F2", CultureInfo.InvariantCulture)}; patience={state.PersonaPatienceLevel.ToString("F2", CultureInfo.InvariantCulture)}.";
        }
    }
}
