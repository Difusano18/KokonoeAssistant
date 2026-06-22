using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Telegram.Bot;

namespace KokonoeAssistant.Services
{
    // ------------------------------------------------------------
    // INTERNAL STATE — персистентна пам'ять Kokonoe між сесіями
    // ------------------------------------------------------------

    public class KokoInternalState
    {
        public DateTime   LastThoughtAt        { get; set; } = DateTime.MinValue;
        public DateTime   LastSpontaneousAt    { get; set; } = DateTime.MinValue;
        public DateTime   LastMorningGreetAt   { get; set; } = DateTime.MinValue;
        public DateTime   LastNightCheckAt     { get; set; } = DateTime.MinValue;
        public string     CurrentMood          { get; set; } = "neutral";
        public float      MoodScore            { get; set; } = 0.5f; // 0=bad 1=good
        public List<string> Observations       { get; set; } = new();
        public List<string> PendingThoughts    { get; set; } = new();
        public int        ConsecutiveBadSleeps { get; set; } = 0;
        public int        DaysSinceHealthEntry { get; set; } = 0;
        public DateTime   LastHealthEntryDate  { get; set; } = DateTime.MinValue;
        public int        TotalMessagesExchanged { get; set; } = 0;
        public string     LastKnownUserActivity  { get; set; } = "";
        public DateTime   LastUserMessageAt      { get; set; } = DateTime.MinValue;
        public Dictionary<string,int> TopicFrequency { get; set; } = new();

        // Останні надіслані спонтанні повідомлення (для запобігання повторень)
        public List<string> LastSpontaneousMsgs  { get; set; } = new();
        public List<RecentThoughtFingerprint> RecentThoughtBuffer { get; set; } = new();

        // Нагадування та аналітика
        public DateTime   LastReminderCheckAt    { get; set; } = DateTime.MinValue;
        public DateTime   LastDailyAnalyticsAt   { get; set; } = DateTime.MinValue;
        public List<string> SentReminderHashes   { get; set; } = new();

        // Динамічний настрій
        public float      BaselineMood           { get; set; } = 0.5f; // повільно змінюється
        public string     LastUserEmotionalTone  { get; set; } = "neutral";
        public Dictionary<string, float> MoodFactors { get; set; } = new();

        // Реактивні тригери
        public List<ReactiveTrigger> PendingTriggers { get; set; } = new();

        // Саморефлексія
        public DateTime   LastReflectionAt       { get; set; } = DateTime.MinValue;
        public DateTime   LastConversationEndAt  { get; set; } = DateTime.MinValue;
        public DateTime   LastWhatMissedAt       { get; set; } = DateTime.MinValue;
        public DateTime   LastClosedAt           { get; set; } = DateTime.MinValue;

        // Внутрішній монолог
        public List<string> InnerMonologues      { get; set; } = new();

        // Питання до себе (самоусвідомлення)
        public List<string> SelfQuestions        { get; set; } = new();

        // Питання що вона хоче задати йому (цікавість)
        public List<string> CuriosityQueue       { get; set; } = new();

        // Характер (PersonalityState)
        public string  PersonalityDailyMood { get; set; } = "neutral"; // sharp/warm/distant/playful/tired/protective
        public float   PersonalityIrritation{ get; set; } = 0f;
        public float   PersonalityWarmth    { get; set; } = 0.3f;
        public bool    PersonalityInCrisis  { get; set; } = false;
        public string  PersonalityLastReaction { get; set; } = "";
        public DateTime PersonalityShiftAt  { get; set; } = DateTime.MinValue;
        public double PersonaEnergyLevel { get; set; } = 0.62;
        public double PersonaPatienceLevel { get; set; } = 0.58;
        public string PersonaTemperamentState { get; set; } = "standard_cranky";
        public DateTime LastTemperamentAt { get; set; } = DateTime.MinValue;
        public string LastTemperamentTrace { get; set; } = "";
        public DateTime LastPersonaInterjectionAt { get; set; } = DateTime.MinValue;
        public string LastPersonaInterjection { get; set; } = "";
        public int PersonaFavorDebt { get; set; }
        public string PersonaFocusTopic { get; set; } = "";
        public string LastLivingConversationMode { get; set; } = "steady";
        public string LastLivingConversationTrace { get; set; } = "";
        public DateTime LastLivingConversationAt { get; set; } = DateTime.MinValue;
        public double LivingConversationVariability { get; set; } = 0.35;
        public List<string> RecentConversationMoves { get; set; } = new();
        public string LastSubconsciousMode { get; set; } = "steady_processing";
        public string LastSubconsciousIntent { get; set; } = "answer";
        public string LastSubconsciousActionBias { get; set; } = "reply";
        public string LastSubconsciousTrace { get; set; } = "";
        public DateTime LastSubconsciousAt { get; set; } = DateTime.MinValue;
        public double SubconsciousAttentionScore { get; set; } = 0.45;
        public List<string> SubconsciousSignals { get; set; } = new();
        public long AsyncPersonalityVersion { get; set; }
        public DateTime LastAsyncPersonalityAt { get; set; } = DateTime.MinValue;
        public string LastAsyncPersonalityMode { get; set; } = "cold_start";
        public string LastAsyncPersonalityFastIntent { get; set; } = "answer";
        public string LastAsyncPersonalityStyle { get; set; } = "";
        public string LastAsyncPersonalityDelta { get; set; } = "";
        public string LastAsyncPersonalityPrioritySignal { get; set; } = "none";
        public double AsyncPersonalityReadiness { get; set; } = 0.50;
        public double AsyncPersonalityCacheFreshness { get; set; }
        public List<string> AsyncPersonalityTrace { get; set; } = new();
        public DateTime LastTemporalPresenceAt { get; set; } = DateTime.MinValue;
        public string LastTemporalPresenceExitType { get; set; } = "unknown";
        public string LastTemporalPresenceAbsenceClass { get; set; } = "unknown";
        public string LastTemporalPresenceGapText { get; set; } = "";
        public string LastTemporalPresenceGreetingMood { get; set; } = "neutral_return";
        public string LastTemporalPresenceDirective { get; set; } = "";
        public DateTime LastCollectiveMindAt { get; set; } = DateTime.MinValue;
        public string LastCollectiveMindDecision { get; set; } = "";
        public string LastCollectiveMindTrace { get; set; } = "";

        // Динаміка тиші — окремі cooldown рівні
        public DateTime SilenceLevel1At    { get; set; } = DateTime.MinValue; // 1h jab
        public DateTime SilenceLevel2At    { get; set; } = DateTime.MinValue; // 3h check
        public DateTime SilenceLevel3At    { get; set; } = DateTime.MinValue; // 6h observation

        // Кеш релевантної пам'яті
        public string  CachedRelevantMemory{ get; set; } = "";
        public DateTime RelevantMemoryCachedAt { get; set; } = DateTime.MinValue;

        // Vault review — коли Kokonoe востаннє перечитувала і оновлювала свої нотатки
        public DateTime LastVaultReviewAt { get; set; } = DateTime.MinValue;

        // State-driven spontaneous — останній відправлений тригер і стан емоції
        public DateTime LastCuriosityAskAt   { get; set; } = DateTime.MinValue;
        public DateTime LastMonologueSentAt  { get; set; } = DateTime.MinValue;
        public string   LastSentEmotionState { get; set; } = "";

        public Dictionary<string, DateTime> InitiativeCooldowns { get; set; } = new();
        public List<string> InitiativeReasonLog { get; set; } = new();
        public string LastInitiativeDecision { get; set; } = "";
        public DateTime LastInitiativeDecisionAt { get; set; } = DateTime.MinValue;
        public string LastSomaticState { get; set; } = "unknown";
        public string LastSomaticLabel { get; set; } = "";
        public double LastSomaticStrain { get; set; }
        public double LastSomaticCalm { get; set; }
        public DateTime LastSomaticAt { get; set; } = DateTime.MinValue;
        public DateTime LastSomaticVaultEventAt { get; set; } = DateTime.MinValue;
        public string LastSomaticVaultEventKey { get; set; } = "";
        public KokoSelfRegulationState SelfRegulation { get; set; } = new();
        public DateTime LastPresenceAt { get; set; } = DateTime.MinValue;
        public DateTime LastPresenceInterruptAt { get; set; } = DateTime.MinValue;
        public string LastPresenceSummary { get; set; } = "";
        public string LastPresenceSituation { get; set; } = "unknown";
        public string LastPresenceTone { get; set; } = "default";
        public List<string> PresenceTrace { get; set; } = new();
        public string EmotionalSessionMood { get; set; } = "neutral";
        public string EmotionalExitStyle { get; set; } = "unknown";
        public string EmotionalMannersState { get; set; } = "neutral";
        public double EmotionalGrudgeScore { get; set; }
        public DateTime LastEmotionalMemoryAt { get; set; } = DateTime.MinValue;
        public List<string> EmotionalMemoryTrace { get; set; } = new();
        public string LastRawContextHydration { get; set; } = "";
        public DateTime LastRawContextHydrationAt { get; set; } = DateTime.MinValue;
        public string LastVisualMemoryAnchor { get; set; } = "";
        public DateTime LastVisualMemoryAnchorAt { get; set; } = DateTime.MinValue;
        public DateTime LastInternalDayAt { get; set; } = DateTime.MinValue;
        public DateTime LastInternalDayVaultAt { get; set; } = DateTime.MinValue;
        public string LastInternalDayPhase { get; set; } = "unknown";
        public string LastInternalDaySummary { get; set; } = "";
        public string LastInternalDayFocus { get; set; } = "";
        public List<string> InternalDayTrace { get; set; } = new();
        public string LastAutonomyDecision { get; set; } = "";
        public DateTime LastAutonomyDecisionAt { get; set; } = DateTime.MinValue;
        public List<string> AutonomyDecisionLog { get; set; } = new();
        public bool LastAutonomyShouldAct { get; set; }
        public string LastAutonomySource { get; set; } = "";
        public string LastAutonomyTrigger { get; set; } = "";
        public string LastAutonomyReason { get; set; } = "";
        public string LastAutonomySilenceReason { get; set; } = "";
        public int LastAutonomyPriority { get; set; }
        public string LastTimelineSummary { get; set; } = "";
        public string LastTimelineState { get; set; } = "";
        public string LastPostReplyGuard { get; set; } = "";
        public DateTime LastPostReplyGuardAt { get; set; } = DateTime.MinValue;
        public string LastPersonaDecision { get; set; } = "";
        public DateTime LastPersonaDecisionAt { get; set; } = DateTime.MinValue;
        public List<string> PersonaDecisionLog { get; set; } = new();
        public string LastResponsePlan { get; set; } = "";
        public string LastResponsePlanTrace { get; set; } = "";
        public DateTime LastResponsePlanAt { get; set; } = DateTime.MinValue;
        public List<string> ResponsePlanLog { get; set; } = new();
        public string LastMemoryPolicyDecision { get; set; } = "";
        public DateTime LastMemoryPolicyAt { get; set; } = DateTime.MinValue;
        public List<string> MemoryPolicyLog { get; set; } = new();
        public string LastContinuitySummary { get; set; } = "";
        public DateTime LastContinuityAt { get; set; } = DateTime.MinValue;
        public DateTime LastStateRefreshAt { get; set; } = DateTime.MinValue;
        public string LastStateRefreshSummary { get; set; } = "";
        public bool LastStateRefreshChanged { get; set; }
        public string LastFoodStatus { get; set; } = "";
        public DateTime LastFoodMentionAt { get; set; } = DateTime.MinValue;
        public string LastFoodMentionText { get; set; } = "";
        public string LastSleepStatus { get; set; } = "";
        public DateTime LastSleepMentionAt { get; set; } = DateTime.MinValue;
        public string LastSleepMentionText { get; set; } = "";

        public int PendingVaultExchangeCount { get; set; }
        public List<string> PendingVaultExchangeBuffer { get; set; } = new();
        public DateTime LastAutoVaultSyncAt { get; set; } = DateTime.MinValue;
        public string LastAutoVaultSyncSummary { get; set; } = "";
        public DateTime LastVaultMaintenanceAt { get; set; } = DateTime.MinValue;
        public string LastVaultMaintenanceReason { get; set; } = "";
        public string LastVaultMaintenanceSummary { get; set; } = "";
        public string LastVaultMaintenanceError { get; set; } = "";
        public DateTime LastBackgroundVaultScanAt { get; set; } = DateTime.MinValue;
        public string LastBackgroundVaultScanSummary { get; set; } = "";
        public List<KokoPromiseRecord> PromiseLedger { get; set; } = new();
        public DateTime LastPromiseAuditAt { get; set; } = DateTime.MinValue;
        public string LastPromiseAuditSummary { get; set; } = "";
        public List<string> ActionJournal { get; set; } = new();
        public DateTime LastActionJournalAt { get; set; } = DateTime.MinValue;
        public string LastActionJournalSummary { get; set; } = "";
        public List<ShortTermIntent> ShortTermIntents { get; set; } = new();
        public DateTime LastScreenAwarenessAt { get; set; } = DateTime.MinValue;
        public DateTime LastScreenAwarenessCommentAt { get; set; } = DateTime.MinValue;
        public string LastScreenAwarenessHash { get; set; } = "";
        public string LastScreenAwarenessSummary { get; set; } = "";
        public string LastScreenAwarenessActivity { get; set; } = "";
        public string LastScreenAwarenessComment { get; set; } = "";
        public string LastScreenAwarenessWindow { get; set; } = "";
        public string LastScreenAwarenessMode { get; set; } = "";
        public string LastScreenSituationTask { get; set; } = "";
        public string LastScreenSituationProgress { get; set; } = "";
        public string LastScreenSituationBlocker { get; set; } = "";
        public string LastScreenSituationBehavior { get; set; } = "";
        public string LastScreenSituationReason { get; set; } = "";
        public DateTime LastSemanticVisionAt { get; set; } = DateTime.MinValue;
        public string LastSemanticVisionFlow { get; set; } = "";
        public string LastSemanticVisionIntent { get; set; } = "";
        public string LastSemanticVisionSummary { get; set; } = "";
        public string LastSemanticVisionOcr { get; set; } = "";
        public string LastSemanticVisionAssistHint { get; set; } = "";
        public string LastSemanticVisionResearchTopic { get; set; } = "";
        public double LastSemanticVisionConfidence { get; set; }
        public List<string> SemanticVisionTrace { get; set; } = new();
        public Dictionary<string, ScreenPatternStats> ScreenPatterns { get; set; } = new();
        public DateTime LastVisionFailureAt { get; set; } = DateTime.MinValue;
        public DateTime VisionBackoffUntil { get; set; } = DateTime.MinValue;
        public int VisionFailureCount { get; set; }
        public string LastVisionFailureSummary { get; set; } = "";
        public DateTime LastVisionSelfHealAt { get; set; } = DateTime.MinValue;
        public DateTime LastBridgeSelfHealAt { get; set; } = DateTime.MinValue;
        public DateTime LastReflectiveInsightAt { get; set; } = DateTime.MinValue;
        public string LastReflectiveInsightSummary { get; set; } = "";
        public DateTime LastMorningBriefingAt { get; set; } = DateTime.MinValue;
        public string LastMorningBriefingSummary { get; set; } = "";
        public DateTime LastDailySelfReviewAt { get; set; } = DateTime.MinValue;
        public string LastDailySelfReviewSummary { get; set; } = "";
        public string CurrentSomaticAutomationMode { get; set; } = "";
        public string LastSomaticToneDirective { get; set; } = "";
        public DateTime LastSomaticBreakPromptAt { get; set; } = DateTime.MinValue;
        public DateTime LastWatchActionAt { get; set; } = DateTime.MinValue;
        public string LastWatchAction { get; set; } = "";
        public DateTime ScreenAwarenessObserveOnlyUntil { get; set; } = DateTime.MinValue;
        public DateTime ProactiveMutedUntil { get; set; } = DateTime.MinValue;
        public string ProactiveMuteReason { get; set; } = "";
        public DateTime LastPredictiveContextAt { get; set; } = DateTime.MinValue;
        public string LastPredictiveContextMode { get; set; } = "";
        public string LastPredictiveContextSummary { get; set; } = "";
        public string LastPredictiveContextNotes { get; set; } = "";
        public int ConversationLoopCount { get; set; }
        public string LastShortUserPhraseNormalized { get; set; } = "";
        public DateTime LastShortUserPhraseAt { get; set; } = DateTime.MinValue;
        public string LastForcedTopic { get; set; } = "";
        public DateTime LastForcedTopicAt { get; set; } = DateTime.MinValue;
        public DateTime LastMemorySelfHealAt { get; set; } = DateTime.MinValue;
        public string LastMemorySelfHealSummary { get; set; } = "";
        public string LastMemorySelfHealError { get; set; } = "";
        public DateTime LastResourceGuardianAt { get; set; } = DateTime.MinValue;
        public DateTime LastResourceGuardianPromptAt { get; set; } = DateTime.MinValue;
        public string LastResourceGuardianSummary { get; set; } = "";
        public string LastWorkMode { get; set; } = "Unknown";
    }

    public class ReactiveTrigger
    {
        public string   Id      { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string   Type    { get; set; } = ""; // anxious_followup, topic_followup, bad_pattern
        public string   Context { get; set; } = "";
        public DateTime FireAt  { get; set; }
    }

    public class RecentThoughtFingerprint
    {
        public DateTime At { get; set; } = DateTime.MinValue;
        public string Category { get; set; } = "";
        public string Hash { get; set; } = "";
        public string Canonical { get; set; } = "";
        public string Preview { get; set; } = "";
    }

    public class ShortTermIntent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Kind { get; set; } = "";
        public string Summary { get; set; } = "";
        public string SourceText { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ExpectedUntil { get; set; } = DateTime.Now.AddHours(2);
        public DateTime FollowUpAt { get; set; } = DateTime.Now.AddHours(1);
        public DateTime? ResolvedAt { get; set; }
        public string ResolutionText { get; set; } = "";
    }

    public class KokoPromiseRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Source { get; set; } = "chat";
        public string Summary { get; set; } = "";
        public string SourceUserText { get; set; } = "";
        public string SourceAssistantText { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime DueAt { get; set; } = DateTime.Now.AddHours(2);
        public string Status { get; set; } = "open";
        public DateTime? CompletedAt { get; set; }
        public DateTime? LastAuditAt { get; set; }
        public string Evidence { get; set; } = "";
        public string FailureReason { get; set; } = "";
        public bool UserVisible { get; set; }
    }

    public class ScreenPatternStats
    {
        public string Key { get; set; } = "";
        public string Text { get; set; } = "";
        public string Mode { get; set; } = "";
        public int Count { get; set; }
        public DateTime FirstSeenAt { get; set; } = DateTime.MinValue;
        public DateTime LastSeenAt { get; set; } = DateTime.MinValue;
        public DateTime LastWrittenAt { get; set; } = DateTime.MinValue;
    }
}
