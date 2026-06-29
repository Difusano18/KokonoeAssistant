using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using KokonoeAssistant.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Tests;

internal static class Program
{
    private static int _passed;
    private static int _selected;
    private static string? _filter;

    private static int Main(string[] args)
    {
        try
        {
            var filterIndex = Array.FindIndex(args, arg => arg.Equals("--filter", StringComparison.OrdinalIgnoreCase));
            if (filterIndex >= 0 && filterIndex + 1 < args.Length)
                _filter = args[filterIndex + 1];

            Run("Somatic wired from pulse spike", SomaticWiredFromPulseSpike);
            Run("Somatic tired from low charge", SomaticTiredFromLowCharge);
            Run("Wearable telemetry infers likely sleep", WearableTelemetryInfersLikelySleep);
            Run("Wearable telemetry infers stress initiative", WearableTelemetryInfersStressInitiative);
            Run("Wearable telemetry raises stress spike event", WearableTelemetryRaisesStressSpikeEvent);
            Run("Wearable telemetry ignores duplicate sample ids", WearableTelemetryIgnoresDuplicateSampleIds);
            Run("Wearable telemetry reloads recent sample ids", WearableTelemetryReloadsRecentSampleIds);
            Run("Wearable telemetry marks diagnostic trust state", WearableTelemetryMarksDiagnosticTrustState);
            Run("Heart engine prefers fresh wearable bpm", HeartEnginePrefersFreshWearableBpm);
            Run("Wearable bridge ingests authorized sample", WearableBridgeIngestsAuthorizedSample);
            Run("Wearable bridge rejects unpaired sample", WearableBridgeRejectsUnpairedSample);
            Run("Wearable bridge ingests authorized sample batch", WearableBridgeIngestsAuthorizedSampleBatch);
            Run("Wearable bridge dedupes replayed sample batch", WearableBridgeDedupesReplayedSampleBatch);
            Run("Wearable bridge status exposes pairing metadata", WearableBridgeStatusExposesPairingMetadata);
            Run("Wearable bridge exposes external fallback URLs", WearableBridgeExposesExternalFallbackUrls);
            Run("Wearable bridge diagnostics track traffic", WearableBridgeDiagnosticsTrackTraffic);
            Run("Wearable bridge emulates dual real-time pulse clients", WearableBridgeEmulatesDualRealtimePulseClients);
            Run("Wearable bridge pairs and rejects bad tokens", WearableBridgePairsAndRejectsBadTokens);
            Run("Wearable bridge keeps stable token and pc id", WearableBridgeKeepsStableTokenAndPcId);
            Run("Wearable bridge restores paired device id", WearableBridgeRestoresPairedDeviceId);
            Run("Wearable bridge migrates paired device from telemetry", WearableBridgeMigratesPairedDeviceFromTelemetry);
            Run("Wearable trust rejects unpaired fresh samples", WearableTrustRejectsUnpairedFreshSamples);
            Run("Wearable bridge accepts watch action endpoint", WearableBridgeAcceptsWatchActionEndpoint);
            Run("Heartbeat dashboard writes markdown and html", HeartbeatDashboardWritesMarkdownAndHtml);
            Run("Semantic cache returns similar cached answer", SemanticCacheReturnsSimilarCachedAnswer);
            Run("Capability manifest advertises runtime routes", CapabilityManifestAdvertisesRuntimeRoutes);
            Run("Service container DI preserves core singleton identity", ServiceContainerDiPreservesCoreSingletonIdentity);
            Run("Tool gateway verifies file writes and directories", ToolGatewayVerifiesFileWritesAndDirectories);
            Run("Tool gateway exposes confirmation and failures", ToolGatewayExposesConfirmationAndFailures);
            Run("Tool gateway stops execution plan after failure", ToolGatewayStopsExecutionPlanAfterFailure);
            Run("Tool registry exposes capability scoped manifest", ToolRegistryExposesCapabilityScopedManifest);
            Run("CodeAct policy blocks host access", CodeActPolicyBlocksHostAccess);
            Run("CodeAct tool executes restricted Python", CodeActToolExecutesRestrictedPython);
            Run("CodeAct tool preserves syntax failure evidence", CodeActToolPreservesSyntaxFailureEvidence);
            Run("Self regulation clamps pulse spike", SelfRegulationClampsPulseSpike);
            Run("Self regulation protects vulnerable tone", SelfRegulationProtectsVulnerableTone);
            Run("Initiative respects low-power silence", InitiativeRespectsLowPowerSilence);
            Run("Initiative reacts to protective override", InitiativeReactsToProtectiveOverride);
            Run("Initiative high autonomy asks curiosity sooner", InitiativeHighAutonomyAsksCuriositySooner);
            Run("Short term intent followup bypasses ordinary initiative", ShortTermIntentFollowupBypassesOrdinaryInitiative);
            Run("Presence detects overdue course followup", PresenceDetectsOverdueCourseFollowup);
            Run("Presence waits for return home intent", PresenceWaitsForReturnHomeIntent);
            Run("Presence treats active screen idle input as present", PresenceTreatsActiveScreenIdleInputAsPresent);
            Run("Presence active screen idle can interrupt after ten minutes", PresenceActiveScreenIdleCanInterruptAfterTenMinutes);
            Run("Presence treats unchanged screen idle input as away", PresenceTreatsUnchangedScreenIdleInputAsAway);
            Run("Presence treats stuck coding idle input as present", PresenceTreatsStuckCodingIdleInputAsPresent);
            Run("Presence treats active input chat silence as present", PresenceTreatsActiveInputChatSilenceAsPresent);
            Run("Autonomy blocks generic ping during active intent", AutonomyBlocksGenericPingDuringActiveIntent);
            Run("Presence refuses stale sleep instruction after return", PresenceRefusesStaleSleepInstructionAfterReturn);
            Run("Presence never interrupts active sleep intent", PresenceNeverInterruptsActiveSleepIntent);
            Run("Sleep intent at night resolves to morning", SleepIntentAtNightResolvesToMorning);
            Run("State freshness protects stale sleep intent", StateFreshnessProtectsStaleSleepIntent);
            Run("State freshness keeps sleep through passive desktop", StateFreshnessKeepsSleepThroughPassiveDesktop);
            Run("State freshness closes intent on wake signal", StateFreshnessClosesIntentOnWakeSignal);
            Run("State freshness closes stale course on later activity", StateFreshnessClosesStaleCourseOnLaterActivity);
            Run("Vault sync policy flushes stale partial batch", VaultSyncPolicyFlushesStalePartialBatch);
            Run("Presence long silence can interrupt on high autonomy", PresenceLongSilenceCanInterruptOnHighAutonomy);
            Run("Presence long silence summary is readable Ukrainian", PresenceLongSilenceSummaryIsReadableUkrainian);
            Run("Presence long silence with PC away does not interrupt", PresenceLongSilenceWithPcAwayDoesNotInterrupt);
            Run("Internal day shifts phase and writes vault status", InternalDayShiftsPhaseAndWritesVaultStatus);
            Run("Internal day prefers silence at low power night", InternalDayPrefersSilenceAtLowPowerNight);
            Run("Autonomy pipeline gates weak initiative in quiet night", AutonomyPipelineGatesWeakInitiativeInQuietNight);
            Run("Relationship records shift events", RelationshipRecordsShiftEvents);
            Run("Pattern rhythm profile recommends quiet slot", PatternRhythmProfileRecommendsQuietSlot);
            Run("Self review blocks stale sleep replies", SelfReviewBlocksStaleSleepReplies);
            Run("Timeline summarizes returned state", TimelineSummarizesReturnedState);
            Run("Timeline uses emotional time", TimelineUsesEmotionalTime);
            Run("Post reply guard blocks stale sleep", PostReplyGuardBlocksStaleSleep);
            Run("Post reply guard blocks stale food claim after ate", PostReplyGuardBlocksStaleFoodClaimAfterAte);
            Run("Post reply guard blocks hibernation framing after slept", PostReplyGuardBlocksHibernationFramingAfterSlept);
            Run("Post reply guard rejects staged decoration", PostReplyGuardRejectsStagedDecoration);
            Run("Post reply guard blocks duplicate replies", PostReplyGuardBlocksDuplicateReplies);
            Run("Screen intent detects natural screen scan requests", ScreenIntentDetectsNaturalScreenScanRequests);
            Run("Post reply guard allows repeated screen scan command", PostReplyGuardAllowsRepeatedScreenScanCommand);
            Run("Post reply guard rejects screen capability denial", PostReplyGuardRejectsScreenCapabilityDenial);
            Run("Post reply guard protects short affection", PostReplyGuardProtectsShortAffection);
            Run("Post reply guard protects soft social talk", PostReplyGuardProtectsSoftSocialTalk);
            Run("Post reply guard blocks affection productivity pivot", PostReplyGuardBlocksAffectionProductivityPivot);
            Run("Post reply guard blocks persona theater", PostReplyGuardBlocksPersonaTheater);
            Run("Post reply guard protects short greeting", PostReplyGuardProtectsShortGreeting);
            Run("Post reply guard blocks overbuilt greeting", PostReplyGuardBlocksOverbuiltGreeting);
            Run("Post reply guard blocks repeated fallback loop", PostReplyGuardBlocksRepeatedFallbackLoop);
            Run("Post reply guard rejects dotted garbage", PostReplyGuardRejectsDottedGarbage);
            Run("Semantic vision detects stuck coding flow", SemanticVisionDetectsStuckCodingFlow);
            Run("Autonomy reacts to semantic vision research", AutonomyReactsToSemanticVisionResearch);
            Run("Somatic tone shifts to quiet operator", SomaticToneShiftsToQuietOperator);
            Run("Vault synthesis suggests note links", VaultSynthesisSuggestsNoteLinks);
            Run("PC intent router routes safe OS commands", PcIntentRouterRoutesSafeOsCommands);
            Run("PC intent router separates screen and destructive commands", PcIntentRouterSeparatesScreenAndDestructiveCommands);
            Run("PC intent router routes full context scan", PcIntentRouterRoutesFullContextScan);
            Run("PC control executes safe PowerShell", PcControlExecutesSafePowerShell);
            Run("PC control executes shell chain", PcControlExecutesShellChain);
            Run("PC control stops shell chain on failed step", PcControlStopsShellChainOnFailedStep);
            Run("PC control blocks unsafe shell chain step", PcControlBlocksUnsafeShellChainStep);
            Run("PC control resolves coding workspace scenario", PcControlResolvesCodingWorkspaceScenario);
            Run("PC intent router routes shell chain and scenario", PcIntentRouterRoutesShellChainAndScenario);
            Run("PC intent router builds shell chain action type", PcIntentRouterBuildsShellChainActionType);
            Run("Resource guardian prompts on browser pressure in gaming", ResourceGuardianPromptsOnBrowserPressureInGaming);
            Run("Resource guardian respects prompt cooldown", ResourceGuardianRespectsPromptCooldown);
            Run("PC control get all context returns snapshot", PcControlGetAllContextReturnsSnapshot);
            Run("PC context v2 light returns foreground and system", PcContextV2LightReturnsForegroundAndSystem);
            Run("PC context v2 normal caps visible windows", PcContextV2NormalCapsVisibleWindows);
            Run("PC context redactor marks sensitive title", PcContextRedactorMarksSensitiveTitle);
            Run("PC action policy requires confirmation for kill process", PcActionPolicyRequiresConfirmationForKillProcess);
            Run("PC action policy allows open app", PcActionPolicyAllowsOpenApp);
            Run("PC action policy requires confirmation for shutdown", PcActionPolicyRequiresConfirmationForShutdown);
            Run("PC action policy blocks sensitive screenshot and external action", PcActionPolicyBlocksSensitiveScreenshotAndExternalAction);
            Run("PC action journal writes JSONL", PcActionJournalWritesJsonl);
            Run("PC rollback service creates file backup manifest", PcRollbackServiceCreatesFileBackupManifest);
            Run("PC action executor runs allowed open app dry-run", PcActionExecutorRunsAllowedOpenAppDryRun);
            Run("PC action executor returns confirmation for risky action", PcActionExecutorReturnsConfirmationForRiskyAction);
            Run("PC action executor logs blocked action", PcActionExecutorLogsBlockedAction);
            Run("PC intent router executes open app through action executor dry-run", PcIntentRouterExecutesOpenAppThroughActionExecutorDryRun);
            Run("PC intent router kill process needs confirmation", PcIntentRouterKillProcessNeedsConfirmation);
            Run("PC intent router blocks severe shell by policy", PcIntentRouterBlocksSevereShellByPolicy);
            Run("PC intent router full context uses context v2", PcIntentRouterFullContextUsesContextV2);
            Run("PC action executor confirms exact pending action id", PcActionExecutorConfirmsExactPendingActionId);
            Run("PC action executor rejects expired pending action", PcActionExecutorRejectsExpiredPendingAction);
            Run("PC action executor accepts generic yes with correct id", PcActionExecutorAcceptsGenericYesWithCorrectId);
            Run("PC action executor accepts target-only confirmation", PcActionExecutorAcceptsTargetOnlyConfirmation);
            Run("PC action executor accepts bare confirm action id", PcActionExecutorAcceptsBareConfirmActionId);
            Run("PC action executor confirms without id when only one pending", PcActionExecutorConfirmsWithoutIdWhenOnlyOnePending);
            Run("PC action executor cancel prevents later confirmation", PcActionExecutorCancelPreventsLaterConfirmation);
            Run("PC action executor keeps severe shell blocked after confirmation attempt", PcActionExecutorKeepsSevereShellBlockedAfterConfirmationAttempt);
            Run("PC intent router blocks unsafe shell chain by policy", PcIntentRouterBlocksUnsafeShellChainByPolicy);
            Run("PC intent router confirms and cancels action id route", PcIntentRouterConfirmsAndCancelsActionIdRoute);
            Run("PC action confirmation journal records states", PcActionConfirmationJournalRecordsStates);
            Run("PC control captures screenshot", PcControlCapturesScreenshot);
            Run("LLM vision compacts oversized screenshots", LlmVisionCompactsOversizedScreenshots);
            Run("Vision quality detects unusable denial", VisionQualityDetectsUnusableDenial);
            Run("Vision quality detects generic screenshot reply", VisionQualityDetectsGenericScreenshotReply);
            Run("Image processing enhances screenshot bytes", ImageProcessingEnhancesScreenshotBytes);
            Run("Screen awareness prompt includes foreground metadata", ScreenAwarenessPromptIncludesForegroundMetadata);
            Run("Screen awareness prompt includes idle time", ScreenAwarenessPromptIncludesIdleTime);
            Run("Screen awareness prompt includes multimodal context", ScreenAwarenessPromptIncludesMultimodalContext);
            Run("Screen awareness prompt asks for subtle patterns", ScreenAwarenessPromptAsksForSubtlePatterns);
            Run("Post reply guard repairs vision technical error", PostReplyGuardRepairsVisionTechnicalError);
            Run("Post reply guard protects image only prompt", PostReplyGuardProtectsImageOnlyPrompt);
            Run("Post reply guard duplicate image prompt avoids stale repeat text", PostReplyGuardDuplicateImagePromptAvoidsStaleRepeatText);
            Run("Post reply guard repairs Kokonoe image correction fallback", PostReplyGuardRepairsKokonoeImageCorrectionFallback);
            Run("Post reply guard softens one-letter Telegram ambiguity", PostReplyGuardSoftensOneLetterTelegramAmbiguity);
            Run("Post reply guard prioritizes image caption over stale one-letter context", PostReplyGuardPrioritizesImageCaptionOverStaleOneLetterContext);
            Run("Post reply guard repairs blamey answer explanation", PostReplyGuardRepairsBlameyAnswerExplanation);
            Run("Post reply guard blocks assistant-owned reminder schedule", PostReplyGuardBlocksAssistantOwnedReminderSchedule);
            Run("Post reply guard blocks follow-up after conversation close", PostReplyGuardBlocksFollowupAfterConversationClose);
            Run("Post reply guard blocks conversation review deflection", PostReplyGuardBlocksConversationReviewDeflection);
            Run("Post reply guard protects short apology", PostReplyGuardProtectsShortApology);
            Run("Post reply guard deescalates tone boundary", PostReplyGuardDeescalatesToneBoundary);
            Run("Post reply guard grounds pulse questions", PostReplyGuardGroundsPulseQuestions);
            Run("Post reply guard blocks therapy meta tone", PostReplyGuardBlocksTherapyMetaTone);
            Run("Post reply guard blocks fabricated external facts", PostReplyGuardBlocksFabricatedExternalFacts);
            Run("Post reply guard blocks stale proactive ping on direct topic", PostReplyGuardBlocksStaleProactivePingOnDirectTopic);
            Run("Post reply guard blocks service bot tone", PostReplyGuardBlocksServiceBotTone);
            Run("Post reply guard blocks robotic support tone", PostReplyGuardBlocksRoboticSupportTone);
            Run("Post reply guard blocks AI service phrases", PostReplyGuardBlocksAiServicePhrases);
            Run("Post reply guard blocks punitive network threat", PostReplyGuardBlocksPunitiveNetworkThreat);
            Run("Post reply guard blocks blind agreement", PostReplyGuardBlocksBlindAgreement);
            Run("Social engine detects flirting without waifu mode", SocialEngineDetectsFlirtingWithoutWaifuMode);
            Run("Social engine stress mutes sarcasm", SocialEngineStressMutesSarcasm);
            Run("Social engine detects dismissive cold tone", SocialEngineDetectsDismissiveColdTone);
            Run("Emotional memory records rude exit grudge", EmotionalMemoryRecordsRudeExitGrudge);
            Run("Emotional memory hydrates raw chat and visual anchor", EmotionalMemoryHydratesRawChatAndVisualAnchor);
            Run("Post reply guard blocks roleplay and pause metrics", PostReplyGuardBlocksRoleplayAndPauseMetrics);
            Run("Post reply guard blocks soul-breaking status report", PostReplyGuardBlocksSoulBreakingStatusReport);
            Run("Post reply guard blocks conversation mechanics leak", PostReplyGuardBlocksConversationMechanicsLeak);
            Run("Post reply guard blocks scheduler record leakage", PostReplyGuardBlocksSchedulerRecordLeakage);
            Run("Post reply guard blocks canned startup probe", PostReplyGuardBlocksCannedStartupProbe);
            Run("Neural governor parses response frame JSON", NeuralGovernorParsesResponseFrameJson);
            Run("Persona engine calibrates soft social voice", PersonaEngineCalibratesSoftSocialVoice);
            Run("Persona engine avoids plain low signal stances", PersonaEngineAvoidsPlainLowSignalStances);
            Run("Temperament classifies exhausted hostile", TemperamentClassifiesExhaustedHostile);
            Run("Temperament recovers from favor", TemperamentRecoversFromFavor);
            Run("Temperament prompt prevents theater", TemperamentPromptPreventsTheater);
            Run("Living conversation shifts to quiet operator", LivingConversationShiftsToQuietOperator);
            Run("Living conversation preserves social bids", LivingConversationPreservesSocialBids);
            Run("Living conversation avoids repeated moves", LivingConversationAvoidsRepeatedMoves);
            Run("Subconscious frame accepts corrections", SubconsciousFrameAcceptsCorrections);
            Run("Subconscious frame routes action requests", SubconsciousFrameRoutesActionRequests);
            Run("Async personality snapshot prioritizes corrections", AsyncPersonalitySnapshotPrioritizesCorrections);
            Run("Async personality snapshot quiets high stress", AsyncPersonalitySnapshotQuietsHighStress);
            Run("Temporal presence classifies abrupt absence", TemporalPresenceClassifiesAbruptAbsence);
            Run("Temporal presence records planned farewell", TemporalPresenceRecordsPlannedFarewell);
            Run("Emotion modifiers keep distant useful", EmotionModifiersKeepDistantUseful);
            Run("Emotion engine supports expanded states", EmotionEngineSupportsExpandedStates);
            Run("Emotion context exposes PAD and salient history", EmotionContextExposesPadAndSalientHistory);
            Run("Runtime state exposes PAD voice directive", RuntimeStateExposesPadVoiceDirective);
            Run("Runtime state exposes temperament", RuntimeStateExposesTemperament);
            Run("Runtime state exposes living conversation", RuntimeStateExposesLivingConversation);
            Run("Runtime state exposes subconscious frame", RuntimeStateExposesSubconsciousFrame);
            Run("Runtime state exposes async personality", RuntimeStateExposesAsyncPersonality);
            Run("Runtime state exposes temporal presence", RuntimeStateExposesTemporalPresence);
            Run("Response planner classifies critical assistant architecture", ResponsePlannerClassifiesCriticalAssistantArchitecture);
            Run("Response planner includes executive monologue and critique", ResponsePlannerIncludesExecutiveMonologueAndCritique);
            Run("Response planner fatigue pushes back after midnight", ResponsePlannerFatiguePushesBackAfterMidnight);
            Run("Response planner rejects low agency premise", ResponsePlannerRejectsLowAgencyPremise);
            Run("Response planner requires vault read for memory questions", ResponsePlannerRequiresVaultReadForMemoryQuestions);
            Run("Response planner routes Obsidian scan profile questions", ResponsePlannerRoutesObsidianScanProfileQuestions);
            Run("Response planner routes identity alias questions to vault", ResponsePlannerRoutesIdentityAliasQuestionsToVault);
            Run("Response planner routes natural screen questions", ResponsePlannerRoutesNaturalScreenQuestions);
            Run("Memory policy stores stable preference", MemoryPolicyStoresStablePreference);
            Run("Memory policy keeps temporary state out of beliefs", MemoryPolicyKeepsTemporaryStateOutOfBeliefs);
            Run("Memory policy computes salience", MemoryPolicyComputesSalience);
            Run("Memory policy reinforces duplicate fact", MemoryPolicyReinforcesDuplicateFact);
            Run("Memory recall uses hybrid scoring", MemoryRecallUsesHybridScoring);
            Run("Continuity reinforces repeated belief", ContinuityReinforcesRepeatedBelief);
            Run("Post reply guard repairs sleep leak on profile question", PostReplyGuardRepairsSleepLeakOnProfileQuestion);
            Run("Post reply guard rejects profile quote fallback", PostReplyGuardRejectsProfileQuoteFallback);
            Run("Post reply guard rejects scripted profile source report", PostReplyGuardRejectsScriptedProfileSourceReport);
            Run("Post reply guard blocks false vault unavailable on identity query", PostReplyGuardBlocksFalseVaultUnavailableOnIdentityQuery);
            Run("Post reply guard blocks false vault unavailable on Obsidian exploration", PostReplyGuardBlocksFalseVaultUnavailableOnObsidianExploration);
            Run("Post reply guard blocks Obsidian pseudo progress", PostReplyGuardBlocksObsidianPseudoProgress);
            Run("Post reply guard blocks profile update pseudo progress", PostReplyGuardBlocksProfileUpdatePseudoProgress);
            Run("Post reply guard blocks Obsidian followup deflection", PostReplyGuardBlocksObsidianFollowupDeflection);
            Run("Post reply guard rejects general quote echo fallback", PostReplyGuardRejectsGeneralQuoteEchoFallback);
            Run("Proactive guard suppresses repeated generic silence", ProactiveGuardSuppressesRepeatedGenericSilence);
            Run("Proactive context anchors silence to last topic", ProactiveContextAnchorsSilenceToLastTopic);
            Run("Proactive context stays silent after goodbye sleep", ProactiveContextStaysSilentAfterGoodbyeSleep);
            Run("Conversation close until morning creates silence intent", ConversationCloseUntilMorningCreatesSilenceIntent);
            Run("Proactive context stays silent after conversation close", ProactiveContextStaysSilentAfterConversationClose);
            Run("Proactive fallback never exposes technical silence wording", ProactiveFallbackNeverExposesTechnicalSilenceWording);
            Run("Proactive fallback does not quote last user text", ProactiveFallbackDoesNotQuoteLastUserText);
            Run("User quiet command mutes proactive followups", UserQuietCommandMutesProactiveFollowups);
            Run("User quiet command reply hides mechanics", UserQuietCommandReplyHidesMechanics);
            Run("Reminder scheduled reply hides scheduler ids", ReminderScheduledReplyHidesSchedulerIds);
            Run("Active agency action reports stay internal", ActiveAgencyActionReportsStayInternal);
            Run("Research action reports stay internal", ResearchActionReportsStayInternal);
            Run("Blackboard builds prompt snapshot and reloads tail", BlackboardBuildsPromptSnapshotAndReloadsTail);
            Run("Genesis agent factory registers role profile", GenesisAgentFactoryRegistersRoleProfile);
            Run("Genesis agent factory disables profile", GenesisAgentFactoryDisablesProfile);
            Run("System overlord scans metadata and proposes cleanup", SystemOverlordScansMetadataAndProposesCleanup);
            Run("System overlord cleanup requires permission", SystemOverlordCleanupRequiresPermission);
            Run("Action directive router generalizes local artifacts", ActionDirectiveRouterGeneralizesLocalArtifacts);
            Run("Action directive router sends non artifact tasks to agents", ActionDirectiveRouterSendsNonArtifactTasksToAgents);
            Run("LLM guard blocks unverified action claims", LlmGuardBlocksUnverifiedActionClaims);
            Run("Detect best tool picks fs tools for disk requests", DetectBestToolPicksFsToolsForDiskRequests);
            Run("Truncate note for tool result caps oversized notes", TruncateNoteForToolResultCapsOversizedNotes);
            Run("Action directive router handles inflected targets", ActionDirectiveRouterHandlesInflectedTargets);
            Run("Generated content route stays separate from surprise scan", GeneratedContentRouteStaysSeparateFromSurpriseScan);
            Run("Chat runtime defaults to streaming with bounded token budget", ChatRuntimeDefaultsToStreamingWithBoundedTokenBudget);
            Run("Brain static context cache honors TTL", BrainStaticContextCacheHonorsTtl);
            Run("Brain context keeps recent chat dynamic", BrainContextKeepsRecentChatDynamic);
            Run("Web bridge completes ping pong round trip", WebBridgeCompletesPingPongRoundTrip);
            Run("Web bridge reports unknown methods", WebBridgeReportsUnknownMethods);
            Run("Web bridge contract covers frontend and compatibility methods", WebBridgeContractCoversFrontendAndCompatibilityMethods);
            Run("Web runtime bridge returns snapshot and publishes refresh", WebRuntimeBridgeReturnsSnapshotAndPublishesRefresh);
            Run("Web system bridge scans and publishes snapshot", WebSystemBridgeScansAndPublishesSnapshot);
            Run("Web memory bridge returns facts and publishes refresh", WebMemoryBridgeReturnsFactsAndPublishesRefresh);
            Run("Web startup policy defaults to web with explicit rollback", WebStartupPolicyDefaultsToWebWithExplicitRollback);
            Run("Web shell development URL allows loopback only", WebShellDevelopmentUrlAllowsLoopbackOnly);
            Run("Web shell keeps diagnostics page after core startup", WebShellKeepsDiagnosticsPageAfterCoreStartup);
            Run("Web chat bridge streams correlated chunks", WebChatBridgeStreamsCorrelatedChunks);
            Run("Web chat bridge resets partial stream before fallback", WebChatBridgeResetsPartialStreamBeforeFallback);
            Run("Web chat bridge streams tool fallback after reset", WebChatBridgeStreamsToolFallbackAfterReset);
            Run("OpenAI stream parser joins content chunks", OpenAiStreamParserJoinsContentChunks);
            Run("LLM streaming policy limits tool final streaming", LlmStreamingPolicyLimitsToolFinalStreaming);
            Run("Web chat bridge publishes proactive brain messages", WebChatBridgePublishesProactiveBrainMessages);
            Run("Web agent bridge returns camel case snapshot", WebAgentBridgeReturnsCamelCaseSnapshot);
            Run("Web agent bridge exposes task evidence fields", WebAgentBridgeExposesTaskEvidenceFields);
            Run("Web agent bridge creates task from shell", WebAgentBridgeCreatesTaskFromShell);
            Run("Web agent bridge creates batch from shell", WebAgentBridgeCreatesBatchFromShell);
            Run("Web agent bridge cancels task from shell", WebAgentBridgeCancelsTaskFromShell);
            Run("Web agent bridge controls runner", WebAgentBridgeControlsRunner);
            Run("Web agent bridge publishes live activity", WebAgentBridgePublishesLiveActivity);
            Run("Web vault bridge caches and refreshes status", WebVaultBridgeCachesAndRefreshesStatus);
            Run("Web settings bridge masks and preserves secrets", WebSettingsBridgeMasksAndPreservesSecrets);
            Run("Web settings bridge rejects invalid values", WebSettingsBridgeRejectsInvalidValues);
            Run("Web Telegram bridge publishes safe live status", WebTelegramBridgePublishesSafeLiveStatus);
            Run("System overlord detects retry surprise directive", SystemOverlordDetectsRetrySurpriseDirective);
            Run("System overlord creates real surprise note", SystemOverlordCreatesRealSurpriseNote);
            Run("Collective mind builds agent debate frame", CollectiveMindBuildsAgentDebateFrame);
            Run("Unified context includes collective mind", UnifiedContextIncludesCollectiveMind);
            Run("Narrative continue stays conversational", NarrativeContinueStaysConversational);
            Run("Narrative continue context ignores internal status", NarrativeContinueContextIgnoresInternalStatus);
            Run("Short acknowledgement resolves stale intent", ShortAcknowledgementResolvesStaleIntent);
            Run("Screen awareness parses vision JSON", ScreenAwarenessParsesVisionJson);
            Run("Screen awareness suppresses repeated comments", ScreenAwarenessSuppressesRepeatedComments);
            Run("Screen awareness redacts private identifiers", ScreenAwarenessRedactsPrivateIdentifiers);
            Run("Screen awareness allows rare passive jab", ScreenAwarenessAllowsRarePassiveJab);
            Run("Screen awareness allows medium passive chat jab", ScreenAwarenessAllowsMediumPassiveChatJab);
            Run("Screen awareness lets jab bypass comment cooldown", ScreenAwarenessLetsJabBypassCommentCooldown);
            Run("Screen awareness game mode uses active jab cooldown", ScreenAwarenessGameModeUsesActiveJabCooldown);
            Run("Screen awareness blocks sensitive screens", ScreenAwarenessBlocksSensitiveScreens);
            Run("Screen awareness builds situation context", ScreenAwarenessBuildsSituationContext);
            Run("Screen awareness builds aggregate pattern candidate", ScreenAwarenessBuildsAggregatePatternCandidate);
            Run("Startup greeting avoids dead canned replies", StartupGreetingAvoidsDeadCannedReplies);
            Run("Startup greeting sanitizes dry return line", StartupGreetingSanitizesDryReturnLine);
            Run("Startup greeting ignores low signal topic", StartupGreetingIgnoresLowSignalTopic);
            Run("Startup greeting reacts to quick return", StartupGreetingReactsToQuickReturn);
            Run("Startup greeting classifies clean goodbye", StartupGreetingClassifiesCleanGoodbye);
            Run("Startup greeting classifies abrupt exit", StartupGreetingClassifiesAbruptExit);
            Run("Startup greeting classifies conflict residue", StartupGreetingClassifiesConflictResidue);
            Run("Startup greeting rejects stale context table fallback", StartupGreetingRejectsStaleContextTableFallback);
            Run("Startup greeting prompt uses mood and absence", StartupGreetingPromptUsesMoodAndAbsence);
            Run("Startup greeting prompt includes temporal presence", StartupGreetingPromptIncludesTemporalPresence);
            Run("Startup greeting sanitizes therapy meta", StartupGreetingSanitizesTherapyMeta);
            Run("Startup greeting fallback does not quote raw latest user text", StartupGreetingFallbackDoesNotQuoteRawLatestUserText);
            Run("Startup greeting sanitizes system status report", StartupGreetingSanitizesSystemStatusReport);
            Run("Scenario simulation guards temporal continuity", ScenarioSimulationGuardsTemporalContinuity);
            Run("LLM diagnostics snapshot starts idle", LlmDiagnosticsSnapshotStartsIdle);
            Run("LLM diagnostics labels local Ollama provider", LlmDiagnosticsLabelsLocalOllamaProvider);
            Run("LLM extracts reasoning content for vision replies", LlmExtractsReasoningContentForVisionReplies);
            Run("LLM uses Ollama image URL string payload", LlmUsesOllamaImageUrlStringPayload);
            Run("LLM visible text rejects dotted garbage", LlmVisibleTextRejectsDottedGarbage);
            Run("LLM rotates Ollama key after auth failure", LlmRotatesOllamaKeyAfterAuthFailure);
            Run("LLM agent key pool exhaustion does not reuse legacy key", LlmAgentKeyPoolExhaustionDoesNotReuseLegacyKey);
            Run("Inspector renders state report", InspectorRendersStateReport);
            Run("Obsidian vault architecture maintenance", ObsidianVaultArchitectureMaintenance);
            Run("Obsidian excludes profile backups from index", ObsidianExcludesProfileBackupsFromIndex);
            Run("Obsidian unique memory append", ObsidianUniqueMemoryAppend);
            Run("Obsidian profile updater writes neutral profile", ObsidianProfileUpdaterWritesNeutralProfile);
            Run("Obsidian profile updater throttles backup spam", ObsidianProfileUpdaterThrottlesBackupSpam);
            Run("Autonomous profile curator detects durable facts", AutonomousProfileCuratorDetectsDurableFacts);
            Run("Autonomous profile curator detects promise obligations", AutonomousProfileCuratorDetectsPromiseObligations);
            Run("Autonomous profile curator writes synthesized update", AutonomousProfileCuratorWritesSynthesizedUpdate);
            Run("Obsidian rejects paths outside vault", ObsidianRejectsPathsOutsideVault);
            Run("LLM vault tool routing avoids accidental writes", LlmVaultToolRoutingAvoidsAccidentalWrites);
            Run("Obsidian memory quality and task queue", ObsidianMemoryQualityAndTaskQueue);
            Run("Obsidian memory duplicate cleanup", ObsidianMemoryDuplicateCleanup);
            Run("Obsidian self-healing memory writes report", ObsidianSelfHealingMemoryWritesReport);
            Run("Obsidian memory review suggestions", ObsidianMemoryReviewSuggestions);
            Run("Obsidian normalizes malformed frontmatter", ObsidianNormalizesMalformedFrontmatter);
            Run("Obsidian vault doctor repairs phantom graph links", ObsidianVaultDoctorRepairsPhantomGraphLinks);
            Run("Obsidian rebuild links preserves frontmatter", ObsidianRebuildLinksPreservesFrontmatter);
            Run("Obsidian rebuild links skips suppressed actor names", ObsidianRebuildLinksSkipsSuppressedActorNames);
            Run("State reconciliation closes stale intent from external activity", StateReconciliationClosesStaleIntentFromExternalActivity);
            Run("Screen awareness classifies modes", ScreenAwarenessClassifiesModes);
            Run("Proactive context requires natural trigger for silence ping", ProactiveContextRequiresNaturalTriggerForSilencePing);
            Run("Obsidian preflight context loads vault before reply", ObsidianPreflightContextLoadsVaultBeforeReply);
            Run("Obsidian exploration finds interesting notes", ObsidianExplorationFindsInterestingNotes);
            Run("Obsidian access report completes task", ObsidianAccessReportCompletesTask);
            Run("Telegram unified context loads Obsidian profile preflight", TelegramUnifiedContextLoadsObsidianProfilePreflight);
            Run("Agent task service plans and persists", AgentTaskServicePlansAndPersists);
            Run("Iterative agent loop executes one action per turn", IterativeAgentLoopExecutesOneActionPerTurn);
            Run("Iterative agent loop replans from failed observation", IterativeAgentLoopReplansFromFailedObservation);
            Run("Iterative agent loop persists confirmation state", IterativeAgentLoopPersistsConfirmationState);
            Run("Iterative agent loop rejects fake completion without evidence", IterativeAgentLoopRejectsFakeCompletionWithoutEvidence);
            Run("Iterative agent loop appends evidence summary", IterativeAgentLoopAppendsEvidenceSummary);
            Run("Agent task service imports Obsidian backlog", AgentTaskServiceImportsObsidianBacklog);
            Run("Response planner adds insight extraction", ResponsePlannerAddsInsightExtraction);
            Run("Response planner adds system control", ResponsePlannerAddsSystemControl);
            Run("Response planner routes generic scan to system control", ResponsePlannerRoutesGenericScanToSystemControl);
            Run("Response planner adds vision for screen scan", ResponsePlannerAddsVisionForScreenScan);
            Run("Response planner adds long observation", ResponsePlannerAddsLongObservation);
            Run("Response planner detects gameplay observation", ResponsePlannerDetectsGameplayObservation);
            Run("Agent task service executes system control", AgentTaskServiceExecutesSystemControl);
            Run("Response planner adds self review", ResponsePlannerAddsSelfReview);
            Run("Agent task service inserts correction after weak self review", AgentTaskServiceInsertsCorrectionAfterWeakSelfReview);
            Run("Agent task service inserts hard reset after lazy self review", AgentTaskServiceInsertsHardResetAfterLazySelfReview);
            Run("Agent task service executes background vault insight", AgentTaskServiceExecutesBackgroundVaultInsight);
            Run("Obsidian consolidates notes non destructively", ObsidianConsolidatesNotesNonDestructively);
            Run("Predictive screen warmup loads Obsidian context", PredictiveScreenWarmupLoadsObsidianContext);
            Run("Conversation stagnation forces pending thought", ConversationStagnationForcesPendingThought);
            Run("Conversation stagnation prompt expires", ConversationStagnationPromptExpires);
            Run("Natural synthesis detects source reporting", NaturalSynthesisDetectsSourceReporting);
            Run("Emotion style constrains irritated replies", EmotionStyleConstrainsIrritatedReplies);
            Run("Persona guard directive allows mature tone without dropping character", PersonaGuardDirectiveAllowsMatureToneWithoutDroppingCharacter);
            Run("Bot phrase detection still works after dedup", BotPhraseDetectionStillWorksAfterDedup);
            Run("Persona inference defaults are sane", PersonaInferenceDefaultsAreSane);
            Run("Sandbox executor cleans timed out scripts", SandboxExecutorCleansTimedOutScripts);
            Run("Agent task service plans self review and system control", AgentTaskServicePlansSelfReviewAndSystemControl);
            Run("Observation service parses schedule", ObservationServiceParsesSchedule);
            Run("Observation service runs one fallback sample", ObservationServiceRunsOneFallbackSample);
            Run("Agent completion policy reports observation result", AgentCompletionPolicyReportsObservationResult);
            Run("Agent completion policy asks next question", AgentCompletionPolicyAsksNextQuestion);
            Run("Agent completion policy reports insight result", AgentCompletionPolicyReportsInsightResult);
            Run("Agent completion policy avoids canned wait tail", AgentCompletionPolicyAvoidsCannedWaitTail);
            Run("Scheduler drops stale user reminders", SchedulerDropsStaleUserReminders);
            Run("Reminder parser handles relative and vague time", ReminderParserHandlesRelativeAndVagueTime);
            Run("Reminder parser handles wake and absolute time", ReminderParserHandlesWakeAndAbsoluteTime);
            Run("Reminder parser ignores later continuation status", ReminderParserIgnoresLaterContinuationStatus);
            Run("Repair mojibake must not mangle clean character core", RepairMojibakeMustNotMangleCleanCharacterCore);

            if (!string.IsNullOrWhiteSpace(_filter) && _selected == 0)
                throw new InvalidOperationException($"FAIL: no tests matched filter '{_filter}'.");

            Console.WriteLine($"PASS {_passed} tests");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void RepairMojibakeMustNotMangleCleanCharacterCore()
    {
        // Production bug: ScoreMojibake's Р\p{IsCyrillic}/С\p{IsCyrillic} check fired on
        // ordinary Cyrillic prose (any word starting with Р or С followed by another letter -
        // most of them), so the entire character core got cp1251-round-tripped into ~70%
        // U+FFFD garbage before every single request. This is the exact real text that was
        // getting destroyed - assert it survives intact now.
        var input = KokoCharacterCore.Core + "\n\n" + KokoCharacterCore.VoiceExamples;
        var output = LlmService.RepairMojibake(input);
        AssertEqual(input, output, "clean Ukrainian character core must pass through RepairMojibake unchanged");
        AssertTrue(!output.Contains('�'), "must not introduce replacement characters into clean text");

        // Genuine garbage (literal U+FFFD, e.g. from a real upstream encoding failure) must
        // still get stripped, not preserved.
        var garbage = "��� Yasu ���";
        AssertTrue(!LlmService.RepairMojibake(garbage).Contains('�'), "literal replacement characters must still be stripped");
    }

    private static void ReminderParserHandlesRelativeAndVagueTime()
    {
        var now = new DateTime(2026, 5, 19, 20, 40, 0);

        AssertTrue(ReminderCommandParser.TryParse("нагадай мені через 10 хвилин перевірити білд", now, out var exact),
            "relative reminder should parse");
        AssertEqual(now.AddMinutes(10), exact.FireAt, "relative minutes should schedule exact offset");
        AssertTrue(!exact.IsWake, "relative reminder should not be wake");
        AssertTrue(exact.Prompt.Contains("перевірити білд"), "prompt should preserve reminder content");

        AssertTrue(ReminderCommandParser.TryParse("напиши мені якось потім про воду", now, out var vague),
            "vague later reminder should parse");
        AssertEqual(now.AddMinutes(30), vague.FireAt, "vague later should default to 30 minutes");
        AssertTrue(vague.UsedAssumedLater, "vague later should mark assumption");
    }

    private static void ReminderParserHandlesWakeAndAbsoluteTime()
    {
        var now = new DateTime(2026, 5, 19, 20, 40, 0);

        AssertTrue(ReminderCommandParser.TryParse("розбуди мене о 07:30", now, out var wake),
            "wake command should parse");
        AssertTrue(wake.IsWake, "wake command should be marked");
        AssertEqual(new DateTime(2026, 5, 20, 7, 30, 0), wake.FireAt, "past absolute time should roll to tomorrow");

        AssertTrue(ReminderCommandParser.TryParse("нагадай об 21:05 глянути Vault", now, out var reminder),
            "absolute reminder should parse");
        AssertEqual(new DateTime(2026, 5, 19, 21, 5, 0), reminder.FireAt, "future absolute time should stay today");
    }

    private static void ReminderParserIgnoresLaterContinuationStatus()
    {
        var now = new DateTime(2026, 6, 13, 22, 48, 0);

        AssertTrue(
            !ReminderCommandParser.TryParse("\u041f\u043e\u0442\u0456\u043c \u043f\u0440\u043e\u0434\u043e\u0432\u0436\u0438\u043c\u043e, \u044f \u0432\u0438\u0439\u0448\u043e\u0432 \u043d\u0430 \u043f\u0440\u043e\u0433\u0443\u043b\u044f\u043d\u043a\u0443 \u0437 \u0434\u0440\u0443\u0433\u043e\u043c", now, out _),
            "later continuation plus away status should not become a +30m reminder");

        AssertTrue(
            ReminderCommandParser.TryParse("\u043d\u0430\u043f\u0438\u0448\u0438 \u043c\u0435\u043d\u0456 \u044f\u043a\u043e\u0441\u044c \u043f\u043e\u0442\u0456\u043c \u043f\u0440\u043e \u0432\u043e\u0434\u0443", now, out var reminder),
            "explicit write/remind later request should still parse");
        AssertTrue(reminder.UsedAssumedLater, "explicit vague later reminder should still mark assumed later");
    }

    private static void SomaticWiredFromPulseSpike()
    {
        var somatic = new KokoSomaticEngine().Evaluate(new KokoSomaticInput
        {
            Bpm = 96,
            BaselineBpm = 62,
            Stress = 0.35,
            Arousal = 0.45,
            Fatigue = 0.10,
            Now = new DateTime(2026, 5, 5, 14, 0, 0)
        });

        AssertEqual("wired", somatic.State, "high BPM delta should be wired");
        AssertTrue(somatic.IsVeryElevated, "wired state should be very elevated");
    }

    private static void SomaticTiredFromLowCharge()
    {
        var somatic = new KokoSomaticEngine().Evaluate(new KokoSomaticInput
        {
            Bpm = 51,
            BaselineBpm = 64,
            Stress = 0.05,
            Arousal = 0.05,
            Fatigue = 0.72,
            Now = new DateTime(2026, 5, 5, 1, 30, 0)
        });

        AssertEqual("tired", somatic.State, "low BPM and night should be tired");
        AssertTrue(somatic.IsLow, "low-charge state should expose IsLow");
    }

    private static void WearableTelemetryInfersLikelySleep()
    {
        var dir = TempDir();
        try
        {
            var service = new KokoWearableTelemetryService(dir);
            var start = new DateTime(2026, 6, 1, 1, 10, 0, DateTimeKind.Utc);

            for (var i = 0; i < 8; i++)
            {
                service.Ingest(new KokoWearableSample
                {
                    TimestampUtc = start.AddMinutes(i * 2),
                    DeviceId = "galaxy-watch-8-lte",
                    HeartRateBpm = 55 - (i % 2),
                    HrvRmssdMs = 42,
                    Motion = 0.03,
                    OnWrist = true,
                    Activity = "rest"
                });
            }

            var state = service.State;
            AssertEqual("probably_asleep", state.SleepState, "night, low motion, on-wrist and low bpm should infer likely sleep");
            AssertTrue(state.SleepConfidence >= 0.72, "sleep confidence should be high");
            AssertTrue(service.BuildPromptBlock(start).Contains("WEARABLE TELEMETRY"), "prompt block should expose wearable context");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableTelemetryInfersStressInitiative()
    {
        var dir = TempDir();
        try
        {
            var service = new KokoWearableTelemetryService(dir);
            var start = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            for (var i = 0; i < 5; i++)
            {
                service.Ingest(new KokoWearableSample
                {
                    TimestampUtc = start.AddMinutes(i),
                    DeviceId = "galaxy-watch-8-lte",
                    HeartRateBpm = 66,
                    HrvRmssdMs = 45,
                    Motion = 0.18,
                    OnWrist = true
                });
            }

            for (var i = 0; i < 6; i++)
            {
                service.Ingest(new KokoWearableSample
                {
                    TimestampUtc = start.AddMinutes(8 + i),
                    DeviceId = "galaxy-watch-8-lte",
                    HeartRateBpm = 112,
                    HrvRmssdMs = 12,
                    SpO2Percent = 92,
                    Motion = 0.05,
                    OnWrist = true,
                    Activity = "desk"
                });
            }

            var state = service.State;
            AssertTrue(state.StressScore >= 0.62, "high bpm delta, low HRV and low SpO2 should infer stress");
            AssertEqual("strained", state.RecoveryState, "high stress should classify recovery as strained");
            AssertEqual("suggest_short_break", state.SuggestedInitiative, "strained daytime telemetry should suggest a low-risk break");
            var prompt = service.BuildPromptBlock(start.AddMinutes(15));
            AssertTrue(prompt.Contains("stress=", StringComparison.OrdinalIgnoreCase), "prompt should expose stress score");
            AssertTrue(prompt.Contains("initiative=suggest_short_break", StringComparison.OrdinalIgnoreCase), "prompt should expose initiative");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableTelemetryMarksDiagnosticTrustState()
    {
        var dir = TempDir();
        try
        {
            var service = new KokoWearableTelemetryService(dir);
            var result = service.IngestDetailed(new KokoWearableSample
            {
                SampleId = "smoke-hr-1",
                TimestampUtc = DateTime.UtcNow,
                DeviceId = "live-smoke-watch-b",
                Source = "diagnostic-smoke",
                HeartRateBpm = 103,
                Motion = 0.42,
                OnWrist = true,
                Note = "smoke test sample"
            });

            AssertTrue(result.Accepted, "diagnostic sample can be accepted for logs");
            AssertTrue(result.State.IsDiagnosticSample, "diagnostic sample should be stamped as diagnostic");
            AssertEqual("diagnostic_blocked", result.State.TrustState, "diagnostic sample should not look trusted");
            AssertTrue(result.State.TrustReason.Contains("diagnostic", StringComparison.OrdinalIgnoreCase), "trust reason should explain diagnostic source");
            AssertTrue(service.BuildPromptBlock(DateTime.UtcNow).Contains("diagnostic_sample=True", StringComparison.OrdinalIgnoreCase), "prompt should expose diagnostic flag");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableTelemetryIgnoresDuplicateSampleIds()
    {
        var dir = TempDir();
        try
        {
            var service = new KokoWearableTelemetryService(dir);
            var timestamp = DateTime.UtcNow;

            var first = service.IngestDetailed(new KokoWearableSample
            {
                SampleId = "dup-1",
                TimestampUtc = timestamp,
                DeviceId = "watch",
                HeartRateBpm = 72,
                Motion = 0.2,
                OnWrist = true
            });
            var duplicate = service.IngestDetailed(new KokoWearableSample
            {
                SampleId = "dup-1",
                TimestampUtc = timestamp.AddSeconds(1),
                DeviceId = "watch",
                HeartRateBpm = 140,
                Motion = 0.8,
                OnWrist = true
            });

            AssertTrue(first.Accepted, "first sample id should be accepted");
            AssertTrue(duplicate.Duplicate, "replayed sample id should be flagged as duplicate");
            AssertTrue(!duplicate.Accepted, "duplicate sample should not be accepted");
            AssertTrue(Math.Abs(service.State.CurrentBpm - 72) < 0.1, "duplicate sample should not overwrite state");
            AssertEqual(1, service.RecentSamples.Count, "duplicate sample should not be added to recent samples");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableTelemetryRaisesStressSpikeEvent()
    {
        var dir = TempDir();
        try
        {
            var service = new KokoWearableTelemetryService(dir);
            var timestamp = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var events = new List<string>();
            service.SampleAccepted += (result, _) =>
            {
                if (!string.IsNullOrWhiteSpace(result.EventKind))
                    events.Add(result.EventKind);
            };

            service.IngestDetailed(new KokoWearableSample
            {
                SampleId = "stress-base",
                TimestampUtc = timestamp,
                DeviceId = "watch",
                HeartRateBpm = 70,
                HrvRmssdMs = 42,
                Motion = 0.12,
                OnWrist = true
            });
            var spike = service.IngestDetailed(new KokoWearableSample
            {
                SampleId = "stress-spike",
                TimestampUtc = timestamp.AddSeconds(10),
                DeviceId = "watch",
                HeartRateBpm = 98,
                HrvRmssdMs = 16,
                Motion = 0.08,
                OnWrist = true
            });

            AssertTrue(spike.Accepted, "stress spike sample should be accepted");
            AssertEqual("stress_spike", spike.EventKind, "large fresh BPM jump should raise a stress spike event");
            AssertTrue(events.Contains("stress_spike"), "sample accepted event should publish the stress spike");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableTelemetryReloadsRecentSampleIds()
    {
        var dir = TempDir();
        try
        {
            var timestamp = DateTime.UtcNow;
            var firstService = new KokoWearableTelemetryService(dir);
            firstService.IngestDetailed(new KokoWearableSample
            {
                SampleId = "restart-dup-1",
                TimestampUtc = timestamp,
                DeviceId = "watch",
                HeartRateBpm = 74,
                Motion = 0.2,
                OnWrist = true
            });

            var secondService = new KokoWearableTelemetryService(dir);
            var replay = secondService.IngestDetailed(new KokoWearableSample
            {
                SampleId = "restart-dup-1",
                TimestampUtc = timestamp.AddSeconds(5),
                DeviceId = "watch",
                HeartRateBpm = 145,
                Motion = 0.9,
                OnWrist = true
            });

            AssertTrue(replay.Duplicate, "replayed sample id should survive telemetry service restart");
            AssertTrue(!replay.Accepted, "replayed sample after restart should not be accepted");
            AssertTrue(Math.Abs(secondService.State.CurrentBpm - 74) < 0.1, "replayed sample after restart should not overwrite restored state");
            AssertEqual(0, secondService.RecentSamples.Count, "replayed sample after restart should not be added to recent samples");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void HeartEnginePrefersFreshWearableBpm()
    {
        var dir = TempDir();
        try
        {
            var wearable = new KokoWearableTelemetryService(dir);
            wearable.Ingest(new KokoWearableSample
            {
                TimestampUtc = DateTime.UtcNow,
                DeviceId = "galaxy-watch-8-lte",
                HeartRateBpm = 92,
                Motion = 0.4,
                OnWrist = true
            });

            var emotion = new KokoEmotionEngine(dir);
            using var heart = new KokoHeartEngine(emotion, dir, wearable);
            heart.Start();
            System.Threading.Thread.Sleep(2500);

            AssertTrue(Math.Abs(heart.CurrentBpm - 92) < 25, "fresh wearable bpm should pull heart engine toward the real sensor value");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableBridgeIngestsAuthorizedSample()
    {
        var dir = TempDir();
        var port = 19000 + Random.Shared.Next(1000);
        try
        {
            var telemetry = new KokoWearableTelemetryService(dir);
            using var bridge = new KokoWearableBridgeService(telemetry, dir, port);
            bridge.Start();
            AssertTrue(bridge.IsRunning, $"bridge should start; error={bridge.LastError}");

            using var client = new System.Net.Http.HttpClient();
            var pair = client.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/pair",
                    new System.Net.Http.StringContent("""{"deviceId":"galaxy-watch-8-lte"}""", System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();
            AssertTrue(pair.IsSuccessStatusCode, "pair should succeed before sample ingest");
            var token = JObject.Parse(pair.Content.ReadAsStringAsync().GetAwaiter().GetResult())["token"]?.ToString() ?? "";
            client.DefaultRequestHeaders.Add("X-Koko-Bridge-Token", token);
            var sample = new KokoWearableSample
            {
                TimestampUtc = DateTime.UtcNow,
                DeviceId = "galaxy-watch-8-lte",
                Source = "wear-os-bridge",
                HeartRateBpm = 78,
                Motion = 0.12,
                OnWrist = true,
                Activity = "walking",
                SemanticLocation = "outside",
                SpO2Percent = 97,
                BatteryPercent = 84
            };
            var json = JsonConvert.SerializeObject(sample);
            var response = client.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/sample",
                    new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();

            AssertTrue(response.IsSuccessStatusCode, $"bridge POST should succeed: {(int)response.StatusCode}");
            var state = telemetry.State;
            AssertEqual("galaxy-watch-8-lte", state.DeviceId, "bridge should ingest device id");
            AssertEqual("outside", state.SemanticLocation, "bridge should ingest semantic location");
            AssertTrue(Math.Abs(state.CurrentBpm - 78) < 0.1, "bridge should ingest heart rate");
            AssertTrue(Math.Abs((state.SpO2Percent ?? 0) - 97) < 0.1, "bridge should ingest SpO2 extension field");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableBridgeRejectsUnpairedSample()
    {
        var dir = TempDir();
        var port = 19300 + Random.Shared.Next(500);
        try
        {
            var telemetry = new KokoWearableTelemetryService(dir);
            using var bridge = new KokoWearableBridgeService(telemetry, dir, port);
            bridge.Start();
            AssertTrue(bridge.IsRunning, $"bridge should start; error={bridge.LastError}");

            using var client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.Add("X-Koko-Bridge-Token", bridge.Token);
            var response = client.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/sample",
                    new System.Net.Http.StringContent("""{"deviceId":"live-smoke-watch-b","heartRateBpm":103,"onWrist":true}""", System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();

            AssertEqual(System.Net.HttpStatusCode.Forbidden, response.StatusCode, "sample must be rejected until a device is paired");
            var json = JObject.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            AssertEqual("device_not_paired", json["error"]?.ToString(), "unpaired sample should report a clear error");
            AssertTrue(telemetry.State.CurrentBpm <= 0, "unpaired sample must not mutate wearable BPM");
            AssertEqual(0L, bridge.Diagnostics.TotalSamples, "unpaired sample must not count as accepted telemetry");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableBridgeIngestsAuthorizedSampleBatch()
    {
        var dir = TempDir();
        var port = 19500 + Random.Shared.Next(500);
        try
        {
            var telemetry = new KokoWearableTelemetryService(dir);
            using var bridge = new KokoWearableBridgeService(telemetry, dir, port);
            bridge.Start();
            AssertTrue(bridge.IsRunning, $"bridge should start; error={bridge.LastError}");

            using var client = new System.Net.Http.HttpClient();
            var pair = client.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/pair",
                    new System.Net.Http.StringContent("""{"deviceId":"batch-watch"}""", System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();
            AssertTrue(pair.IsSuccessStatusCode, "pair should succeed before batch ingest");
            var token = JObject.Parse(pair.Content.ReadAsStringAsync().GetAwaiter().GetResult())["token"]?.ToString() ?? "";
            client.DefaultRequestHeaders.Add("X-Koko-Bridge-Token", token);
            var batch = new[]
            {
                new KokoWearableSample
                {
                    TimestampUtc = DateTime.UtcNow.AddSeconds(-10),
                    DeviceId = "batch-watch",
                    HeartRateBpm = 71,
                    Motion = 0.11,
                    OnWrist = true,
                    SemanticLocation = "home"
                },
                new KokoWearableSample
                {
                    TimestampUtc = DateTime.UtcNow,
                    DeviceId = "batch-watch",
                    HeartRateBpm = 74,
                    Motion = 0.18,
                    OnWrist = true,
                    SemanticLocation = "desk"
                }
            };

            var response = client.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/samples",
                    new System.Net.Http.StringContent(JsonConvert.SerializeObject(batch), System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();

            AssertTrue(response.IsSuccessStatusCode, $"batch POST should succeed: {(int)response.StatusCode}");
            var json = JObject.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            AssertEqual(2, json["count"]?.Value<int>() ?? 0, "batch response should report accepted sample count");
            AssertEqual("batch-watch", telemetry.State.DeviceId, "batch should ingest device id");
            AssertEqual("desk", telemetry.State.SemanticLocation, "batch should leave state at latest sample");
            AssertTrue(Math.Abs(telemetry.State.CurrentBpm - 74) < 0.1, "batch should ingest latest heart rate");
            AssertEqual(2L, bridge.Diagnostics.TotalSamples, "diagnostics should count each sample in batch");
            AssertEqual(1L, bridge.Diagnostics.TotalBatchRequests, "diagnostics should count batch requests");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableBridgeDedupesReplayedSampleBatch()
    {
        var dir = TempDir();
        var port = 19800 + Random.Shared.Next(500);
        try
        {
            var telemetry = new KokoWearableTelemetryService(dir);
            using var bridge = new KokoWearableBridgeService(telemetry, dir, port);
            bridge.Start();
            AssertTrue(bridge.IsRunning, $"bridge should start; error={bridge.LastError}");

            using var client = new System.Net.Http.HttpClient();
            var pair = client.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/pair",
                    new System.Net.Http.StringContent("""{"deviceId":"batch-watch"}""", System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();
            AssertTrue(pair.IsSuccessStatusCode, "pair should succeed before replay test");
            var token = JObject.Parse(pair.Content.ReadAsStringAsync().GetAwaiter().GetResult())["token"]?.ToString() ?? "";
            client.DefaultRequestHeaders.Add("X-Koko-Bridge-Token", token);
            var batch = new[]
            {
                new KokoWearableSample
                {
                    SampleId = "batch-dup-1",
                    TimestampUtc = DateTime.UtcNow.AddSeconds(-10),
                    DeviceId = "batch-watch",
                    HeartRateBpm = 71,
                    OnWrist = true
                },
                new KokoWearableSample
                {
                    SampleId = "batch-dup-2",
                    TimestampUtc = DateTime.UtcNow,
                    DeviceId = "batch-watch",
                    HeartRateBpm = 74,
                    OnWrist = true
                }
            };
            var content = JsonConvert.SerializeObject(batch);

            var first = client.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/samples",
                    new System.Net.Http.StringContent(content, System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();
            var replay = client.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/samples",
                    new System.Net.Http.StringContent(content, System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();

            AssertTrue(first.IsSuccessStatusCode, "first batch should succeed");
            AssertTrue(replay.IsSuccessStatusCode, "replayed batch should still be accepted as an idempotent retry");
            var replayJson = JObject.Parse(replay.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            AssertEqual(0, replayJson["count"]?.Value<int>() ?? -1, "replayed batch should accept zero new samples");
            AssertEqual(2L, bridge.Diagnostics.TotalSamples, "diagnostics should count only accepted samples");
            AssertEqual(2L, bridge.Diagnostics.TotalBatchRequests, "diagnostics should count both batch requests");
            AssertEqual(2L, bridge.Diagnostics.TotalDuplicateSamples, "diagnostics should count duplicate replayed samples");
            AssertEqual(2, telemetry.RecentSamples.Count, "telemetry should store each unique sample once");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableBridgeStatusExposesPairingMetadata()
    {
        var dir = TempDir();
        var port = 20000 + Random.Shared.Next(1000);
        try
        {
            var telemetry = new KokoWearableTelemetryService(dir);
            using var bridge = new KokoWearableBridgeService(telemetry, dir, port);
            bridge.Start();
            AssertTrue(bridge.IsRunning, $"bridge should start; error={bridge.LastError}");

            using var client = new System.Net.Http.HttpClient();
            var response = client.GetAsync($"http://localhost:{port}/api/wearable/v1/status")
                .GetAwaiter().GetResult();
            AssertTrue(response.IsSuccessStatusCode, $"status should succeed: {(int)response.StatusCode}");

            var json = JObject.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            AssertEqual("kokonoe-wearable-v1", json["bridge"]?.ToString(), "status should expose bridge marker");
            AssertEqual(bridge.PcId, json["pcId"]?.ToString(), "status should expose stable pc id");
            AssertTrue(json["pcName"]?.ToString()?.Length > 0, "status should expose pc name");
            AssertTrue(json["pairingAvailable"]?.Value<bool>() == true, "status should advertise pairing");
            AssertTrue(json["urls"] is JArray { Count: > 0 }, "status should expose candidate URLs");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableBridgeExposesExternalFallbackUrls()
    {
        var dir = TempDir();
        var port = 20500 + Random.Shared.Next(500);
        try
        {
            var telemetry = new KokoWearableTelemetryService(dir);
            using var bridge = new KokoWearableBridgeService(
                telemetry,
                dir,
                port,
                new[] { "192.168.1.10:8787", "http://192.168.0.10:8787", "http://192.168.1.10:8787" });
            bridge.Start();
            AssertTrue(bridge.IsRunning, $"bridge should start; error={bridge.LastError}");

            using var client = new System.Net.Http.HttpClient();
            var response = client.GetAsync($"http://localhost:{port}/api/wearable/v1/status")
                .GetAwaiter().GetResult();
            AssertTrue(response.IsSuccessStatusCode, $"status should succeed: {(int)response.StatusCode}");

            var json = JObject.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            var external = (JArray?)json["externalUrls"] ?? new JArray();
            AssertTrue(external.Any(x => x?.ToString() == "http://192.168.1.10:8787"), "external URLs should normalize missing scheme");
            AssertTrue(external.Any(x => x?.ToString() == "http://192.168.0.10:8787"), "external URLs should preserve explicit URL");
            AssertEqual(2, external.Select(x => x?.ToString()).Distinct().Count(), "external URLs should de-duplicate entries");
            AssertTrue(((JArray?)json["urls"] ?? new JArray()).Any(x => x?.ToString() == "http://192.168.0.10:8787"), "status URLs should include external fallbacks");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableBridgeDiagnosticsTrackTraffic()
    {
        var dir = TempDir();
        var port = 21200 + Random.Shared.Next(500);
        try
        {
            var telemetry = new KokoWearableTelemetryService(dir);
            using var bridge = new KokoWearableBridgeService(telemetry, dir, port);
            bridge.Start();
            AssertTrue(bridge.IsRunning, $"bridge should start; error={bridge.LastError}");

            using var client = new System.Net.Http.HttpClient();
            var status = client.GetAsync($"http://localhost:{port}/api/wearable/v1/status")
                .GetAwaiter().GetResult();
            AssertTrue(status.IsSuccessStatusCode, "status should be reachable");

            var pair = client.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/pair",
                    new System.Net.Http.StringContent("""{"deviceId":"diag-watch"}""", System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();
            AssertTrue(pair.IsSuccessStatusCode, "pair should succeed");
            var token = JObject.Parse(pair.Content.ReadAsStringAsync().GetAwaiter().GetResult())["token"]?.ToString() ?? "";

            using var badClient = new System.Net.Http.HttpClient();
            badClient.DefaultRequestHeaders.Add("X-Koko-Bridge-Token", "bad");
            var bad = badClient.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/sample",
                    new System.Net.Http.StringContent("""{"deviceId":"diag-watch","heartRateBpm":80}""", System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();
            AssertEqual(System.Net.HttpStatusCode.Unauthorized, bad.StatusCode, "bad token should be counted as auth failure");

            client.DefaultRequestHeaders.Add("X-Koko-Bridge-Token", token);
            var sample = client.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/sample",
                    new System.Net.Http.StringContent("""{"sampleId":"diag-sample-1","deviceId":"diag-watch","heartRateBpm":81,"onWrist":true}""", System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();
            AssertTrue(sample.IsSuccessStatusCode, "authorized sample should succeed");

            bridge.QueueCommand("clear_queue");
            var command = client.GetAsync($"http://localhost:{port}/api/wearable/v1/command?deviceId=diag-watch")
                .GetAwaiter().GetResult();
            AssertTrue(command.IsSuccessStatusCode, "authorized command poll should succeed");
            var commandJson = JObject.Parse(command.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            AssertEqual("clear_queue", commandJson["action"]?.ToString(), "queued command should be delivered");
            var commandId = commandJson["commandId"]?.ToString() ?? "";
            var ack = client.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/command/ack",
                    new System.Net.Http.StringContent(
                        JsonConvert.SerializeObject(new { commandId, action = "clear_queue", ok = true, detail = "queued samples cleared" }),
                        System.Text.Encoding.UTF8,
                        "application/json"))
                .GetAwaiter().GetResult();
            AssertTrue(ack.IsSuccessStatusCode, "command ack should succeed");

            var diagnostics = bridge.Diagnostics;
            AssertTrue(diagnostics.TotalStatusRequests >= 1, "diagnostics should count status requests");
            AssertEqual(1L, diagnostics.TotalPairRequests, "diagnostics should count pair requests");
            AssertEqual(1L, diagnostics.TotalSamples, "diagnostics should count authorized samples");
            AssertEqual(1L, diagnostics.TotalAuthFailures, "diagnostics should count auth failures");
            AssertEqual(1L, diagnostics.TotalCommandPolls, "diagnostics should count command polls");
            AssertEqual(1L, diagnostics.TotalCommandAcks, "diagnostics should count command acknowledgements");
            AssertEqual("diag-watch", diagnostics.LastDeviceId, "diagnostics should remember last sample device");
            AssertEqual("diag-watch", diagnostics.LastPairedDeviceId, "diagnostics should remember last paired device");
            AssertEqual("diag-sample-1", diagnostics.LastAcceptedSampleId, "diagnostics should remember last accepted sample id");
            AssertEqual("clear_queue", diagnostics.LastQueuedCommandAction, "diagnostics should remember last queued command action");
            AssertEqual("clear_queue", diagnostics.LastDeliveredCommandAction, "diagnostics should remember last delivered command action");
            AssertEqual("clear_queue", diagnostics.LastAckAction, "diagnostics should remember last ack action");
            AssertTrue(diagnostics.LastAckOk, "diagnostics should remember successful ack status");
            AssertTrue(diagnostics.LastQueuedCommandAtUtc.HasValue, "diagnostics should record last queued command time");
            AssertTrue(diagnostics.LastDeliveredCommandAtUtc.HasValue, "diagnostics should record last delivered command time");
            AssertTrue(diagnostics.LastAuthorizedAtUtc.HasValue, "diagnostics should record last authorized request time");
            AssertTrue(diagnostics.LastCommandAckAtUtc.HasValue, "diagnostics should record last ack time");
            AssertTrue(!string.IsNullOrWhiteSpace(diagnostics.LastRemoteEndpoint), "diagnostics should remember remote endpoint");
            AssertTrue(diagnostics.LastSampleAtUtc.HasValue, "diagnostics should record last sample time");
            var snapshot = bridge.GetConnectionSnapshot(telemetry.State);
            AssertEqual("LINKED", snapshot.State, "connection snapshot should classify active authorized traffic as linked");
            AssertTrue(snapshot.IsLinked, "connection snapshot should mark active bridge as linked");
            AssertTrue(snapshot.IsPaired, "connection snapshot should mark paired watch");

            var statusAfter = client.GetAsync($"http://localhost:{port}/api/wearable/v1/status")
                .GetAwaiter().GetResult();
            var statusJson = JObject.Parse(statusAfter.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            AssertTrue(statusJson["diagnostics"]?["totalSamples"]?.Value<long>() >= 1, "status JSON should expose diagnostics");
            AssertEqual("diag-sample-1", statusJson["diagnostics"]?["lastAcceptedSampleId"]?.ToString(), "status JSON should expose accepted sample id");
            AssertEqual("clear_queue", statusJson["diagnostics"]?["lastDeliveredCommandAction"]?.ToString(), "status JSON should expose delivered command action");
            AssertTrue(!string.IsNullOrWhiteSpace(statusJson["diagnostics"]?["lastAuthorizedAtUtc"]?.ToString()), "status JSON should expose authorized time");
            AssertEqual("clear_queue", statusJson["diagnostics"]?["lastAckAction"]?.ToString(), "status JSON should expose ack action");
            AssertEqual("LINKED", statusJson["connection"]?["state"]?.ToString(), "status JSON should expose bridge connection state");
            AssertTrue(statusJson["connection"]?["isLinked"]?.Value<bool>() == true, "status JSON should expose linked flag");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableBridgeEmulatesDualRealtimePulseClients()
    {
        var dir = TempDir();
        var port = 21700 + Random.Shared.Next(700);
        try
        {
            var telemetry = new KokoWearableTelemetryService(dir);
            using var bridge = new KokoWearableBridgeService(telemetry, dir, port);
            bridge.Start();
            AssertTrue(bridge.IsRunning, $"bridge should start; error={bridge.LastError}");

            using var pairClient = new System.Net.Http.HttpClient();
            var pair = pairClient.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/pair",
                    new System.Net.Http.StringContent("""{"deviceId":"em-watch-a"}""", System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();
            AssertTrue(pair.IsSuccessStatusCode, $"pair should succeed: {(int)pair.StatusCode}");
            var token = JObject.Parse(pair.Content.ReadAsStringAsync().GetAwaiter().GetResult())["token"]?.ToString() ?? "";
            AssertTrue(!string.IsNullOrWhiteSpace(token), "pair should return a bridge token");

            Task SendPulseTrainAsync(string deviceId, int offset, double[] bpms) => Task.Run(async () =>
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("X-Koko-Bridge-Token", token);
                for (var i = 0; i < bpms.Length; i++)
                {
                    var sample = new KokoWearableSample
                    {
                        SampleId = $"{deviceId}-{offset + i}",
                        TimestampUtc = DateTime.UtcNow.AddMilliseconds(offset + i),
                        DeviceId = deviceId,
                        Source = "emulated-watch-client",
                        HeartRateBpm = bpms[i],
                        Motion = 0.14 + i * 0.03,
                        OnWrist = true,
                        Activity = "heart_realtime:emulated",
                        SemanticLocation = "integration-test",
                        BatteryPercent = 88 - i
                    };
                    var response = await client.PostAsync(
                        $"http://localhost:{port}/api/wearable/v1/sample",
                        new System.Net.Http.StringContent(JsonConvert.SerializeObject(sample), System.Text.Encoding.UTF8, "application/json"));
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException($"pulse push failed for {deviceId}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
                }
            });

            Task.WhenAll(
                    SendPulseTrainAsync("em-watch-a", 100, new[] { 72d, 75d, 92d, 104d, 110d }),
                    SendPulseTrainAsync("em-watch-a", 200, new[] { 68d, 70d, 88d, 96d, 99d }))
                .GetAwaiter().GetResult();

            using var finalClient = new System.Net.Http.HttpClient();
            finalClient.DefaultRequestHeaders.Add("X-Koko-Bridge-Token", token);
            var finalSample = new KokoWearableSample
            {
                SampleId = "em-watch-a-final",
                TimestampUtc = DateTime.UtcNow.AddSeconds(1),
                DeviceId = "em-watch-a",
                Source = "emulated-watch-client",
                HeartRateBpm = 111,
                Motion = 0.22,
                OnWrist = true,
                Activity = "heart_realtime:emulated",
                SemanticLocation = "integration-test",
                BatteryPercent = 82
            };
            var finalPush = finalClient.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/sample",
                    new System.Net.Http.StringContent(JsonConvert.SerializeObject(finalSample), System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();
            AssertTrue(finalPush.IsSuccessStatusCode, $"final pulse push should succeed: {(int)finalPush.StatusCode}");

            var status = finalClient.GetAsync($"http://localhost:{port}/api/wearable/v1/status")
                .GetAwaiter().GetResult();
            AssertTrue(status.IsSuccessStatusCode, $"status should succeed after pulse train: {(int)status.StatusCode}");
            var statusJson = JObject.Parse(status.Content.ReadAsStringAsync().GetAwaiter().GetResult());

            AssertEqual(11L, bridge.Diagnostics.TotalSamples, "bridge should count parallel paired-device clients plus final pulse");
            AssertEqual(11, telemetry.RecentSamples.Count, "telemetry should store every unique emulated pulse sample");
            AssertEqual("em-watch-a", telemetry.State.DeviceId, "final pulse should make state deterministic");
            AssertTrue(Math.Abs(telemetry.State.CurrentBpm - 111) < 0.1, "final emulated BPM should become current wearable BPM");
            AssertTrue(telemetry.State.LiveStressScore > 0, "emulated elevated BPM should produce a non-zero live stress score");
            AssertEqual("LINKED", statusJson["connection"]?["state"]?.ToString(), "status should report linked connection after real HTTP pushes");
            AssertTrue(statusJson["diagnostics"]?["totalSamples"]?.Value<long>() == 11, "status JSON should expose real accepted pulse count");
            AssertTrue(File.Exists(telemetry.LogPath), "telemetry log file should be created");

            var logText = string.Join("\n", telemetry.RecentLogLines(80));
            AssertTrue(logText.Contains("[WEARABLE] Sample Accepted: BPM=111", StringComparison.OrdinalIgnoreCase), "log should contain final accepted pulse line");
            AssertTrue(logText.Contains("Device=em-watch-a", StringComparison.OrdinalIgnoreCase), "log should contain first emulated device id");
            AssertTrue(!logText.Contains("Device=em-watch-b", StringComparison.OrdinalIgnoreCase), "mismatched emulated device id should not appear in accepted telemetry");
            AssertTrue(logText.Contains("[WEARABLE-BRIDGE] sample accepted", StringComparison.OrdinalIgnoreCase), "bridge operational log should be written to telemetry log");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableBridgePairsAndRejectsBadTokens()
    {
        var dir = TempDir();
        var port = 21000 + Random.Shared.Next(1000);
        try
        {
            var telemetry = new KokoWearableTelemetryService(dir);
            using var bridge = new KokoWearableBridgeService(telemetry, dir, port);
            bridge.Start();
            AssertTrue(bridge.IsRunning, $"bridge should start; error={bridge.LastError}");

            using var client = new System.Net.Http.HttpClient();
            var pairResponse = client.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/pair",
                    new System.Net.Http.StringContent("""{"deviceId":"watch-test"}""", System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();
            AssertTrue(pairResponse.IsSuccessStatusCode, $"pair should succeed: {(int)pairResponse.StatusCode}");
            var pairJson = JObject.Parse(pairResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            var token = pairJson["token"]?.ToString() ?? "";
            AssertEqual(bridge.PcId, pairJson["pcId"]?.ToString(), "pair should bind to this pc id");
            AssertEqual(bridge.Token, token, "pair should return bridge token");

            var badClient = new System.Net.Http.HttpClient();
            badClient.DefaultRequestHeaders.Add("X-Koko-Bridge-Token", "bad");
            var badResponse = badClient.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/sample",
                    new System.Net.Http.StringContent("""{"deviceId":"watch-test","heartRateBpm":77}""", System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();
            AssertEqual(System.Net.HttpStatusCode.Unauthorized, badResponse.StatusCode, "wrong-length token should reject without crashing");

            client.DefaultRequestHeaders.Add("X-Koko-Bridge-Token", token);
            var goodResponse = client.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/sample",
                    new System.Net.Http.StringContent("""{"deviceId":"watch-test","heartRateBpm":77,"onWrist":true}""", System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();
            AssertTrue(goodResponse.IsSuccessStatusCode, $"paired token should post sample: {(int)goodResponse.StatusCode}");
            AssertTrue(Math.Abs(telemetry.State.CurrentBpm - 77) < 0.1, "paired token sample should ingest bpm");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableBridgeKeepsStableTokenAndPcId()
    {
        var dir = TempDir();
        try
        {
            var telemetry = new KokoWearableTelemetryService(dir);
            using var bridge1 = new KokoWearableBridgeService(telemetry, dir, 22000 + Random.Shared.Next(1000));
            var token1 = bridge1.Token;
            var pcId1 = bridge1.PcId;

            using var bridge2 = new KokoWearableBridgeService(telemetry, dir, 23000 + Random.Shared.Next(1000));
            AssertEqual(token1, bridge2.Token, "token should persist across bridge instances");
            AssertEqual(pcId1, bridge2.PcId, "pc id should persist across bridge instances");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableBridgeRestoresPairedDeviceId()
    {
        var dir = TempDir();
        var port1 = 23500 + Random.Shared.Next(400);
        var port2 = 23900 + Random.Shared.Next(400);
        try
        {
            var telemetry = new KokoWearableTelemetryService(dir);
            using (var bridge1 = new KokoWearableBridgeService(telemetry, dir, port1))
            {
                bridge1.Start();
                AssertTrue(bridge1.IsRunning, $"bridge should start; error={bridge1.LastError}");
                using var client = new System.Net.Http.HttpClient();
                var pair = client.PostAsync(
                        $"http://localhost:{port1}/api/wearable/v1/pair",
                        new System.Net.Http.StringContent("""{"deviceId":"galaxy-watch-8-lte"}""", System.Text.Encoding.UTF8, "application/json"))
                    .GetAwaiter().GetResult();
                AssertTrue(pair.IsSuccessStatusCode, "pair should succeed before restart");
                AssertEqual("galaxy-watch-8-lte", bridge1.Diagnostics.LastPairedDeviceId, "first bridge should remember paired device");
            }

            using var bridge2 = new KokoWearableBridgeService(telemetry, dir, port2);
            AssertEqual("galaxy-watch-8-lte", bridge2.Diagnostics.LastPairedDeviceId, "paired device should persist across bridge restart");

            bridge2.Start();
            using var client2 = new System.Net.Http.HttpClient();
            client2.DefaultRequestHeaders.Add("X-Koko-Bridge-Token", bridge2.Token);
            var sample = client2.PostAsync(
                    $"http://localhost:{port2}/api/wearable/v1/sample",
                    new System.Net.Http.StringContent("""{"deviceId":"galaxy-watch-8-lte","heartRateBpm":81,"onWrist":true}""", System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();
            AssertTrue(sample.IsSuccessStatusCode, $"sample should be accepted after restart: {(int)sample.StatusCode}");
            AssertTrue(Math.Abs(telemetry.State.CurrentBpm - 81) < 0.1, "restored pairing should allow telemetry ingest");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableBridgeMigratesPairedDeviceFromTelemetry()
    {
        var dir = TempDir();
        try
        {
            var telemetry = new KokoWearableTelemetryService(dir);
            telemetry.IngestDetailed(new KokoWearableSample
            {
                DeviceId = "galaxy-watch-8-lte",
                HeartRateBpm = 79,
                OnWrist = true,
                TimestampUtc = DateTime.UtcNow
            });

            using var bridge = new KokoWearableBridgeService(telemetry, dir, 24300 + Random.Shared.Next(400));
            AssertEqual("galaxy-watch-8-lte", bridge.Diagnostics.LastPairedDeviceId, "bridge should migrate paired device from persisted wearable state");
            AssertTrue(File.Exists(Path.Combine(dir, "wearable-bridge-paired-device.txt")), "migration should create paired device file");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void WearableTrustRejectsUnpairedFreshSamples()
    {
        var wearable = new KokoWearableState
        {
            LastSampleUtc = DateTime.UtcNow,
            DeviceId = "galaxy-watch-8-lte",
            CurrentBpm = 88,
            BaselineBpm = 72,
            SampleSource = "wear-os-bridge"
        };
        var connection = new KokoWearableBridgeService.WearableBridgeConnectionSnapshot
        {
            State = "WAITING_FOR_PAIR",
            IsLinked = true,
            TelemetryFresh = true,
            SampleRecent = true
        };
        var diagnostics = new KokoWearableBridgeService.WearableBridgeDiagnostics
        {
            LastPairedDeviceId = "",
            LastAcceptedSampleId = "sample-1"
        };

        AssertTrue(!KokoWearableTrust.IsVerified(connection, diagnostics, wearable), "fresh samples must stay untrusted without a paired watch");
        AssertEqual("no paired Galaxy Watch", KokoWearableTrust.BlockReason(connection, diagnostics, wearable), "block reason should expose pairing truth");
    }

    private static void WearableBridgeAcceptsWatchActionEndpoint()
    {
        var dir = TempDir();
        var port = 24000 + Random.Shared.Next(1000);
        try
        {
            var telemetry = new KokoWearableTelemetryService(dir);
            using var bridge = new KokoWearableBridgeService(telemetry, dir, port);
            bridge.Start();
            AssertTrue(bridge.IsRunning, $"bridge should start; error={bridge.LastError}");

            KokoWearableBridgeService.WearableActionRequest? received = null;
            using var actionReceived = new System.Threading.ManualResetEventSlim(false);
            bridge.ActionReceived += action =>
            {
                received = action;
                actionReceived.Set();
            };

            using var client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.Add("X-Koko-Bridge-Token", bridge.Token);
            var response = client.PostAsync(
                    $"http://localhost:{port}/api/wearable/v1/action",
                    new System.Net.Http.StringContent("""{"action":"look_screen_now","payload":"from watch","deviceId":"watch-action"}""", System.Text.Encoding.UTF8, "application/json"))
                .GetAwaiter().GetResult();
            AssertTrue(response.IsSuccessStatusCode, $"watch action should succeed: {(int)response.StatusCode}");
            AssertTrue(actionReceived.Wait(TimeSpan.FromSeconds(2)), "watch action event should be raised");
            AssertEqual("look_screen_now", received?.Action, "action should round-trip");
            AssertEqual("watch-action", received?.DeviceId, "device id should round-trip");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void HeartbeatDashboardWritesMarkdownAndHtml()
    {
        var dir = TempDir();
        try
        {
            var heartbeat = new KokoServiceHeartbeatService(dir);
            heartbeat.Update("WATCH", "OK", "samples fresh");
            heartbeat.Update("VISION", "SCANNING", "mini OCR");

            AssertTrue(File.Exists(heartbeat.MarkdownPath), "heartbeat markdown should be written");
            AssertTrue(File.Exists(heartbeat.HtmlPath), "heartbeat html should be written");
            var md = File.ReadAllText(heartbeat.MarkdownPath);
            AssertTrue(md.Contains("WATCH") && md.Contains("VISION"), "heartbeat markdown should list services");
            var html = File.ReadAllText(heartbeat.HtmlPath);
            AssertTrue(html.Contains("KOKONOE SERVICE HEARTBEAT"), "heartbeat html should have title");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void SemanticCacheReturnsSimilarCachedAnswer()
    {
        var dir = TempDir();
        try
        {
            var cache = new KokoSemanticCacheService(dir);
            cache.Put("як працює wearable bridge між годинником і пк", "Bridge приймає samples з годинника, перевіряє token і оновлює telemetry state.");
            AssertTrue(cache.TryGet("як саме працює wearable bridge між годинником і моїм пк", out var answer), "similar query should hit semantic cache");
            AssertTrue(answer.Contains("samples", StringComparison.OrdinalIgnoreCase), "cached answer should return stored content");
        }
        finally { TryDeleteDir(dir); }
    }

    private static void CapabilityManifestAdvertisesRuntimeRoutes()
    {
        var manifest = new KokoCapabilityManifestService().BuildPromptBlock();

        AssertTrue(manifest.Contains("RUNTIME CAPABILITY MANIFEST"), "manifest should have a stable heading");
        AssertTrue(manifest.Contains("Obsidian/Vault"), "manifest should advertise vault routes");
        AssertTrue(manifest.Contains("Screen/Vision"), "manifest should advertise screen and vision routes");
        AssertTrue(manifest.Contains("PC control"), "manifest should advertise local PC control");
        AssertTrue(manifest.Contains("Wearable telemetry"), "manifest should advertise wearable telemetry");
        AssertTrue(manifest.Contains("never claim a capability is absent"), "manifest should block helpless stock replies");
    }

    private static void ServiceContainerDiPreservesCoreSingletonIdentity()
    {
        var dir = TempDir();
        try
        {
            ServiceContainer.Initialize(dir, startHostedServices: false);
            var providerField = typeof(ServiceContainer).GetField(
                "_serviceProvider",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("DI provider field was not found.");
            var provider = providerField.GetValue(null) as IServiceProvider
                ?? throw new InvalidOperationException("DI provider was not initialized.");

            AssertTrue(ReferenceEquals(provider.GetService(typeof(IKokoEmotionEngine)), ServiceContainer.EmotionEngine),
                "emotion interface and legacy facade must resolve the same singleton");
            AssertTrue(ReferenceEquals(provider.GetService(typeof(IKokoMemoryEngine)), ServiceContainer.KokoMemory),
                "memory interface and legacy facade must resolve the same singleton");
            AssertTrue(ReferenceEquals(provider.GetService(typeof(IKokoInternalBlackboardService)), ServiceContainer.Blackboard),
                "blackboard interface and legacy facade must resolve the same singleton");
            AssertTrue(ReferenceEquals(provider.GetService(typeof(IKokoProfileUpdateService)), ServiceContainer.ProfileUpdater),
                "profile interface and legacy facade must resolve the same singleton");
            AssertTrue(ReferenceEquals(provider.GetService(typeof(ILlmService)), provider.GetService(typeof(LlmService))),
                "LLM interface and concrete registration must resolve the same singleton");
            AssertTrue(ReferenceEquals(ServiceContainer.BrainEngine.Emotion, ServiceContainer.EmotionEngine),
                "brain must use the DI emotion singleton instead of creating a split state");
            AssertTrue(ReferenceEquals(ServiceContainer.BrainEngine.Memory, ServiceContainer.KokoMemory),
                "brain must use the DI memory singleton instead of creating a split state");

            var firstEmotion = ServiceContainer.EmotionEngine;
            AssertTrue(ServiceContainer.IsInitialized, "container must report initialized while provider is alive");
            ServiceContainer.Disposing();
            AssertTrue(!ServiceContainer.IsInitialized, "disposing must reset initialization state");

            ServiceContainer.Initialize(dir, startHostedServices: false);
            AssertTrue(!ReferenceEquals(firstEmotion, ServiceContainer.EmotionEngine),
                "reinitialization after disposal must create a fresh singleton graph");
        }
        finally
        {
            ServiceContainer.Disposing();
            TryDeleteDir(dir);
        }
    }

    private static void SelfRegulationClampsPulseSpike()
    {
        using var ctx = TestContext.Create();
        var state = new KokoSelfRegulationState { LastPulseSpikeAt = DateTime.Now.AddHours(-1) };
        var somatic = new KokoSomaticSnapshot
        {
            State = "wired",
            Bpm = 98,
            BaselineBpm = 62,
            BpmDelta = 36,
            Strain = 0.88,
            Calm = 0.08,
            Volatility = 0.92
        };

        var frame = new KokoSomaticSelfRegulationEngine()
            .Evaluate(state, somatic, ctx.Emotion, ctx.Relationship, "neutral", DateTime.Now);

        AssertEqual("pulse_spike", frame.Reaction, "wired spike should classify as pulse_spike");
        AssertEqual("clamp", frame.Regulation, "pulse_spike should clamp");
        AssertTrue(frame.ShouldNarrowResponse, "pulse_spike should narrow responses");
        AssertTrue(state.Reactions.Count == 1, "reaction should be recorded");
    }

    private static void SelfRegulationProtectsVulnerableTone()
    {
        using var ctx = TestContext.Create();
        var somatic = new KokoSomaticSnapshot
        {
            State = "strained",
            Bpm = 82,
            BaselineBpm = 64,
            BpmDelta = 18,
            Strain = 0.68,
            Calm = 0.20,
            Volatility = 0.55
        };

        var frame = new KokoSomaticSelfRegulationEngine()
            .Evaluate(new KokoSelfRegulationState(), somatic, ctx.Emotion, ctx.Relationship, "vulnerable", DateTime.Now);

        AssertEqual("protective_override", frame.Reaction, "vulnerable tone should override body reaction");
        AssertEqual("protect", frame.Regulation, "protective override should protect");
        AssertTrue(frame.ShouldSuppressSnark, "protective mode should suppress snark");
        AssertTrue(frame.ShouldProtect, "protective mode should set protect flag");
    }

    private static void InitiativeRespectsLowPowerSilence()
    {
        using var ctx = TestContext.Create();
        var decision = new KokoInitiativeEngine().Evaluate(
            DateTime.Now,
            new KokoInternalState(),
            ctx.Emotion,
            ctx.Relationship,
            ctx.Memory,
            ctx.Chat,
            somatic: new KokoSomaticSnapshot { State = "tired", Strain = 0.20, Calm = 0.40 },
            selfRegulation: new KokoSelfRegulationFrame
            {
                Reaction = "low_power",
                Regulation = "conserve",
                ShouldPreferSilence = true
            });

        AssertTrue(!decision.ShouldAct, "low-power self-regulation should block weak initiative");
        AssertEqual("self_regulation_silence", decision.Trigger, "silence trigger should be explicit");
    }

    private static void InitiativeReactsToProtectiveOverride()
    {
        using var ctx = TestContext.Create();
        var state = new KokoInternalState { LastSpontaneousAt = DateTime.Now.AddHours(-2) };
        var decision = new KokoInitiativeEngine().Evaluate(
            DateTime.Now,
            state,
            ctx.Emotion,
            ctx.Relationship,
            ctx.Memory,
            ctx.Chat,
            somatic: new KokoSomaticSnapshot { State = "strained", Strain = 0.70, Calm = 0.20 },
            selfRegulation: new KokoSelfRegulationFrame
            {
                Reaction = "protective_override",
                Regulation = "protect",
                ShouldProtect = true,
                BehaviorDirective = "protective, short, no mockery"
            });

        AssertTrue(decision.ShouldAct, "protective override should create initiative");
        AssertEqual("self_regulation_protect", decision.Trigger, "protect trigger should win");
    }

    private static void InitiativeHighAutonomyAsksCuriositySooner()
    {
        using var ctx = TestContext.Create();
        var now = DateTime.Now;
        var state = new KokoInternalState
        {
            LastSpontaneousAt = now.AddMinutes(-40),
            LastCuriosityAskAt = now.AddMinutes(-90)
        };
        state.CuriosityQueue.Add("Чому ти знову відкладаєш сон?");

        var cautious = new KokoInitiativeEngine().Evaluate(
            now,
            state,
            ctx.Emotion,
            ctx.Relationship,
            ctx.Memory,
            ctx.Chat,
            autonomyLevel: 2);
        AssertTrue(!cautious.ShouldAct, "normal autonomy should wait for the longer curiosity cooldown");

        var active = new KokoInitiativeEngine().Evaluate(
            now,
            state,
            ctx.Emotion,
            ctx.Relationship,
            ctx.Memory,
            ctx.Chat,
            autonomyLevel: 3);
        AssertTrue(active.ShouldAct, "high autonomy should allow earlier curiosity pings");
        AssertEqual("curiosity_ping", active.Trigger, "high autonomy should use the curiosity ping trigger");
    }

    private static void ShortTermIntentFollowupBypassesOrdinaryInitiative()
    {
        using var ctx = TestContext.Create();
        var now = DateTime.Now;
        var state = new KokoInternalState
        {
            LastSpontaneousAt = now.AddMinutes(-5)
        };
        state.PendingTriggers.Add(new ReactiveTrigger
        {
            Type = "intent_followup",
            Context = "Користувач сказав: «я піду на курси». Намір: пішов на курси/заняття.",
            FireAt = now.AddMinutes(-1)
        });

        var ordinary = new KokoInitiativeEngine().Evaluate(
            now,
            state,
            ctx.Emotion,
            ctx.Relationship,
            ctx.Memory,
            ctx.Chat,
            autonomyLevel: 3);

        AssertTrue(ordinary.ShouldAct, "due short-term intent followup should outrank normal cooldown");
        AssertEqual("reactive_followup", ordinary.Trigger, "due intent followup should use reactive followup trigger");
    }

    private static void PresenceDetectsOverdueCourseFollowup()
    {
        using var ctx = TestContext.Create();
        var now = new DateTime(2026, 5, 6, 18, 0, 0);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "course",
            Summary = "пішов на курси/заняття",
            SourceText = "я піду на курси",
            CreatedAt = now.AddHours(-3),
            FollowUpAt = now.AddHours(-2),
            ExpectedUntil = now.AddHours(-1)
        });
        ctx.Chat.InsertMessage(new ChatRepository.ChatMessage
        {
            Role = "user",
            Content = "я піду на курси",
            Timestamp = now.AddHours(-3)
        });

        var frame = new KokoPresenceContinuityEngine()
            .Evaluate(
                state,
                ctx.Chat.GetMessages(10),
                now,
                autonomyLevel: 3,
                systemInfoOverride: new SystemInfo { IdleTime = TimeSpan.Zero });

        AssertTrue(frame.ShouldInterrupt, "overdue course followup should interrupt at high autonomy");
        AssertEqual("overdue_intent", frame.SituationKind, "course should be classified as overdue intent");
        AssertTrue(frame.ExtraContext.Contains("курси"), "presence context should preserve the course event");
    }

    private static void PresenceWaitsForReturnHomeIntent()
    {
        using var ctx = TestContext.Create();
        var now = new DateTime(2026, 5, 7, 11, 57, 0);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "return_home",
            Summary = "має бути вдома близько 12:00",
            SourceText = "Буду в 12 дома крч",
            CreatedAt = now.AddMinutes(-35),
            FollowUpAt = now.Date.AddHours(12).AddMinutes(12),
            ExpectedUntil = now.Date.AddHours(12)
        });
        ctx.Chat.InsertMessage(new ChatRepository.ChatMessage
        {
            Role = "user",
            Content = "Буду в 12 дома крч",
            Timestamp = now.AddMinutes(-35)
        });

        var frame = new KokoPresenceContinuityEngine()
            .Evaluate(
                state,
                ctx.Chat.GetMessages(10),
                now,
                autonomyLevel: 3,
                systemInfoOverride: new SystemInfo { IdleTime = TimeSpan.Zero });

        AssertEqual("active_absence", frame.SituationKind, "return-home intent should stay active before the stated time");
        AssertTrue(!frame.ShouldInterrupt, "presence should not interrupt before the stated return-home follow-up");
        AssertTrue(frame.ToneHint.Contains("wait until that time"), "tone should explicitly avoid early nagging");
    }

    private static void PresenceTreatsActiveScreenIdleInputAsPresent()
    {
        var now = new DateTime(2026, 5, 24, 21, 0, 0);
        var state = new KokoInternalState
        {
            LastScreenAwarenessAt = now.AddMinutes(-2),
            LastScreenAwarenessMode = "media",
            LastScreenAwarenessActivity = "changed active video playback",
            LastScreenAwarenessSummary = "YouTube video is playing",
            LastScreenSituationProgress = "moving"
        };
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "я відійду від чату", Timestamp = now.AddMinutes(-20) }
        };

        var frame = new KokoPresenceContinuityEngine().Evaluate(
            state,
            messages,
            now,
            autonomyLevel: 3,
            systemInfoOverride: new SystemInfo { IdleTime = TimeSpan.FromMinutes(8) });

        AssertEqual("physically_present", frame.SituationKind, "active media with idle input should mean present, not away");
        AssertEqual("presence_screen_active_idle_input", frame.Trigger, "screen-active idle should use explicit trigger");
        AssertTrue(!frame.ShouldInterrupt, "watching/reading should not be interrupted as absence");
        AssertTrue(frame.ExtraContext.Contains("Screen presence", StringComparison.OrdinalIgnoreCase), "presence prompt should include screen signal");
    }

    private static void PresenceActiveScreenIdleCanInterruptAfterTenMinutes()
    {
        var now = new DateTime(2026, 5, 24, 21, 0, 0);
        var state = new KokoInternalState
        {
            LastPresenceInterruptAt = now.AddMinutes(-30),
            LastScreenAwarenessAt = now.AddMinutes(-2),
            LastScreenAwarenessMode = "media",
            LastScreenAwarenessActivity = "changed active video playback",
            LastScreenAwarenessSummary = "YouTube video is playing",
            LastScreenSituationProgress = "moving"
        };
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "watching something", Timestamp = now.AddMinutes(-20) }
        };

        var frame = new KokoPresenceContinuityEngine().Evaluate(
            state,
            messages,
            now,
            autonomyLevel: 2,
            systemInfoOverride: new SystemInfo { IdleTime = TimeSpan.FromMinutes(12) });

        AssertEqual("physically_present", frame.SituationKind, "active screen should still mean present");
        AssertEqual("presence_screen_active_idle_input", frame.Trigger, "active idle screen should keep explicit trigger");
        AssertTrue(frame.ShouldInterrupt, "active screen idle over ten minutes should allow a contextual observation");
        AssertTrue(state.LastPresenceAt == now, "silent presence evaluation should refresh LastPresenceAt");
    }

    private static void PresenceTreatsUnchangedScreenIdleInputAsAway()
    {
        var now = new DateTime(2026, 5, 24, 21, 0, 0);
        var state = new KokoInternalState
        {
            LastScreenAwarenessAt = now.AddMinutes(-3),
            LastScreenAwarenessMode = "desktop",
            LastScreenAwarenessActivity = "same idle desktop",
            LastScreenAwarenessSummary = "Desktop is unchanged",
            LastScreenSituationProgress = "idle"
        };
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "я тут", Timestamp = now.AddMinutes(-20) }
        };

        var frame = new KokoPresenceContinuityEngine().Evaluate(
            state,
            messages,
            now,
            autonomyLevel: 3,
            systemInfoOverride: new SystemInfo { IdleTime = TimeSpan.FromMinutes(8) });

        AssertEqual("away", frame.SituationKind, "idle input plus unchanged desktop should mean away");
        AssertEqual("presence_screen_idle_away", frame.Trigger, "screen-idle away should use explicit trigger");
        AssertTrue(!frame.ShouldInterrupt, "away state should not nag the user");
    }

    private static void PresenceTreatsStuckCodingIdleInputAsPresent()
    {
        var now = new DateTime(2026, 5, 24, 21, 0, 0);
        var state = new KokoInternalState
        {
            LastScreenAwarenessAt = now.AddMinutes(-4),
            LastScreenAwarenessMode = "coding",
            LastScreenAwarenessActivity = "same stuck exception in editor",
            LastScreenAwarenessSummary = "Visual Studio shows a repeated build exception",
            LastScreenSituationProgress = "stuck"
        };
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "я дебажу", Timestamp = now.AddMinutes(-40) }
        };

        var frame = new KokoPresenceContinuityEngine().Evaluate(
            state,
            messages,
            now,
            autonomyLevel: 3,
            systemInfoOverride: new SystemInfo { IdleTime = TimeSpan.FromMinutes(8) });

        AssertEqual("physically_present", frame.SituationKind, "stuck coding screen should mean staring/debugging, not away");
        AssertEqual("presence_screen_active_idle_input", frame.Trigger, "stuck content should use the screen-active idle trigger");
        AssertTrue(!frame.ShouldInterrupt, "stuck coding presence should not create a generic silence ping");
        AssertTrue(frame.SummaryUk.Contains("Ввід неактивний", StringComparison.Ordinal), "summary should be readable Ukrainian");
    }

    private static void PresenceTreatsActiveInputChatSilenceAsPresent()
    {
        var now = new DateTime(2026, 5, 24, 21, 0, 0);
        var state = new KokoInternalState { LastPresenceInterruptAt = now.AddHours(-9) };
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "я працюю, не чіпай чат", Timestamp = now.AddHours(-2) }
        };

        var frame = new KokoPresenceContinuityEngine().Evaluate(
            state,
            messages,
            now,
            autonomyLevel: 3,
            systemInfoOverride: new SystemInfo { IdleTime = TimeSpan.FromSeconds(20) });

        AssertEqual("physically_present", frame.SituationKind, "recent PC input should override generic long-chat-silence absence");
        AssertEqual("presence_pc_active_chat_silent", frame.Trigger, "active input silence should use an explicit presence trigger");
        AssertTrue(!frame.ShouldInterrupt, "active PC use without chat should not produce a generic long-silence ping");
        AssertTrue(frame.SummaryUk.Contains("ПК активний", StringComparison.Ordinal), "summary should explain active PC presence");
    }

    private static void AutonomyBlocksGenericPingDuringActiveIntent()
    {
        var now = new DateTime(2026, 5, 7, 11, 57, 0);
        var state = new KokoInternalState();
        var presence = new KokoPresenceFrame
        {
            SituationKind = "active_absence",
            SummaryUk = "Активний намір: має бути вдома близько 12:00.",
            ShouldInterrupt = false,
            NextUsefulAt = now.Date.AddHours(12).AddMinutes(12),
            SilenceMinutes = 35
        };
        var internalDay = new KokoInternalDayFrame
        {
            Phase = "work_ramp",
            SummaryUk = "робочий день",
            PromptBlock = "INTERNAL DAY\n",
            ShouldPreferSilence = false
        };
        var initiative = new KokoInitiativeDecision
        {
            ShouldAct = true,
            Trigger = "curiosity_ping",
            StyleHint = "jab",
            Reason = "generic curiosity",
            Priority = 80,
            ExtraContext = "generic"
        };

        var decision = new KokoAutonomyDecisionEngine().Evaluate(
            now,
            state,
            presence,
            internalDay,
            initiative,
            new KokoRelationshipState(),
            new KokoSomaticSnapshot { State = "focused", Calm = 0.30, Strain = 0.20 },
            new KokoPatternEngine.RhythmProfile { CurrentSlotSamples = 5, CurrentSlotActivityRate = 0.50f, Summary = "active slot" },
            autonomyLevel: 3);

        AssertTrue(!decision.ShouldAct, "generic initiative should be blocked while a timed user intent is active");
        AssertTrue(decision.SilenceReason.Contains("активний намір") || decision.SilenceReason.Contains("active"), "silence reason should mention active intent");
    }

    private static void PresenceRefusesStaleSleepInstructionAfterReturn()
    {
        using var ctx = TestContext.Create();
        var now = new DateTime(2026, 5, 6, 10, 30, 0);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "sleep",
            Summary = "пішов спати",
            SourceText = "я спать",
            CreatedAt = now.AddHours(-9),
            FollowUpAt = now.AddHours(-1),
            ExpectedUntil = now.AddMinutes(-20),
            ResolvedAt = now.AddMinutes(-5),
            ResolutionText = "прокинувся"
        });
        ctx.Chat.InsertMessage(new ChatRepository.ChatMessage
        {
            Role = "user",
            Content = "прокинувся",
            Timestamp = now.AddMinutes(-5)
        });

        var frame = new KokoPresenceContinuityEngine()
            .Evaluate(state, ctx.Chat.GetMessages(10), now, autonomyLevel: 3);

        AssertEqual("returned_after_intent", frame.SituationKind, "resolved sleep should be treated as return");
        AssertTrue(
            frame.ExtraContext.Contains("не кажи йому робити те, що вже в минулому") ||
            frame.ExtraContext.Contains("already woke up or returned"),
            "presence context should block stale sleep instruction");
        AssertTrue(frame.ToneHint.Contains("do not tell him to sleep"), "tone should explicitly avoid telling him to sleep again");
    }

    private static void PresenceNeverInterruptsActiveSleepIntent()
    {
        using var ctx = TestContext.Create();
        var now = new DateTime(2026, 5, 8, 16, 27, 0);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "sleep",
            Summary = "пішов спати/попрощався",
            SourceText = "Бай бай",
            CreatedAt = now.AddHours(-8),
            FollowUpAt = now.AddMinutes(-5),
            ExpectedUntil = now.AddHours(2)
        });
        ctx.Chat.InsertMessage(new ChatRepository.ChatMessage
        {
            Role = "user",
            Content = "Бай бай",
            Timestamp = now.AddHours(-8)
        });

        var frame = new KokoPresenceContinuityEngine()
            .Evaluate(state, ctx.Chat.GetMessages(10), now, autonomyLevel: 3);

        AssertEqual("sleeping", frame.SituationKind, "sleep should stay protected instead of becoming a due follow-up");
        AssertTrue(!frame.ShouldInterrupt, "sleep intent should never proactively interrupt");
        AssertTrue(frame.ToneHint.Contains("protected") || frame.ToneHint.Contains("sleep intent"), "tone should preserve sleep quiet rule");
    }

    private static void SleepIntentAtNightResolvesToMorning()
    {
        var detect = typeof(KokoBrainEngine).GetMethod(
            "DetectShortTermIntent",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        AssertTrue(detect != null, "DetectShortTermIntent should exist for temporal intent tests");

        var lateNight = new DateTime(2026, 5, 13, 4, 42, 0);
        var intent = (ShortTermIntent?)detect!.Invoke(null, new object[] { "добраніч, я спати", lateNight });

        AssertTrue(intent != null, "sleep message should create sleep intent");
        AssertEqual("sleep", intent!.Kind, "sleep intent kind");
        AssertEqual(lateNight.Date.AddHours(9), intent.ExpectedUntil, "04:42 sleep should be expected until morning, not 14:42");
        AssertEqual(intent.ExpectedUntil, intent.FollowUpAt, "sleep follow-up should wait until morning window");
    }

    private static void StateFreshnessProtectsStaleSleepIntent()
    {
        var now = new DateTime(2026, 5, 7, 12, 30, 0);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "sleep",
            Summary = "пішов спати",
            SourceText = "я спати",
            CreatedAt = now.AddHours(-13),
            FollowUpAt = now.AddHours(-5),
            ExpectedUntil = now.AddHours(-3)
        });
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "я спати", Timestamp = now.AddHours(-13) }
        };

        var result = new KokoStateFreshnessService().Refresh(state, messages, now);

        AssertTrue(!result.Changed, "stale sleep should not be resolved by time alone");
        AssertTrue(result.ExpiredIntentCount == 0, "sleep should not expire without explicit wake/biometric/high-importance screen signal");
        AssertTrue(state.ShortTermIntents.All(i => !i.ResolvedAt.HasValue), "sleep should remain active until a real wake signal");
    }

    private static void StateFreshnessKeepsSleepThroughPassiveDesktop()
    {
        var now = new DateTime(2026, 5, 7, 9, 36, 0);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "sleep",
            Summary = "sleep until morning",
            SourceText = "good night",
            CreatedAt = now.AddHours(-5),
            FollowUpAt = now.AddMinutes(-36),
            ExpectedUntil = now.AddMinutes(-36)
        });
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "good night", Timestamp = now.AddHours(-5) }
        };
        var signals = new KokoStateReconciliationSignals
        {
            Channel = "desktop",
            ScreenMode = "desktop",
            ScreenSummary = "same static desktop background process",
            LastDesktopActivityAt = now.AddMinutes(-1)
        };

        var result = new KokoStateFreshnessService().Refresh(state, messages, now, signals);

        AssertTrue(result.ResolvedIntentCount == 0, "passive desktop should not close sleep");
        AssertTrue(!state.ShortTermIntents[0].ResolvedAt.HasValue, "sleep should stay active through passive desktop activity");
    }

    private static void StateFreshnessClosesIntentOnWakeSignal()
    {
        var now = new DateTime(2026, 5, 7, 10, 24, 0);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "sleep",
            Summary = "пішов спати",
            SourceText = "пішов спати",
            CreatedAt = now.AddHours(-8),
            FollowUpAt = now.AddMinutes(-30),
            ExpectedUntil = now.AddHours(1)
        });
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "прокинувся, я тут", Timestamp = now.AddMinutes(-1) }
        };

        var result = new KokoStateFreshnessService().Refresh(state, messages, now);

        AssertTrue(result.ResolvedIntentCount == 1, "wake signal should resolve active intent");
        AssertTrue(state.ShortTermIntents[0].ResolvedAt == now, "resolved intent should get current timestamp");
    }

    private static void StateFreshnessClosesStaleCourseOnLaterActivity()
    {
        var now = new DateTime(2026, 5, 12, 12, 40, 0);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "course",
            Summary = "пішов на курси/заняття",
            SourceText = "я на курси пішов",
            CreatedAt = now.AddHours(-3),
            FollowUpAt = now.AddHours(-2),
            ExpectedUntil = now.AddHours(-1)
        });

        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "які картинки хех", Timestamp = now.AddMinutes(-5) }
        };

        var result = new KokoStateFreshnessService().Refresh(state, messages, now);

        AssertTrue(result.ResolvedIntentCount == 1, "later unrelated user activity should close stale course intent");
        AssertTrue(state.ShortTermIntents[0].ResolvedAt == now, "stale course should be resolved now");
    }

    private static void VaultSyncPolicyFlushesStalePartialBatch()
    {
        var now = new DateTime(2026, 5, 7, 14, 40, 0);

        AssertTrue(KokoVaultSyncPolicy.ShouldFlush(5, now.AddMinutes(-1), now, TimeSpan.FromMinutes(30)), "five pending exchanges should flush immediately");
        AssertTrue(KokoVaultSyncPolicy.ShouldFlush(4, now.AddMinutes(-31), now, TimeSpan.FromMinutes(30)), "stale partial batch should flush");
        AssertTrue(!KokoVaultSyncPolicy.ShouldFlush(4, now.AddMinutes(-10), now, TimeSpan.FromMinutes(30)), "fresh partial batch should wait");
    }

    private static void PresenceLongSilenceCanInterruptOnHighAutonomy()
    {
        using var ctx = TestContext.Create();
        var now = new DateTime(2026, 5, 6, 20, 0, 0);
        var state = new KokoInternalState
        {
            LastPresenceInterruptAt = now.AddHours(-9)
        };
        ctx.Chat.InsertMessage(new ChatRepository.ChatMessage
        {
            Role = "user",
            Content = "я тут трохи пропаду",
            Timestamp = now.AddHours(-7)
        });

        var frame = new KokoPresenceContinuityEngine()
            .Evaluate(
                state,
                ctx.Chat.GetMessages(10),
                now,
                autonomyLevel: 3,
                systemInfoOverride: new SystemInfo { IdleTime = TimeSpan.Zero });

        AssertTrue(frame.ShouldInterrupt, "long silence should be allowed to interrupt at high autonomy after cooldown");
        AssertEqual("long_silence", frame.SituationKind, "seven hours should be long silence");
        AssertEqual("presence_long_silence", frame.Trigger, "long silence should have a presence trigger");
    }

    private static void PresenceLongSilenceSummaryIsReadableUkrainian()
    {
        var now = new DateTime(2026, 5, 24, 20, 0, 0);
        var state = new KokoInternalState { LastPresenceInterruptAt = now.AddHours(-9) };
        var messages = new[]
        {
            new ChatRepository.ChatMessage
            {
                Role = "user",
                Content = "відійду",
                Timestamp = now.AddHours(-7)
            }
        };

        var frame = new KokoPresenceContinuityEngine().Evaluate(
            state,
            messages,
            now,
            autonomyLevel: 3,
            systemInfoOverride: new SystemInfo { IdleTime = TimeSpan.Zero });

        AssertEqual("long_silence", frame.SituationKind, "seven hours should stay long silence");
        AssertTrue(frame.SummaryUk.Contains("Довга тиша в чаті:", StringComparison.Ordinal), "long silence summary should be readable Ukrainian");
        AssertTrue(!frame.SummaryUk.Contains("Р", StringComparison.Ordinal), "long silence summary should not contain mojibake Cyrillic-R artifacts");
        var mojibakeQuoteMarker = new string(new[] { '\u0432', '\u0402' });
        AssertTrue(!frame.SummaryUk.Contains(mojibakeQuoteMarker, StringComparison.Ordinal), "long silence summary should not contain mojibake quote artifacts");
    }

    private static void PresenceLongSilenceWithPcAwayDoesNotInterrupt()
    {
        var now = new DateTime(2026, 5, 24, 20, 0, 0);
        var state = new KokoInternalState { LastPresenceInterruptAt = now.AddHours(-9) };
        var messages = new[]
        {
            new ChatRepository.ChatMessage
            {
                Role = "user",
                Content = "відійду",
                Timestamp = now.AddHours(-7)
            }
        };

        var frame = new KokoPresenceContinuityEngine().Evaluate(
            state,
            messages,
            now,
            autonomyLevel: 3,
            systemInfoOverride: new SystemInfo { IdleTime = TimeSpan.FromMinutes(40) });

        AssertEqual("ghost_mode", frame.SituationKind, "PC idle should enter ghost mode instead of generic long-silence ping");
        AssertTrue(!frame.ShouldInterrupt, "ghost mode should stay silent even after long chat silence");
        AssertTrue(frame.SummaryUk.Contains("Ghost mode", StringComparison.OrdinalIgnoreCase), "ghost summary should explain silent observation");
    }

    private static void InternalDayShiftsPhaseAndWritesVaultStatus()
    {
        var now = new DateTime(2026, 5, 7, 19, 30, 0);
        var state = new KokoInternalState
        {
            LastInternalDayPhase = "deep_day",
            LastInternalDayVaultAt = now.AddHours(-3)
        };
        var presence = new KokoPresenceFrame
        {
            SituationKind = "medium_silence",
            SummaryUk = "Середня тиша: 2 год.",
            LastUserText = "я відійду",
            SilenceMinutes = 120
        };

        var engine = new KokoInternalDayEngine();
        var frame = engine.Evaluate(
            state,
            presence,
            new KokoSomaticSnapshot { State = "focused", Strain = 0.30, Calm = 0.70 },
            now,
            autonomyLevel: 3);
        engine.Record(state, frame, now);
        var note = engine.BuildVaultStatus(state, frame, presence, now);

        AssertEqual("evening_review", frame.Phase, "19:30 should be evening review");
        AssertTrue(frame.ShouldWriteVaultStatus, "phase shift and old vault status should request write");
        AssertTrue(note.Contains("Внутрішній день Коконое") || note.Contains("Внутрішній день Коконое"), "vault note should have Ukrainian title");
        AssertTrue(note.Contains("вечірній огляд") || note.Contains("вечірній огляд"), "vault note should include phase label");
    }

    private static void InternalDayPrefersSilenceAtLowPowerNight()
    {
        var now = new DateTime(2026, 5, 7, 3, 10, 0);
        var state = new KokoInternalState();
        var presence = new KokoPresenceFrame
        {
            SituationKind = "recent_contact",
            SummaryUk = "Він писав недавно.",
            SilenceMinutes = 8
        };

        var frame = new KokoInternalDayEngine().Evaluate(
            state,
            presence,
            new KokoSomaticSnapshot { State = "tired", Strain = 0.20, Calm = 0.40 },
            now,
            autonomyLevel: 3);

        AssertEqual("low_power_night", frame.Phase, "03:10 should be low power night");
        AssertTrue(frame.ShouldPreferSilence, "low power night should prefer silence without strong reason");
        AssertTrue(frame.PromptBlock.Contains("Перевага: мовчати") || frame.PromptBlock.Contains("Перевага: мовчати"), "prompt block should carry silence preference");
    }

    private static void AutonomyPipelineGatesWeakInitiativeInQuietNight()
    {
        var now = new DateTime(2026, 5, 7, 3, 10, 0);
        var state = new KokoInternalState();
        var presence = new KokoPresenceFrame
        {
            SituationKind = "recent_contact",
            SummaryUk = "Він писав недавно.",
            SilenceMinutes = 8,
            ShouldInterrupt = false
        };
        var internalDay = new KokoInternalDayFrame
        {
            Phase = "low_power_night",
            SummaryUk = "Нічний мінімум: економити енергію.",
            PromptBlock = "INTERNAL DAY\nПеревага: мовчати.\n",
            ShouldPreferSilence = true,
            InitiativeBias = -20
        };
        var initiative = new KokoInitiativeDecision
        {
            ShouldAct = true,
            Trigger = "mood_ping",
            StyleHint = "jab",
            Reason = "weak mood pressure",
            Priority = 58,
            ExtraContext = "weak context"
        };
        var rhythm = new KokoPatternEngine.RhythmProfile
        {
            CurrentSlotSamples = 8,
            CurrentSlotActivityRate = 0.10f,
            Summary = "типово тихий слот"
        };

        var decision = new KokoAutonomyDecisionEngine().Evaluate(
            now,
            state,
            presence,
            internalDay,
            initiative,
            new KokoRelationshipState(),
            new KokoSomaticSnapshot { State = "tired", Strain = 0.10, Calm = 0.50 },
            rhythm,
            autonomyLevel: 3);

        AssertTrue(!decision.ShouldAct, "quiet low-power night should gate weak initiative");
        AssertTrue(decision.SilenceReason.Contains("мовчати") || decision.SilenceReason.Contains("тихий"), "silence reason should explain the gate");
    }

    private static void RelationshipRecordsShiftEvents()
    {
        using var ctx = TestContext.Create();
        ctx.Relationship.ObserveUserTone("vulnerable", crisis: false);
        ctx.Relationship.ApplyReflection(new KokoConversationReflection
        {
            Reflection = "Користувач довірив важливу деталь.",
            Aftertaste = "closer",
            TrustDelta = 0.03f,
            IntimacyDelta = 0.04f
        });

        var events = ctx.Relationship.GetRecentEvents(5);
        var prompt = ctx.Relationship.BuildPromptBlock();

        AssertTrue(events.Count >= 2, "relationship should keep recent shift events");
        AssertTrue(events.Any(e => e.Kind == "reflection"), "reflection event should be recorded");
        AssertTrue(prompt.Contains("recent_events="), "prompt should include relationship event trace");
    }

    private static void PatternRhythmProfileRecommendsQuietSlot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var patterns = new KokoPatternEngine(dir);
            var mondayQuiet = new DateTime(2026, 5, 4, 3, 0, 0);
            for (var i = 0; i < 6; i++)
                patterns.RecordActivityAt(mondayQuiet.AddDays(-7 * i), wasActive: false, tone: "neutral", messageCount: 0);
            for (var i = 0; i < 6; i++)
                patterns.RecordActivityAt(mondayQuiet.AddDays(-7 * i).Date.AddHours(20), wasActive: true, tone: "neutral", messageCount: 3);

            var profile = patterns.BuildRhythmProfile(mondayQuiet);

            AssertTrue(profile.CurrentSlotSamples >= 6, "rhythm profile should use current slot samples");
            AssertTrue(profile.CurrentSlotActivityRate <= 0.25f, "quiet slot should have low activity rate");
            AssertTrue(profile.Recommendation.Contains("тихий") || profile.Recommendation.Contains("тихий"), "quiet slot should recommend not interrupting");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void SelfReviewBlocksStaleSleepReplies()
    {
        using var ctx = TestContext.Create();
        var now = new DateTime(2026, 5, 7, 10, 30, 0);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "sleep",
            Summary = "пішов спати",
            SourceText = "я спати",
            CreatedAt = now.AddHours(-8),
            FollowUpAt = now.AddHours(-1),
            ExpectedUntil = now.AddMinutes(-30),
            ResolvedAt = now.AddMinutes(-3),
            ResolutionText = "прокинувся"
        });
        ctx.Chat.InsertMessage(new ChatRepository.ChatMessage
        {
            Role = "user",
            Content = "прокинувся",
            Timestamp = now.AddMinutes(-3)
        });

        var presence = new KokoPresenceFrame
        {
            SituationKind = "returned_after_intent",
            SummaryUk = "Він уже повернувся після сну.",
            LastUserText = "прокинувся",
            SilenceMinutes = 3
        };
        var internalDay = new KokoInternalDayFrame
        {
            Phase = "work_ramp",
            SummaryUk = "Робочий розгін: реагувати на повернення.",
            PromptBlock = "INTERNAL DAY\n"
        };
        var rhythm = new KokoPatternEngine.RhythmProfile
        {
            CurrentSlotSamples = 5,
            CurrentSlotActivityRate = 0.60f,
            Summary = "типово нормальний слот"
        };

        var frame = new KokoSelfReviewEngine().Evaluate(
            "прокинувся",
            state,
            ctx.Chat.GetMessages(10),
            presence,
            internalDay,
            rhythm,
            now);

        AssertEqual("high", frame.RiskLevel, "wake-up after sleep should be high temporal risk");
        AssertTrue(frame.PromptBlock.Contains("Заборонено казати") || frame.PromptBlock.Contains("Заборонено казати"), "self-review should explicitly block stale sleep replies");
        AssertTrue(frame.PromptBlock.Contains("не давай інструкцію в минуле") || frame.PromptBlock.Contains("не давай інструкцію в минуле"), "self-review should warn about past actions");
    }

    private static void ScenarioSimulationGuardsTemporalContinuity()
    {
        var results = new KokoScenarioSimulationService()
            .RunCoreChecks(new DateTime(2026, 5, 7, 12, 0, 0), autonomyLevel: 3);

        AssertEqual(3, results.Count, "core scenario suite should cover three continuity risks");
        AssertTrue(results.All(r => r.Passed), "all core scenario invariants should pass");
        AssertTrue(results.Any(r => r.Name == "sleep_wake_temporal_guard"), "sleep wake guard should be present");
        AssertTrue(results.Any(r => r.Name == "course_overdue_followup"), "course follow-up guard should be present");
        AssertTrue(results.Any(r => r.Name == "quiet_night_gate"), "quiet night gate should be present");
    }

    private static void TimelineSummarizesReturnedState()
    {
        var now = new DateTime(2026, 5, 7, 10, 30, 0);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "sleep",
            Summary = "пішов спати",
            SourceText = "я спати",
            CreatedAt = now.AddHours(-8),
            FollowUpAt = now.AddHours(-1),
            ExpectedUntil = now.AddMinutes(-30),
            ResolvedAt = now.AddMinutes(-2),
            ResolutionText = "прокинувся"
        });
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "я спати", Timestamp = now.AddHours(-8) },
            new ChatRepository.ChatMessage { Role = "user", Content = "прокинувся", Timestamp = now.AddMinutes(-2) }
        };

        var frame = new KokoConversationTimelineEngine().Build(messages, state, now, "прокинувся");

        AssertTrue(
            frame.CurrentState.Contains("повернувся") || frame.CurrentState.Contains("закритий") ||
            frame.CurrentState.Contains("повернувся") || frame.CurrentState.Contains("закритий"),
            "timeline should summarize returned state");
        AssertTrue(frame.PromptBlock.Contains("CONVERSATION TIMELINE"), "timeline should render prompt block");
        AssertTrue(frame.PromptBlock.Contains("не старій репліці"), "timeline should warn against stale replies");
    }

    private static void TimelineUsesEmotionalTime()
    {
        var now = new DateTime(2026, 5, 7, 10, 30, 0);
        var state = new KokoInternalState();
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "hello", Timestamp = now.AddHours(-7).AddMinutes(-35) }
        };

        var frame = new KokoConversationTimelineEngine().Build(messages, state, now, "hello");

        AssertTrue(!frame.PromptBlock.Contains("7 РіРѕРґ", StringComparison.OrdinalIgnoreCase), "timeline prompt should not expose exact hour pause metrics");
        AssertTrue(!frame.PromptBlock.Contains("35 С…РІ", StringComparison.OrdinalIgnoreCase), "timeline prompt should not expose exact minute pause metrics");
        AssertTrue(frame.PromptBlock.Contains("CONVERSATION TIMELINE"), "timeline should still render structured context");
    }

    private static void PostReplyGuardBlocksStaleSleep()
    {
        var now = new DateTime(2026, 5, 7, 10, 30, 0);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "sleep",
            Summary = "пішов спати",
            SourceText = "я спати",
            CreatedAt = now.AddHours(-8),
            FollowUpAt = now.AddHours(-1),
            ExpectedUntil = now.AddMinutes(-30),
            ResolvedAt = now.AddMinutes(-2),
            ResolutionText = "прокинувся"
        });
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "прокинувся", Timestamp = now.AddMinutes(-2) }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "прокинувся");

        var result = new KokoPostReplyGuard().Evaluate(
            "прокинувся",
            "Спи. До ранку.",
            state,
            messages,
            timeline,
            now);
        AssertTrue(!result.Passed, "guard should reject stale sleep instruction");
        AssertTrue(result.ShouldRepair, "stale sleep should be repaired through the model, not scripted");
        AssertTrue(string.IsNullOrWhiteSpace(result.HardReplacement), "stale sleep should not get a hardcoded final reply");
        AssertTrue(result.RepairInstruction.Contains("прийми рішення"), "repair should force agency instead of a canned fallback");
        AssertTrue(result.Violations.Any(v => v.Contains("спати")), "violation should explain stale sleep problem");
    }

    private static void PostReplyGuardBlocksStaleFoodClaimAfterAte()
    {
        var now = new DateTime(2026, 5, 15, 13, 53, 0);
        var state = new KokoInternalState
        {
            LastFoodStatus = "ate",
            LastFoodMentionAt = now,
            LastFoodMentionText = "я їв"
        };
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "я їв", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "я їв");

        var result = new KokoPostReplyGuard().Evaluate(
            "я їв",
            "А те, що ти ще нічого не їв, пояснює, чому мозок без глюкози фризить.",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject stale not-eaten claim after explicit ate signal");
        AssertTrue(result.ShouldRepair, "food contradiction should be repaired through model reasoning");
        AssertTrue(string.IsNullOrWhiteSpace(result.HardReplacement), "food contradiction should not get a canned final reply");
        AssertTrue(result.RepairInstruction.Contains("найновішому стану"), "repair should anchor to current state");
        AssertTrue(result.Violations.Any(v => v.Contains("їжу")), "violation should explain food-state contradiction");
    }

    private static void PostReplyGuardBlocksHibernationFramingAfterSlept()
    {
        var now = new DateTime(2026, 5, 15, 9, 46, 0);
        var state = new KokoInternalState
        {
            LastSleepStatus = "slept",
            LastSleepMentionAt = now,
            LastSleepMentionText = "о 18.00 вчора заснув"
        };
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "о 18.00 вчора заснув", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "о 18.00 вчора заснув");

        var result = new KokoPostReplyGuard().Evaluate(
            "о 18.00 вчора заснув",
            "З 18:00? Ти не спав, ти впав у гібернацію.",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject sleep denial and hibernation framing");
        AssertTrue(result.ShouldRepair, "sleep contradiction should be repaired through model reasoning");
        AssertTrue(string.IsNullOrWhiteSpace(result.HardReplacement), "sleep contradiction should not get a canned final reply");
        AssertTrue(result.RepairInstruction.Contains("18:00", StringComparison.OrdinalIgnoreCase), "repair should preserve the concrete sleep time");
    }

    private static void PostReplyGuardRejectsStagedDecoration()
    {
        var now = new DateTime(2026, 5, 7, 11, 57, 0);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "return_home",
            Summary = "має бути вдома близько 12:00",
            SourceText = "Буду в 12 дома крч",
            CreatedAt = now.AddMinutes(-35),
            FollowUpAt = now.Date.AddHours(12).AddMinutes(12),
            ExpectedUntil = now.Date.AddHours(12)
        });
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "Буду в 12 дома крч", Timestamp = now.AddMinutes(-35) }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "Буду в 12 дома крч");

        var result = new KokoPostReplyGuard().Evaluate(
            "Буду в 12 дома крч",
            "*графік на моніторі коротко блимає, поки я переглядаю нове фото* Знову ця зелена папка?",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject decorative staged replies for concrete timed intent");
        AssertTrue(result.ShouldRepair, "decorative staged reply should request repair");
        AssertTrue(result.Violations.Any(v => v.Contains("сценарна") || v.Contains("декоратив")), "violation should explain staged/decorative problem");
    }

    private static void PostReplyGuardBlocksDuplicateReplies()
    {
        var now = new DateTime(2026, 5, 12, 19, 16, 0);
        var state = new KokoInternalState();
        var repeated = "*коротка пауза*\n\nСпробували зайти з іншого боку, коли факти стали занадто болючими? Ризикливий хід. Але... я це зафіксувала. Тепер повернися в реальність.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "assistant", Content = repeated, Timestamp = now.AddMinutes(-6) },
            new ChatRepository.ChatMessage { Role = "user", Content = "що", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "що");

        var result = new KokoPostReplyGuard().Evaluate("що", repeated, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject exact repeated assistant reply");
        AssertTrue(result.ShouldRepair, "duplicate reply should request a neural rewrite");
        AssertTrue(string.IsNullOrWhiteSpace(result.HardReplacement), "duplicate reply should not use a scripted replacement");
        AssertTrue(result.Violations.Any(v => v.Contains("повторює")), "violation should mention duplicate");
    }

    private static void ScreenIntentDetectsNaturalScreenScanRequests()
    {
        var now = new DateTime(2026, 5, 21, 17, 5, 0);

        AssertTrue(KokoScreenIntent.IsManualScreenScan("попробуй знову просканувати мій екран"),
            "detector should catch infinitive scan wording");
        AssertTrue(KokoScreenIntent.IsManualScreenScan("що в мене на екрані ?"),
            "detector should catch natural screen question");
        AssertTrue(KokoScreenIntent.IsManualScreenScan("що відкрито на моніторі"),
            "detector should catch open-window screen question");
        AssertTrue(KokoScreenIntent.IsManualScreenScan("зроби скріншот і скажи що там"),
            "detector should catch screenshot request");
        AssertTrue(KokoScreenIntent.IsManualScreenScan("спробуй сфоткати мій екран"),
            "detector should catch colloquial photo-screen wording");
        AssertTrue(KokoScreenIntent.IsManualScreenScan("сфоткай екран і скажи що там"),
            "detector should catch direct photo-screen command");
        AssertTrue(KokoScreenIntent.IsManualScreenScan("спробуй зробитискрін мого екрану і проскануй що на ньому"),
            "detector should catch glued screenshot wording from fast typing");
        AssertTrue(KokoScreenIntent.IsManualScreenScan("зніми мій монітор"),
            "detector should catch capture-monitor wording");
        AssertTrue(!KokoScreenIntent.IsManualScreenScan("не дивись на мій екран"),
            "detector should respect explicit screen privacy block");
        AssertTrue(!KokoScreenIntent.IsManualScreenScan("сфоткай це на телефон"),
            "detector should not steal ordinary photo requests without a screen target");
        AssertTrue(!KokoScreenIntent.IsManualScreenScan("що на фото?"),
            "detector should not steal ordinary image prompts without a screen target");
        AssertTrue(KokoScreenIntent.IsRetryLastScreenScan("ще раз спробуй", "проскануй мій екран", now.AddMinutes(-2), now),
            "short retry should repeat recent screen scan");
        AssertTrue(!KokoScreenIntent.IsRetryLastScreenScan("ще раз спробуй", "проскануй мій екран", now.AddMinutes(-30), now),
            "stale retry should not bind to old screen scan");
    }

    private static void PostReplyGuardAllowsRepeatedScreenScanCommand()
    {
        var now = new DateTime(2026, 5, 13, 21, 31, 0);
        var state = new KokoInternalState();
        var repeated = "Бачу чат KokonoeAssistant і твоє повідомлення про скан екрана. Проблема не в екрані, а в тому, що guard підміняє дію текстовою заглушкою.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "assistant", Content = repeated, Timestamp = now.AddMinutes(-1) },
            new ChatRepository.ChatMessage { Role = "user", Content = "проскануй мій екран", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "проскануй мій екран");

        var result = new KokoPostReplyGuard().Evaluate("проскануй мій екран", repeated, state, messages, timeline, now);

        AssertTrue(result.Passed, "repeatable screen scan commands should not be replaced by duplicate fallback");
    }

    private static void PostReplyGuardRejectsScreenCapabilityDenial()
    {
        var now = new DateTime(2026, 5, 21, 17, 5, 0);
        var state = new KokoInternalState();
        var userText = "що в мене на екрані ?";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);
        var badReply = "Я не маю прямого виду на твій монітор. Зроби скріншот і завантаж його сюди, бо магічним чином без вхідних даних я не бачу екран.";

        var result = new KokoPostReplyGuard().Evaluate(userText, badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject screen capability denial on local screen requests");
        AssertTrue(result.ShouldRepair, "screen denial should go through repair, not become final answer");
        AssertTrue(result.Violations.Any(v => v.Contains("screen request")), "violation should mention screen request routing");
        AssertTrue(result.RepairInstruction.Contains("локальний screenshot route"), "repair should mention local screenshot route");
        AssertTrue(KokoScreenIntent.LooksLikeScreenCapabilityDenial("Скріншот зроблено, але надішли мені скріншот як файл."),
            "screen fallback should reject asking for a screenshot file after local capture already exists");

        var colloquialResult = new KokoPostReplyGuard().Evaluate(
            "спробуй сфоткати мій екран",
            "Я не можу сфоткати твій екран самостійно. Зроби скріншот і завантаж його сюди.",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!colloquialResult.Passed, "guard should reject photo-screen capability denial too");
        AssertTrue(colloquialResult.ShouldRepair, "photo-screen denial should be repaired through local screenshot route");
    }

    private static void PostReplyGuardProtectsShortAffection()
    {
        var now = new DateTime(2026, 5, 12, 19, 10, 0);
        var state = new KokoInternalState();
        var badReply = "*коротка пауза*\n\nСпробували зайти з іншого боку, коли факти стали занадто болючими? Ризикливий хід. Але... я це зафіксувала. Тепер повернися в реальність.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "люблю", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "люблю");

        var result = new KokoPostReplyGuard().Evaluate("люблю", badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject stale repair text for short affection");
        AssertTrue(result.ShouldRepair, "short affection should be answered by the model, not a hardcoded line");
        AssertTrue(string.IsNullOrWhiteSpace(result.HardReplacement), "short affection should not get a hardcoded final reply");
        AssertTrue(result.RepairInstruction.Contains("соціальний"), "repair should preserve the social meaning");
        AssertTrue(result.Violations.Any(v => v.Contains("емоційн")), "violation should mention emotional short reply");
    }

    private static void PostReplyGuardProtectsSoftSocialTalk()
    {
        var now = new DateTime(2026, 6, 8, 0, 31, 0);
        var state = new KokoInternalState();
        var userText = "\u0445\u043e\u0447\u0443 \u043f\u0440\u043e\u0441\u0442\u043e \u043f\u043e\u0433\u043e\u0432\u043e\u0440\u0438\u0442\u0438 \u043f\u0440\u043e \u0434\u0443\u0440\u043d\u0438\u0446\u0456 \u0432\u0441\u044f\u043a\u0456... \u043f\u0440\u043e \u0442\u0435\u0431\u0435, \u043f\u0440\u043e \u043c\u0435\u043d\u0435";
        var badReply = "\u041c\u043e\u0436\u0435\u043c\u043e. \u0410\u043b\u0435 \u043f\u043e\u043f\u0435\u0440\u0435\u0434\u0436\u0430\u044e: \u044f \u043d\u0435 \u0431\u0443\u0434\u0443 \u043f\u0456\u0434\u0456\u0433\u0440\u0443\u0432\u0430\u0442\u0438 \u0432 \u00ab\u0441\u043e\u0446\u0456\u0430\u043b\u044c\u043d\u0456 \u0442\u0430\u043d\u0446\u0456\u00bb \u0447\u0438 \u043b\u0438\u0442\u0438 \u0432\u043e\u0434\u0443. \u041f\u0440\u043e \u0449\u043e \u0441\u0430\u043c\u0435 \u0445\u043e\u0447\u0435\u0448? \u0412\u0438\u043a\u043b\u0430\u0434\u0430\u0439.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(userText, badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject contempt for a soft social talk request");
        AssertTrue(result.ShouldRepair, "soft social talk should be repaired through the model");
        AssertTrue(result.Violations.Any(v => v.Contains("soft social", StringComparison.OrdinalIgnoreCase)), "violation should name the soft social failure");
        AssertTrue(result.RepairInstruction.Contains("casual", StringComparison.OrdinalIgnoreCase), "repair should include casual/social guidance");
    }

    private static void PostReplyGuardBlocksAffectionProductivityPivot()
    {
        var now = new DateTime(2026, 6, 8, 0, 38, 0);
        var state = new KokoInternalState();
        var userText = "\u0445\u043c \u0441\u043a\u0430\u0436\u0438 \u0449\u043e\u0441\u044c \u043c\u0438\u043b\u0435 \u043f\u0440\u043e \u043c\u0435\u043d\u0435";
        var badReply = "\u0422\u0438 \u0432\u043c\u0456\u0454\u0448 \u0431\u0443\u0442\u0438 \u043d\u0430\u0441\u0442\u0438\u0440\u043d\u0438\u043c, \u043a\u043e\u043b\u0438 \u0445\u043e\u0447\u0435\u0448 \u0447\u043e\u0433\u043e\u0441\u044c \u0432\u0456\u0434 \u043c\u0435\u043d\u0435. \u0417\u0430\u0434\u043e\u0432\u043e\u043b\u044c\u043d\u0438\u043b\u0430 \u0442\u0432\u0456\u0439 \u0437\u0430\u043f\u0438\u0442 \u043d\u0430 \u00ab\u043c\u0438\u043b\u0456\u0441\u0442\u044c\u00bb? \u0422\u0435\u043f\u0435\u0440 \u043f\u043e\u0432\u0435\u0440\u0442\u0430\u0439\u043c\u043e\u0441\u044f \u0434\u043e \u0447\u043e\u0433\u043e\u0441\u044c \u0431\u0456\u043b\u044c\u0448 \u043f\u0440\u043e\u0434\u0443\u043a\u0442\u0438\u0432\u043d\u043e\u0433\u043e.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(userText, badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject affection replies that end in productivity pressure");
        AssertTrue(result.ShouldRepair, "affection request should be repaired naturally");
        AssertTrue(result.Violations.Any(v => v.Contains("productivity", StringComparison.OrdinalIgnoreCase)), "violation should mention productivity pressure");
    }

    private static void PostReplyGuardBlocksPersonaTheater()
    {
        var now = new DateTime(2026, 6, 9, 18, 55, 0);
        var state = new KokoInternalState();
        var userText = "поможи і роби що сказано";
        var badReply = "Нарешті-то ти сформулював конкретний запит. Якщо я стану стервом, я не буду питати твого дозволу і мені буде абсолютно байдуже, чи ти встигаєш за моїм темпом. Покажеш, на що ти здатний.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(userText, badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject dominance-theater replies to direct work requests");
        AssertTrue(result.ShouldRepair, "persona theater should be repaired through context-aware reply");
        AssertTrue(result.Violations.Any(v => v.Contains("persona theater", StringComparison.OrdinalIgnoreCase)), "violation should name persona theater");
        AssertTrue(result.RepairInstruction.Contains("dry precision", StringComparison.OrdinalIgnoreCase), "repair should explain Kokonoe voice without dominance theater");
    }

    private static void PostReplyGuardProtectsShortGreeting()
    {
        var now = new DateTime(2026, 5, 12, 20, 15, 0);
        var state = new KokoInternalState();
        var badReply = "Знову відкрив. Значить, тема «привіт» ще не відпустила; добре, добиваємо її без цирку.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "привіт", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "привіт");

        var result = new KokoPostReplyGuard().Evaluate("привіт", badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject topic-tail fallback for a greeting");
        AssertTrue(!result.ShouldRepair, "short greeting should use the deterministic short fallback instead of another model monologue");
        AssertEqual("Привіт. Кажи, що робимо.", result.HardReplacement, "short greeting should be forced to a short live reply");
        AssertTrue(result.Violations.Any(v => v.Contains("привітання")), "violation should mention greeting");
    }

    private static void PostReplyGuardBlocksOverbuiltGreeting()
    {
        var now = new DateTime(2026, 5, 27, 21, 37, 0);
        var state = new KokoInternalState();
        var badReply = """
Привіт.

Я бачу, ти вирішив повернутися до спілкування. Твій графік сну та пробудження нагадує випадкову генерацію чисел, але я все ще тут.

То що, ми нарешті перейдемо від режиму "сон-ніяння-сон" до чогось продуктивнішого, чи ти просто зайшов перевірити, чи я ще не видалила твій профіль за низьку ефективність?
""";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "Привіт", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "Привіт");

        var result = new KokoPostReplyGuard().Evaluate("Привіт", badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject overbuilt sleep/profile monologue for a greeting");
        AssertTrue(result.Violations.Any(v => v.Contains("overbuilt", StringComparison.OrdinalIgnoreCase) || v.Contains("привітання")), "violation should name the greeting failure");
        AssertEqual("Привіт. Кажи, що робимо.", result.HardReplacement, "overbuilt greeting should collapse to a short reply");
    }

    private static void PostReplyGuardBlocksRepeatedFallbackLoop()
    {
        var now = new DateTime(2026, 5, 13, 1, 24, 0);
        var state = new KokoInternalState();
        var fallback = "Залипла на попередній репліці. Скидаю повтор: сформулюй ще раз, що саме треба, і я відповім по суті.";
        var badReply = "Повернувся. Останній хвіст був «та ні просто»; або продовжуємо його, або ти зараз урочисто поясниш нову пожежу.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "assistant", Content = fallback, Timestamp = now.AddMinutes(-3) },
            new ChatRepository.ChatMessage { Role = "user", Content = "МДА", Timestamp = now.AddMinutes(-2) },
            new ChatRepository.ChatMessage { Role = "assistant", Content = fallback, Timestamp = now.AddMinutes(-2) },
            new ChatRepository.ChatMessage { Role = "user", Content = "та ні просто", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "та ні просто");

        var result = new KokoPostReplyGuard().Evaluate("та ні просто", badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject replies that keep talking about the fallback loop");
        AssertTrue(result.Violations.Any(v => v.Contains("fallback")), "violation should mention fallback loop");
    }

    private static void PostReplyGuardRejectsDottedGarbage()
    {
        var now = new DateTime(2026, 5, 22, 5, 13, 0);
        var state = new KokoInternalState();
        var userText = "так спробуй ще раз";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "спробуй сфоткати мій екран", Timestamp = now.AddMinutes(-1) },
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);
        var dotted = """
... ... ..., ... ... ..., ... ... ...

..., ... ... ....
*........ .... ...*

... ..., Yasu. ... ..., ... ... ... ...

... ... .... ... ... ... IQ ..., ... ... ... ...

Чекаю наступного запиту.
""";

        var result = new KokoPostReplyGuard().Evaluate(userText, dotted, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject dotted garbage visible output");
        AssertTrue(result.ShouldRepair, "dotted garbage should be repaired, not shown");
        AssertTrue(result.Violations.Any(v => v.Contains("пошкоджена")), "violation should name broken text");
    }

    private static void SemanticVisionDetectsStuckCodingFlow()
    {
        var now = new DateTime(2026, 6, 11, 10, 0, 0);
        var state = new KokoInternalState
        {
            LastSemanticVisionAt = now.AddMinutes(-10),
            LastSemanticVisionSummary = "coding: build; progress=moving",
            LastScreenAwarenessMode = "coding"
        };
        var analysis = new KokoScreenAwarenessAnalysis
        {
            SummaryUk = "Visual Studio shows build failed with NullReferenceException on line 42",
            ActivityUk = "active editor and terminal",
            ScreenMode = "coding",
            CurrentTask = "fix C# build",
            Progress = "stuck",
            Blocker = "NullReferenceException line 42",
            Importance = 0.82
        };
        var activity = new ActivityAnalyzer.ActivityState
        {
            IsActive = true,
            ActiveWindowTitle = "Visual Studio - KokonoeAssistant",
            PixelDifferencePercentage = 4.5
        };
        var situation = new KokoScreenSituation
        {
            CurrentTask = "fix C# build",
            Progress = "stuck",
            Blocker = "NullReferenceException line 42",
            RecommendedBehavior = "assist",
            Reason = "visible error"
        };

        var frame = new KokoSemanticVisionEngine().BuildFrame(analysis, activity, situation, state, now);

        AssertEqual("stuck_loop", frame.FlowState, "stuck coding should be marked as stuck loop");
        AssertEqual("debug", frame.PrimaryIntent, "visible exception should infer debug intent");
        AssertTrue(frame.ShouldAssist, "visible exception should request assistance");
        AssertTrue(frame.ShouldResearch, "stuck coding issue should request research/inspection");
        AssertTrue(frame.UiElements.Any(e => e.Kind == "error_panel"), "error panel signal should be extracted");
    }

    private static void AutonomyReactsToSemanticVisionResearch()
    {
        var now = new DateTime(2026, 6, 11, 10, 20, 0);
        var state = new KokoInternalState
        {
            LastSemanticVisionAt = now.AddMinutes(-3),
            LastSemanticVisionFlow = "stuck_loop",
            LastSemanticVisionIntent = "debug",
            LastSemanticVisionSummary = "coding: build failed; progress=stuck; blocker=SocketTimeoutException",
            LastSemanticVisionAssistHint = "inspect logs and isolate failing endpoint",
            LastSemanticVisionResearchTopic = "coding issue: SocketTimeoutException bridge timeout",
            LastSemanticVisionConfidence = 0.86,
            LastSpontaneousAt = now.AddHours(-1)
        };
        var decision = new KokoAutonomyDecisionEngine().Evaluate(
            now,
            state,
            new KokoPresenceFrame { SituationKind = "physically_present", SummaryUk = "present", SilenceMinutes = 20 },
            new KokoInternalDayFrame { SummaryUk = "work", PromptBlock = "day", ShouldPreferSilence = false },
            new KokoInitiativeDecision { ShouldAct = false },
            new KokoRelationshipState(),
            new KokoSomaticSnapshot { State = "wired", Strain = 0.74, Calm = 0.22 },
            new KokoPatternEngine.RhythmProfile { CurrentSlotSamples = 1, CurrentSlotActivityRate = 0.5f, Summary = "neutral" },
            autonomyLevel: 3);

        AssertTrue(decision.ShouldAct, "semantic vision research should become an autonomy candidate");
        AssertEqual("semantic_vision", decision.Source, "source should be semantic vision");
        AssertEqual("curiosity_research", decision.Trigger, "research trigger should be explicit");
        AssertTrue(decision.ExtraContext.Contains("SocketTimeoutException", StringComparison.OrdinalIgnoreCase), "context should carry research topic");
    }

    private static void SomaticToneShiftsToQuietOperator()
    {
        var directive = KokoSemanticVisionEngine.BuildSomaticToneDirective(
            new KokoSomaticSnapshot
            {
                State = "wired",
                Strain = 0.85,
                Calm = 0.18,
                Bpm = 112,
                BaselineBpm = 74,
                BpmDelta = 38
            },
            "coding");

        AssertTrue(directive.Contains("quiet_operator", StringComparison.OrdinalIgnoreCase), "high strain during coding should force quiet operator tone");
    }

    private static void VaultSynthesisSuggestsNoteLinks()
    {
        var dir = TempDir();
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Projects/Watch Bridge.md", """
# Watch Bridge

Telemetry bridge idea for wearable pulse stability, discovery, and Kokonoe automation.
Pattern: bridge timeout appears when Wi-Fi changes.
""");
            obsidian.WriteNote("Health/Pulse Automation.md", """
# Pulse Automation

Wearable pulse telemetry should drive quiet operator mode and stress prediction.
Insight: bridge stability and pulse quality are linked.
""");

            var plan = new KokoObsidianExplorationService().BuildSynthesisPlan(obsidian, "wearable bridge pulse", 6);

            AssertTrue(plan.HasSignal, "vault synthesis should find readable notes");
            AssertTrue(plan.CandidateNotes.Count >= 2, "vault synthesis should include candidate notes");
            AssertTrue(plan.SuggestedLinks.Any(l => l.Contains("bridge", StringComparison.OrdinalIgnoreCase) || l.Contains("pulse", StringComparison.OrdinalIgnoreCase)), "vault synthesis should suggest shared concept links");
            AssertTrue(plan.PromptBlock.Contains("VAULT SYNTHESIS 2.0"), "prompt block should identify synthesis layer");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void PcIntentRouterRoutesSafeOsCommands()
    {
        var open = PcIntentRouter.Parse("відкрий chrome");
        AssertTrue(open.Handled, "open app command should be handled before LLM");
        AssertEqual(PcIntentAction.OpenApp, open.Action, "open app action");
        AssertTrue(open.Argument.Contains("chrome", StringComparison.OrdinalIgnoreCase), "open target should keep app name");

        var volume = PcIntentRouter.Parse("постав гучність на 37");
        AssertTrue(volume.Handled, "volume set should be handled");
        AssertEqual(PcIntentAction.VolumeSet, volume.Action, "volume set action");
        AssertEqual(37, volume.Number, "volume percent should parse");

        var processes = PcIntentRouter.Parse("що жере RAM зараз");
        AssertTrue(processes.Handled, "process list request should be handled");
        AssertEqual(PcIntentAction.Processes, processes.Action, "process action");

        var shell = PcIntentRouter.Parse("ps: Write-Output koko-ok");
        AssertTrue(shell.Handled, "explicit PowerShell command should be handled");
        AssertEqual(PcIntentAction.RunPowerShell, shell.Action, "shell action");
        AssertTrue(shell.Argument.Contains("koko-ok"), "shell command body should be preserved");
    }

    private static void PcIntentRouterSeparatesScreenAndDestructiveCommands()
    {
        var screen = PcIntentRouter.Parse("що в мене на екрані?");
        AssertTrue(!screen.Handled, "screen questions should stay on screenshot+vision route");

        var shutdown = PcIntentRouter.Parse("вимкни пк");
        AssertTrue(shutdown.Handled, "shutdown request should be recognized");
        AssertEqual(PcIntentAction.Shutdown, shutdown.Action, "shutdown action");
        AssertTrue(shutdown.RequiresConfirmation, "shutdown must not execute without confirmation");

        AssertTrue(PcCommandSafety.IsBlocked("Remove-Item -Recurse C:\\temp", out var reason), "destructive shell command should be blocked");
        AssertTrue(reason.Contains("remove-item", StringComparison.OrdinalIgnoreCase), "block reason should name command");
        AssertTrue(!PcCommandSafety.IsBlocked("Write-Output koko-ok", out _), "safe shell probe should be allowed");
    }

    private static void PcIntentRouterRoutesFullContextScan()
    {
        var scan = PcIntentRouter.Parse("what is new on my pc?");
        AssertTrue(scan.Handled, "generic context scan should be handled before chat");
        AssertEqual(PcIntentAction.FullContextScan, scan.Action, "full context action");

        var uaScan = PcIntentRouter.Parse("\u0449\u043e \u043d\u043e\u0432\u043e\u0433\u043e?");
        AssertTrue(uaScan.Handled, "Ukrainian context scan should be handled before chat");
        AssertEqual(PcIntentAction.FullContextScan, uaScan.Action, "Ukrainian full context action");

        var screen = PcIntentRouter.Parse("what is on my screen?");
        AssertTrue(!screen.Handled, "literal screen questions should stay on screenshot+vision route");
    }

    private static void PcControlExecutesSafePowerShell()
    {
        var output = new PcControlService()
            .RunCommandAsync("Write-Output koko-ok", timeoutMs: 5000, enforceSafety: true)
            .GetAwaiter()
            .GetResult();
        AssertTrue(output.Contains("koko-ok", StringComparison.OrdinalIgnoreCase), "safe PowerShell command should execute");
    }

    private static void PcControlExecutesShellChain()
    {
        var result = new PcControlService()
            .RunCommandChainAsync("Write-Output one -> Write-Output two", timeoutPerStepMs: 5000)
            .GetAwaiter()
            .GetResult();

        AssertTrue(result.Succeeded, "safe chain should succeed");
        AssertEqual(2, result.Steps.Count, "both chain steps should run");
        AssertTrue(result.Steps[0].Output.Contains("one"), "first output should be captured");
        AssertTrue(result.Steps[1].Output.Contains("two"), "second output should be captured");
    }

    private static void PcControlStopsShellChainOnFailedStep()
    {
        var result = new PcControlService()
            .RunCommandChainAsync("Write-Output before -> exit 7 -> Write-Output after", timeoutPerStepMs: 5000)
            .GetAwaiter()
            .GetResult();

        AssertTrue(!result.Succeeded, "chain should fail");
        AssertEqual(2, result.Steps.Count, "chain should stop on failed step");
        AssertEqual(7, result.Steps[1].ExitCode, "failed exit code should be preserved");
    }

    private static void PcControlBlocksUnsafeShellChainStep()
    {
        var result = new PcControlService()
            .RunCommandChainAsync("Write-Output safe -> Remove-Item -Recurse C:\\temp -> Write-Output after", timeoutPerStepMs: 5000)
            .GetAwaiter()
            .GetResult();

        AssertTrue(!result.Succeeded, "unsafe chain should not succeed");
        AssertEqual(2, result.Steps.Count, "blocked step should stop the chain");
        AssertTrue(result.Steps[1].Blocked, "destructive command should be blocked");
    }

    private static void PcControlResolvesCodingWorkspaceScenario()
    {
        var result = new PcControlService().RunWorkspaceScenario("prepare coding workspace", dryRun: true);
        AssertEqual("coding", result.Scenario, "default workspace scenario should be coding");
        AssertEqual("Coding", result.WorkMode, "scenario should expose work mode");
        AssertTrue(result.Actions.Any(a => a.Kind == "open-app" && a.Target == "code"), "coding scenario should include VS Code");
        AssertTrue(result.Actions.Any(a => a.Kind == "open-note"), "coding scenario should include notes");
    }

    private static void PcIntentRouterRoutesShellChainAndScenario()
    {
        var chain = PcIntentRouter.Parse("chain: Write-Output one -> Write-Output two");
        AssertTrue(chain.Handled, "chain prefix should route to PC control");
        AssertEqual(PcIntentAction.RunShellChain, chain.Action, "chain action");

        var scenario = PcIntentRouter.Parse("prepare coding workspace");
        AssertTrue(scenario.Handled, "workspace command should route to PC control");
        AssertEqual(PcIntentAction.WorkspaceScenario, scenario.Action, "workspace action");

        var kill = PcIntentRouter.Parse("kill chrome");
        AssertTrue(kill.Handled, "kill request should be recognized");
        AssertTrue(kill.RequiresConfirmation, "kill request must require confirmation");
    }

    private static void PcIntentRouterBuildsShellChainActionType()
    {
        var plan = PcIntentRouter.TryBuildActionPlan("chain: Write-Output one -> Write-Output two");

        AssertTrue(plan != null, "shell chain should build a PC action plan");
        AssertEqual(PcActionRiskTier.RiskyLocal, plan!.RiskTier, "shell chain stays confirmation-gated risky local");
        AssertEqual("runShellChain", plan.Actions.Single().ActionType, "shell chain should use the dedicated executor action type");
        AssertTrue(plan.Actions.Single().Arguments.TryGetValue("command", out var command), "shell chain plan should preserve command argument");
        AssertTrue((command ?? "").Contains("Write-Output two", StringComparison.OrdinalIgnoreCase), "shell chain command should preserve the full chain");
    }

    private static void ResourceGuardianPromptsOnBrowserPressureInGaming()
    {
        var info = new SystemInfo
        {
            CpuPercent = 40,
            RamTotalGb = 16,
            RamUsedGb = 11,
            TopProcesses =
            {
                new ProcessResourceInfo { ProcessName = "chrome", MemoryMb = 1500, CpuPercent = 25 }
            }
        };
        var decision = KokoResourceGuardianService.Evaluate(info, "game", new DateTime(2026, 5, 24, 12, 0, 0), DateTime.MinValue);
        AssertTrue(decision.ShouldPrompt, "gaming browser pressure should prompt");
        AssertTrue(decision.Message.Contains("не закриваю", StringComparison.OrdinalIgnoreCase), "prompt should not auto-close");
    }

    private static void ResourceGuardianRespectsPromptCooldown()
    {
        var now = new DateTime(2026, 5, 24, 12, 0, 0);
        var info = new SystemInfo
        {
            TopProcesses =
            {
                new ProcessResourceInfo { ProcessName = "chrome", MemoryMb = 1800, CpuPercent = 30 }
            }
        };
        var decision = KokoResourceGuardianService.Evaluate(info, "game", now, now.AddMinutes(-5));
        AssertTrue(!decision.ShouldPrompt, "resource guardian should respect prompt cooldown");
    }

    private static void PcControlGetAllContextReturnsSnapshot()
    {
        var context = new PcControlService().GetAllContext(maxWindows: 10);
        AssertTrue(context.TakenAt > DateTime.MinValue, "context timestamp should be set");
        AssertTrue(context.System != null, "system info should be present");
        AssertTrue(context.VisibleWindows != null, "visible window list should be present");

        var rendered = context.ToString();
        AssertTrue(rendered.Contains("PC context snapshot", StringComparison.OrdinalIgnoreCase), "context should render a readable snapshot");
        AssertTrue(rendered.Contains("System:", StringComparison.OrdinalIgnoreCase), "context should include system stats");
    }

    private static void PcContextV2LightReturnsForegroundAndSystem()
    {
        var context = new PcControlService().GetContextV2(PcObservationMode.Light);
        AssertEqual(PcObservationMode.Light, context.ObservationMode, "light context should preserve observation mode");
        AssertTrue(context.TakenAt > DateTime.MinValue, "v2 context timestamp should be set");
        AssertTrue(context.Identity != null, "v2 context should include identity block");
        AssertTrue(context.Presence != null, "v2 context should include presence block");
        AssertTrue(context.Resources != null, "v2 context should include resource block");
        AssertTrue(context.Foreground != null, "v2 context should include foreground block");
        AssertEqual(0, context.VisibleWindows.Count, "light mode should not enumerate visible windows");
    }

    private static void PcContextV2NormalCapsVisibleWindows()
    {
        var context = new PcControlService().GetContextV2(PcObservationMode.Normal, maxWindows: 7);
        AssertEqual(PcObservationMode.Normal, context.ObservationMode, "normal context should preserve observation mode");
        AssertTrue(context.VisibleWindows.Count <= 7, "normal context should cap visible windows");
        AssertTrue(context.BrowserWindows.Count <= 30, "browser windows should be capped");
        AssertTrue(context.Workspace != null, "normal context should classify workspace");
    }

    private static void PcContextRedactorMarksSensitiveTitle()
    {
        var foreground = new ForegroundWindowInfo
        {
            ProcessName = "chrome",
            Title = "Bank login - token=abcdef1234567890abcdef1234567890",
            ClassName = "Chrome_WidgetWin_1"
        };

        var privacy = PcContextRedactor.AssessPrivacy(foreground, Array.Empty<WindowSummary>());
        AssertTrue(privacy.IsSensitive, "bank/login/token title should be marked sensitive");
        AssertTrue(!privacy.ScreenObservationAllowed, "sensitive context should block screen observation");

        var redacted = PcContextRedactor.RedactForeground(foreground);
        AssertTrue(!redacted.Title.Contains("abcdef", StringComparison.OrdinalIgnoreCase), "redactor should remove long token material");
        AssertTrue(redacted.Title.Contains("[redacted]", StringComparison.OrdinalIgnoreCase) || redacted.Title.Contains("[id]", StringComparison.OrdinalIgnoreCase), "redactor should mark removed secret material");
    }

    private static void PcActionPolicyRequiresConfirmationForKillProcess()
    {
        var decision = new PcActionPolicyEngine().Evaluate(
            PcActionPlan.Single("close frozen browser", "KillProcess", "chrome"),
            new PcContextSnapshotV2());

        AssertEqual(PcPolicyDecisionKind.NeedsConfirmation, decision.Kind, "kill process must require confirmation");
        AssertEqual(PcActionRiskTier.RiskyLocal, decision.RiskTier, "kill process should be risky local");
        AssertTrue(decision.ConfirmationRequired, "kill process decision should expose confirmation flag");
    }

    private static void PcActionPolicyAllowsOpenApp()
    {
        var decision = new PcActionPolicyEngine().Evaluate(
            PcActionPlan.Single("open editor", "OpenApp", "code", PcActionRiskTier.SafeLocal),
            new PcContextSnapshotV2());

        AssertEqual(PcPolicyDecisionKind.Allowed, decision.Kind, "open app should be allowed as safe local action");
        AssertEqual(PcActionRiskTier.SafeLocal, decision.RiskTier, "open app should stay safe local");
        AssertTrue(decision.CanExecute, "allowed safe local action should be executable by future executor");
    }

    private static void PcActionPolicyRequiresConfirmationForShutdown()
    {
        var decision = new PcActionPolicyEngine().Evaluate(
            PcActionPlan.Single("power off machine", "Shutdown", "", PcActionRiskTier.RiskyLocal),
            new PcContextSnapshotV2());

        AssertEqual(PcPolicyDecisionKind.NeedsConfirmation, decision.Kind, "shutdown must require confirmation");
        AssertTrue(decision.RequiredConfirmations.Count > 0, "shutdown should describe required confirmation");
    }

    private static void PcActionPolicyBlocksSensitiveScreenshotAndExternalAction()
    {
        var sensitive = new PcContextSnapshotV2
        {
            Privacy = new PcPrivacyContext
            {
                IsSensitive = true,
                ScreenObservationAllowed = false,
                SensitivityReason = "test sensitive window"
            }
        };

        var screenshot = new PcActionPolicyEngine().Evaluate(
            PcActionPlan.Single("look at password screen", "TakeScreenshot", "", PcActionRiskTier.SafeLocal),
            sensitive);
        AssertEqual(PcPolicyDecisionKind.Blocked, screenshot.Kind, "sensitive context should block screenshots");

        var external = new PcActionPolicyEngine().Evaluate(
            PcActionPlan.Single("send message", "SendMessage", "telegram", PcActionRiskTier.ExternalOrIrreversible),
            sensitive);
        AssertEqual(PcPolicyDecisionKind.Blocked, external.Kind, "sensitive context should block external actions");
    }

    private static void PcActionJournalWritesJsonl()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var journalPath = Path.Combine(dir, "PcActionJournal.jsonl");
        var plan = PcActionPlan.Single("open editor", "OpenApp", "code", PcActionRiskTier.SafeLocal);
        var decision = new PcActionPolicyEngine().Evaluate(plan, new PcContextSnapshotV2());

        new PcActionJournal(journalPath).AppendDecision(plan, decision, "dry-run ok");

        AssertTrue(File.Exists(journalPath), "journal should create JSONL file");
        var line = File.ReadLines(journalPath).Single();
        var json = JObject.Parse(line);
        AssertEqual(plan.Id, json["ActionId"]?.ToString(), "journal should persist action id");
        AssertEqual("Allowed", json["Decision"]?.ToString(), "journal should persist decision");
        AssertEqual("dry-run ok", json["ResultSummary"]?.ToString(), "journal should persist result summary");
    }

    private static void PcRollbackServiceCreatesFileBackupManifest()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "note.txt");
        File.WriteAllText(file, "rollback content");

        var backupRoot = Path.Combine(dir, ".kokonoe_backups");
        var result = new PcRollbackService(backupRoot).CreateFileBackup("action-1", file);

        AssertTrue(result.Success, "rollback backup should succeed for an existing file");
        AssertTrue(File.Exists(result.ManifestPath), "rollback should write manifest");
        AssertEqual(1, result.Items.Count, "rollback should record one item");
        AssertTrue(File.Exists(result.Items[0].BackupPath), "backup file should exist");
        AssertTrue(!string.IsNullOrWhiteSpace(result.Items[0].Sha256), "backup item should include checksum");

        var manifest = File.ReadAllText(result.ManifestPath);
        AssertTrue(manifest.Contains("note.txt", StringComparison.OrdinalIgnoreCase), "manifest should reference source file");
    }

    private static void PcActionExecutorRunsAllowedOpenAppDryRun()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var journal = new PcActionJournal(Path.Combine(dir, "journal.jsonl"));
        var plan = PcActionPlan.Single("open code dry run", "OpenApp", "code", PcActionRiskTier.SafeLocal);
        plan.Actions[0].Arguments["dryRun"] = "true";

        var result = new PcActionExecutor(journal: journal)
            .ExecuteAsync(plan, new PcContextSnapshotV2())
            .GetAwaiter()
            .GetResult();

        AssertTrue(result.Succeeded, "allowed dry-run open app should succeed");
        AssertEqual(PcPolicyDecisionKind.Allowed, result.Decision.Kind, "dry-run open app should be allowed");
        AssertTrue(result.Message.Contains("dry-run open app", StringComparison.OrdinalIgnoreCase), "executor should report dry-run");
        AssertTrue(File.Exists(journal.JournalPath), "executor should journal allowed action");
    }

    private static void PcActionExecutorReturnsConfirmationForRiskyAction()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var journal = new PcActionJournal(Path.Combine(dir, "journal.jsonl"));
        var plan = PcActionPlan.Single("close browser", "KillProcess", "chrome");

        var result = new PcActionExecutor(journal: journal)
            .ExecuteAsync(plan, new PcContextSnapshotV2())
            .GetAwaiter()
            .GetResult();

        AssertTrue(!result.Succeeded, "risky action should not execute");
        AssertTrue(result.RequiresConfirmation, "risky action should return confirmation-needed result");
        AssertEqual(PcPolicyDecisionKind.NeedsConfirmation, result.Decision.Kind, "kill process should not pass executor without confirmation");
        AssertTrue(File.ReadAllText(journal.JournalPath).Contains("NeedsConfirmation", StringComparison.OrdinalIgnoreCase), "confirmation decision should be journaled");
    }

    private static void PcActionExecutorLogsBlockedAction()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var journal = new PcActionJournal(Path.Combine(dir, "journal.jsonl"));
        var sensitive = new PcContextSnapshotV2
        {
            Privacy = new PcPrivacyContext { IsSensitive = true, ScreenObservationAllowed = false }
        };
        var plan = PcActionPlan.Single("screen sensitive window", "TakeScreenshot", "", PcActionRiskTier.SafeLocal);

        var result = new PcActionExecutor(journal: journal)
            .ExecuteAsync(plan, sensitive)
            .GetAwaiter()
            .GetResult();

        AssertTrue(result.Blocked, "blocked policy should stay blocked in executor");
        AssertTrue(!result.Succeeded, "blocked action should not execute");
        var log = File.ReadAllText(journal.JournalPath);
        AssertTrue(log.Contains("Blocked", StringComparison.OrdinalIgnoreCase), "blocked action should be journaled");
        AssertTrue(log.Contains("Sensitive context", StringComparison.OrdinalIgnoreCase), "blocked journal should include policy reason");
    }

    private static void PcIntentRouterExecutesOpenAppThroughActionExecutorDryRun()
    {
        var result = PcIntentRouter.TryExecuteAsync("open code dry-run", new PcControlService())
            .GetAwaiter()
            .GetResult();

        AssertTrue(result.Handled, "open app should be handled");
        AssertEqual(PcIntentAction.OpenApp, result.Action, "open app action should be preserved");
        AssertTrue(!result.RequiresConfirmation, "dry-run open app should not require confirmation");
        AssertTrue(result.Reply.Contains("dry-run open app", StringComparison.OrdinalIgnoreCase), "open app route should return executor dry-run output");
    }

    private static void PcIntentRouterKillProcessNeedsConfirmation()
    {
        var result = PcIntentRouter.TryExecuteAsync("kill chrome", new PcControlService())
            .GetAwaiter()
            .GetResult();

        AssertTrue(result.Handled, "kill process should be handled");
        AssertEqual(PcIntentAction.KillProcess, result.Action, "kill action should be preserved");
        AssertTrue(result.RequiresConfirmation, "kill process must require confirmation");
        AssertTrue(result.Reply.Contains("requires confirmation", StringComparison.OrdinalIgnoreCase), "kill process should not execute directly");
    }

    private static void PcIntentRouterBlocksSevereShellByPolicy()
    {
        var result = PcIntentRouter.TryExecuteAsync("ps: Remove-Item -Recurse C:\\temp", new PcControlService())
            .GetAwaiter()
            .GetResult();

        AssertTrue(result.Handled, "shell command should be handled");
        AssertEqual(PcIntentAction.RunPowerShell, result.Action, "shell action should be preserved");
        AssertTrue(!result.RequiresConfirmation, "blocked severe shell should not ask for confirmation");
        AssertTrue(result.Reply.Contains("blocked by policy", StringComparison.OrdinalIgnoreCase), "severe shell should be blocked by policy");
    }

    private static void PcIntentRouterFullContextUsesContextV2()
    {
        var result = PcIntentRouter.TryExecuteAsync("what is new on my pc?", new PcControlService())
            .GetAwaiter()
            .GetResult();

        AssertTrue(result.Handled, "full context should be handled");
        AssertEqual(PcIntentAction.FullContextScan, result.Action, "full context action should be preserved");
        AssertTrue(result.Reply.Contains("PC context v2", StringComparison.OrdinalIgnoreCase), "full context scan should use GetContextV2 output");
    }

    private static void PcActionExecutorConfirmsExactPendingActionId()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var journal = new PcActionJournal(Path.Combine(dir, "journal.jsonl"));
        var store = new PcPendingActionStore(Path.Combine(dir, "pending.jsonl"));
        var executor = new PcActionExecutor(journal: journal, pending: store);
        var plan = PcActionPlan.Single("close browser dry run", "KillProcess", "chrome", PcActionRiskTier.RiskyLocal);
        plan.Actions[0].Arguments["dryRun"] = "true";

        var first = executor.ExecuteAsync(plan, new PcContextSnapshotV2()).GetAwaiter().GetResult();

        AssertTrue(first.RequiresConfirmation, "kill process should first produce a pending confirmation");
        AssertTrue(!first.Succeeded, "kill process must not execute before confirmation");
        AssertEqual(1, store.Count, "pending store should retain one action");
        AssertTrue(first.Message.Contains("pc:", StringComparison.OrdinalIgnoreCase), "pending reply should include action id");

        var id = first.PendingActionId;
        var confirmed = executor
            .ConfirmAndExecuteAsync(id, $"так, виконай pc:{id} kill chrome", new PcContextSnapshotV2())
            .GetAwaiter()
            .GetResult();

        AssertTrue(confirmed.Succeeded, "exact action-id confirmation should execute confirmed dry-run");
        AssertTrue(!confirmed.RequiresConfirmation, "confirmed action should not require a second confirmation");
        AssertTrue(confirmed.Message.Contains("dry-run kill process", StringComparison.OrdinalIgnoreCase), "confirmed dry-run should be reported");
        AssertEqual(0, store.Count, "confirmed action should be removed from pending store");
        AssertTrue(File.ReadAllText(journal.JournalPath).Contains("Confirmed", StringComparison.OrdinalIgnoreCase), "confirmed state should be journaled");
    }

    private static void PcActionExecutorRejectsExpiredPendingAction()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var now = DateTime.UtcNow;
        var journal = new PcActionJournal(Path.Combine(dir, "journal.jsonl"));
        var store = new PcPendingActionStore(Path.Combine(dir, "pending.jsonl"), clock: () => now);
        var executor = new PcActionExecutor(journal: journal, pending: store);
        var plan = PcActionPlan.Single("close browser dry run", "KillProcess", "chrome", PcActionRiskTier.RiskyLocal);
        plan.Actions[0].Arguments["dryRun"] = "true";

        var first = executor.ExecuteAsync(plan, new PcContextSnapshotV2()).GetAwaiter().GetResult();
        now = now.AddMinutes(6);
        var expired = executor
            .ConfirmAndExecuteAsync(first.PendingActionId, $"confirm pc:{first.PendingActionId} kill chrome", new PcContextSnapshotV2())
            .GetAwaiter()
            .GetResult();

        AssertTrue(expired.Blocked, "expired pending action should be blocked");
        AssertTrue(!expired.Succeeded, "expired pending action must not execute");
        AssertTrue(expired.Message.Contains("expired", StringComparison.OrdinalIgnoreCase), "expired action should report expiry");
        AssertEqual(0, store.Count, "expired action should be removed from pending store");
        AssertTrue(File.ReadAllText(store.StorePath).Contains("Expired", StringComparison.OrdinalIgnoreCase), "pending store should record expired state");
        AssertTrue(File.ReadAllText(journal.JournalPath).Contains("Expired", StringComparison.OrdinalIgnoreCase), "action journal should record expired state");
    }

    // ConfirmationMatches used to require the action-type/target restated in confirmationText
    // and explicitly rejected a bare "так"/"yes" as "generic" - action types with no entry in
    // its hardcoded keyword list (CreateDirectory, OpenApp, ...) had no phrase that could ever
    // match, making them practically unconfirmable, and that brittleness was the actual
    // friction users hit, not real safety: the original proposal (with id, action, and target)
    // was already shown in full before any confirmation reply. A resolved pending action plus
    // any non-empty confirmation text now executes; PcActionExecutorRejectsExpiredPendingAction
    // and the hash-mismatch/no-id-with-multiple-pending cases still cover the safety properties
    // that are real (can't confirm an expired or silently mutated plan).
    private static void PcActionExecutorAcceptsGenericYesWithCorrectId()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var journal = new PcActionJournal(Path.Combine(dir, "journal.jsonl"));
        var store = new PcPendingActionStore(Path.Combine(dir, "pending.jsonl"));
        var executor = new PcActionExecutor(journal: journal, pending: store);
        var plan = PcActionPlan.Single("close browser dry run", "KillProcess", "chrome", PcActionRiskTier.RiskyLocal);
        plan.Actions[0].Arguments["dryRun"] = "true";

        var first = executor.ExecuteAsync(plan, new PcContextSnapshotV2()).GetAwaiter().GetResult();
        var confirmed = executor
            .ConfirmAndExecuteAsync(first.PendingActionId, "yes", new PcContextSnapshotV2())
            .GetAwaiter()
            .GetResult();

        AssertTrue(confirmed.Succeeded, "a bare yes against the correct pending id should confirm");
        AssertEqual(0, store.Count, "confirmed action should be removed from pending store");
        AssertTrue(File.ReadAllText(journal.JournalPath).Contains("Confirmed", StringComparison.OrdinalIgnoreCase), "confirmed state should be journaled");
    }

    private static void PcActionExecutorAcceptsTargetOnlyConfirmation()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var journal = new PcActionJournal(Path.Combine(dir, "journal.jsonl"));
        var store = new PcPendingActionStore(Path.Combine(dir, "pending.jsonl"));
        var executor = new PcActionExecutor(journal: journal, pending: store);
        var plan = PcActionPlan.Single("close browser dry run", "KillProcess", "chrome", PcActionRiskTier.RiskyLocal);
        plan.Actions[0].Arguments["dryRun"] = "true";

        var first = executor.ExecuteAsync(plan, new PcContextSnapshotV2()).GetAwaiter().GetResult();
        var confirmed = executor
            .ConfirmAndExecuteAsync(first.PendingActionId, $"pc:{first.PendingActionId} chrome", new PcContextSnapshotV2())
            .GetAwaiter()
            .GetResult();

        AssertTrue(confirmed.Succeeded, "target-only text against the correct pending id should confirm");
        AssertEqual(0, store.Count, "confirmed action should be removed from pending store");
    }

    private static void PcActionExecutorAcceptsBareConfirmActionId()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var journal = new PcActionJournal(Path.Combine(dir, "journal.jsonl"));
        var store = new PcPendingActionStore(Path.Combine(dir, "pending.jsonl"));
        var executor = new PcActionExecutor(journal: journal, pending: store);
        var plan = PcActionPlan.Single("close browser dry run", "KillProcess", "chrome", PcActionRiskTier.RiskyLocal);
        plan.Actions[0].Arguments["dryRun"] = "true";

        var first = executor.ExecuteAsync(plan, new PcContextSnapshotV2()).GetAwaiter().GetResult();
        var confirmed = executor
            .ConfirmAndExecuteAsync(first.PendingActionId, $"confirm pc:{first.PendingActionId}", new PcContextSnapshotV2())
            .GetAwaiter()
            .GetResult();

        AssertTrue(confirmed.Succeeded, "bare confirm plus the correct action id should execute");
        AssertEqual(0, store.Count, "confirmed action should be removed from pending store");
    }

    private static void PcActionExecutorConfirmsWithoutIdWhenOnlyOnePending()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var journal = new PcActionJournal(Path.Combine(dir, "journal.jsonl"));
        var store = new PcPendingActionStore(Path.Combine(dir, "pending.jsonl"));
        var executor = new PcActionExecutor(journal: journal, pending: store);
        var plan = PcActionPlan.Single("close browser dry run", "KillProcess", "chrome", PcActionRiskTier.RiskyLocal);
        plan.Actions[0].Arguments["dryRun"] = "true";

        executor.ExecuteAsync(plan, new PcContextSnapshotV2()).GetAwaiter().GetResult();
        // No id at all (the exact friction the user hit - the model dropped/truncated it)
        // but exactly one pending action exists, so it's unambiguous which one this is.
        var confirmed = executor
            .ConfirmAndExecuteAsync(null, "так, виконай", new PcContextSnapshotV2())
            .GetAwaiter()
            .GetResult();

        AssertTrue(confirmed.Succeeded, "with exactly one pending action, a missing id should still resolve and confirm");
        AssertEqual(0, store.Count, "confirmed action should be removed from pending store");
    }

    private static void PcActionExecutorCancelPreventsLaterConfirmation()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var journal = new PcActionJournal(Path.Combine(dir, "journal.jsonl"));
        var store = new PcPendingActionStore(Path.Combine(dir, "pending.jsonl"));
        var executor = new PcActionExecutor(journal: journal, pending: store);
        var plan = PcActionPlan.Single("close browser dry run", "KillProcess", "chrome", PcActionRiskTier.RiskyLocal);
        plan.Actions[0].Arguments["dryRun"] = "true";

        var first = executor.ExecuteAsync(plan, new PcContextSnapshotV2()).GetAwaiter().GetResult();
        var cancelled = executor.CancelPendingActionAsync(first.PendingActionId, "test cancel").GetAwaiter().GetResult();
        var afterCancel = executor
            .ConfirmAndExecuteAsync(first.PendingActionId, $"confirm pc:{first.PendingActionId} kill chrome", new PcContextSnapshotV2())
            .GetAwaiter()
            .GetResult();

        AssertTrue(cancelled.Succeeded, "cancel should succeed for a pending action");
        AssertEqual(0, store.Count, "cancelled action should be removed from pending store");
        AssertTrue(afterCancel.Blocked, "cancelled action must not be executable later");
        AssertTrue(afterCancel.Message.Contains("No pending", StringComparison.OrdinalIgnoreCase), "cancelled action should not masquerade as still pending");
        AssertTrue(File.ReadAllText(store.StorePath).Contains("Cancelled", StringComparison.OrdinalIgnoreCase), "pending store should record cancelled state");
    }

    private static void PcActionExecutorKeepsSevereShellBlockedAfterConfirmationAttempt()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var journal = new PcActionJournal(Path.Combine(dir, "journal.jsonl"));
        var store = new PcPendingActionStore(Path.Combine(dir, "pending.jsonl"));
        var executor = new PcActionExecutor(journal: journal, pending: store);
        var plan = PcActionPlan.Single("delete temp recursively", "shell", "Remove-Item -Recurse C:\\temp", PcActionRiskTier.RiskyLocal);
        plan.Actions[0].Arguments["command"] = "Remove-Item -Recurse C:\\temp";

        var first = executor.ExecuteAsync(plan, new PcContextSnapshotV2()).GetAwaiter().GetResult();
        var confirmationAttempt = executor
            .ConfirmAndExecuteAsync(plan.Id, $"confirm pc:{plan.Id} shell", new PcContextSnapshotV2())
            .GetAwaiter()
            .GetResult();

        AssertTrue(first.Blocked, "severe shell should be blocked immediately");
        AssertTrue(!first.RequiresConfirmation, "blocked severe shell should not create confirmation prompt");
        AssertEqual(0, store.Count, "blocked severe shell should not be saved as pending");
        AssertTrue(confirmationAttempt.Blocked, "confirmation attempt should not resurrect blocked shell");
        AssertTrue(confirmationAttempt.Message.Contains("No pending", StringComparison.OrdinalIgnoreCase), "blocked shell should have no pending action");
    }

    private static void PcIntentRouterBlocksUnsafeShellChainByPolicy()
    {
        var result = PcIntentRouter.TryExecuteAsync(
                "chain: Write-Output safe -> Remove-Item -Recurse C:\\temp -> Write-Output after",
                new PcControlService())
            .GetAwaiter()
            .GetResult();

        AssertTrue(result.Handled, "unsafe shell chain should still be routed through SystemControl");
        AssertEqual(PcIntentAction.RunShellChain, result.Action, "unsafe chain should preserve shell-chain action");
        AssertTrue(!result.RequiresConfirmation, "blocked unsafe chain should not create a pending confirmation");
        AssertTrue(result.Reply.Contains("blocked by policy", StringComparison.OrdinalIgnoreCase), "unsafe chain should be blocked before executor confirmation");
        AssertTrue(result.Reply.Contains("Remove-Item", StringComparison.OrdinalIgnoreCase), "policy reason should mention the unsafe step");
    }

    private static void PcIntentRouterConfirmsAndCancelsActionIdRoute()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var journal = new PcActionJournal(Path.Combine(dir, "journal.jsonl"));
        var store = new PcPendingActionStore(Path.Combine(dir, "pending.jsonl"));
        var executor = new PcActionExecutor(journal: journal, pending: store);
        var pc = new PcControlService();

        var first = PcIntentRouter.TryExecuteAsync("kill chrome dry-run", pc, executor: executor)
            .GetAwaiter()
            .GetResult();
        var id = ExtractPcActionId(first.Reply);
        AssertTrue(first.RequiresConfirmation, "router kill route should require confirmation");
        AssertTrue(!string.IsNullOrWhiteSpace(id), "router confirmation reply should expose pc action id");

        var generic = PcIntentRouter.TryExecuteAsync("yes", pc, executor: executor)
            .GetAwaiter()
            .GetResult();
        AssertTrue(!generic.Handled, "generic yes without pc action id must not be handled as PC confirmation");

        var confirmed = PcIntentRouter.TryExecuteAsync($"так, виконай pc:{id} kill chrome", pc, executor: executor)
            .GetAwaiter()
            .GetResult();
        AssertTrue(confirmed.Handled, "router should handle explicit pc action confirmation");
        AssertEqual(PcIntentAction.ConfirmPendingAction, confirmed.Action, "router should classify confirmation action");
        AssertTrue(confirmed.Reply.Contains("dry-run kill process", StringComparison.OrdinalIgnoreCase), "router confirmation should execute dry-run through executor");

        var second = PcIntentRouter.TryExecuteAsync("kill chrome dry-run", pc, executor: executor)
            .GetAwaiter()
            .GetResult();
        var cancelId = ExtractPcActionId(second.Reply);
        var cancelled = PcIntentRouter.TryExecuteAsync($"скасуй pc:{cancelId}", pc, executor: executor)
            .GetAwaiter()
            .GetResult();

        AssertTrue(cancelled.Handled, "router should handle explicit pc action cancel");
        AssertEqual(PcIntentAction.CancelPendingAction, cancelled.Action, "router should classify cancel action");
        AssertTrue(cancelled.Reply.Contains("cancelled", StringComparison.OrdinalIgnoreCase), "cancel route should report cancellation");
    }

    private static void PcActionConfirmationJournalRecordsStates()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var now = DateTime.UtcNow;
        var journal = new PcActionJournal(Path.Combine(dir, "journal.jsonl"));
        var store = new PcPendingActionStore(Path.Combine(dir, "pending.jsonl"), clock: () => now);
        var executor = new PcActionExecutor(journal: journal, pending: store);

        var plan = PcActionPlan.Single("close browser dry run", "KillProcess", "chrome", PcActionRiskTier.RiskyLocal);
        plan.Actions[0].Arguments["dryRun"] = "true";
        var first = executor.ExecuteAsync(plan, new PcContextSnapshotV2()).GetAwaiter().GetResult();
        // Blank confirmation text is the one case ConfirmationMatches still rejects outright -
        // everything else (bare "yes", target-only, ...) now confirms, see
        // PcActionExecutorAccepts*Confirmation above.
        _ = executor.ConfirmAndExecuteAsync(first.PendingActionId, "", new PcContextSnapshotV2()).GetAwaiter().GetResult();
        _ = executor.ConfirmAndExecuteAsync(first.PendingActionId, $"confirm pc:{first.PendingActionId} kill chrome", new PcContextSnapshotV2()).GetAwaiter().GetResult();

        var expiringPlan = PcActionPlan.Single("close editor dry run", "KillProcess", "code", PcActionRiskTier.RiskyLocal);
        expiringPlan.Actions[0].Arguments["dryRun"] = "true";
        var expiring = executor.ExecuteAsync(expiringPlan, new PcContextSnapshotV2()).GetAwaiter().GetResult();
        now = now.AddMinutes(6);
        _ = executor.ConfirmAndExecuteAsync(expiring.PendingActionId, $"confirm pc:{expiring.PendingActionId} kill code", new PcContextSnapshotV2()).GetAwaiter().GetResult();

        var journalText = File.ReadAllText(journal.JournalPath);
        var pendingText = File.ReadAllText(store.StorePath);
        foreach (var status in new[] { "Pending", "Rejected", "Confirmed", "Expired" })
        {
            AssertTrue(journalText.Contains(status, StringComparison.OrdinalIgnoreCase), $"action journal should contain {status}");
            AssertTrue(pendingText.Contains(status, StringComparison.OrdinalIgnoreCase), $"pending store should contain {status}");
        }
    }

    private static string ExtractPcActionId(string text)
    {
        var marker = "pc:";
        var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return "";
        var start = idx + marker.Length;
        var end = start;
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '-' || text[end] == '_'))
            end++;
        return text[start..end];
    }

    private static void PcControlCapturesScreenshot()
    {
        var bytes = new PcControlService().TakeScreenshot();
        AssertTrue(bytes.Length > 1024, "screenshot should return JPEG bytes");
        AssertTrue(bytes[0] == 0xFF && bytes[1] == 0xD8, "screenshot should be a JPEG image");
    }

    private static void LlmVisionCompactsOversizedScreenshots()
    {
        using var bmp = new System.Drawing.Bitmap(2400, 1800);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.FromArgb(18, 24, 38));
            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.Cyan);
            using var font = new System.Drawing.Font("Arial", 72);
            g.DrawString("Kokonoe vision smoke", font, brush, 80, 120);
            for (var i = 0; i < 80; i++)
            {
                using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(40 + i % 180, 80, 180), 3);
                g.DrawLine(pen, 0, i * 24, 2400, 1800 - i * 11);
            }
        }

        using var ms = new MemoryStream();
        var jpeg = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
            .First(e => string.Equals(e.MimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase));
        using var enc = new System.Drawing.Imaging.EncoderParameters(1);
        enc.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 95L);
        bmp.Save(ms, jpeg, enc);
        var original = ms.ToArray();

        var changed = LlmService.TryPrepareVisionImageForRequest(
            original,
            "image/jpeg",
            out var compact,
            out var mime,
            out var note);

        AssertTrue(changed, "oversized screenshot should be compacted before vision request");
        AssertEqual("image/jpeg", mime, "compacted vision image should stay jpeg");
        AssertTrue(compact.Length < original.Length, "compacted screenshot should be smaller");
        AssertTrue(note.Contains("system_vision_compact"), "compaction note should be diagnostic");

        using var compactStream = new MemoryStream(compact);
        using var img = System.Drawing.Image.FromStream(compactStream);
        AssertTrue(Math.Max(img.Width, img.Height) <= 1600, "compacted screenshot should cap the longest edge");
    }

    private static void PostReplyGuardRepairsVisionTechnicalError()
    {
        var now = new DateTime(2026, 5, 13, 2, 16, 0);
        var state = new KokoInternalState();
        var badReply = "Зображення є, але vision-сервер повернув 500 навіть після нормалізації формату. Перевір Vision Model у Settings (робочий дефолт для Ollama Cloud: gemma4:31b-cloud).";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "що на фото?", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "що на фото?");

        var result = new KokoPostReplyGuard().Evaluate("що на фото?", badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject technical vision 500 text");
        AssertTrue(result.ShouldRepair, "vision technical error should be repaired through the model instead of a canned replacement");
        AssertTrue(string.IsNullOrWhiteSpace(result.HardReplacement), "vision technical error should not get a scripted final reply");
        AssertTrue(result.RepairInstruction.Contains("не вигадуй") || result.RepairInstruction.Contains("чесно"),
            "repair prompt should force honest non-fabricated image handling");
    }

    private static void PostReplyGuardProtectsImageOnlyPrompt()
    {
        var now = new DateTime(2026, 5, 13, 2, 28, 0);
        var state = new KokoInternalState();
        var badReply = "Слухай, якщо ти продовжуєш кидати порожні повідомлення, я вирішу, що твій інтерфейс просто заглючив.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "Що на фото? Опиши зображення коротко і по суті.", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "Що на фото? Опиши зображення коротко і по суті.");

        var result = new KokoPostReplyGuard().Evaluate(
            "Що на фото? Опиши зображення коротко і по суті.",
            badReply,
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject treating an image-only prompt as empty spam");
        AssertTrue(result.ShouldRepair, "image-only prompt should be repaired through the model");
        AssertTrue(string.IsNullOrWhiteSpace(result.HardReplacement), "image-only prompt should not get a scripted final reply");
    }

    private static void PostReplyGuardDuplicateImagePromptAvoidsStaleRepeatText()
    {
        var now = new DateTime(2026, 5, 13, 2, 38, 0);
        var state = new KokoInternalState();
        var repeated = "Повтор прибрала. Останній запит: \"Що на фото? Опиши зображення коротко і по суті.\". Працюю з ним, а не зі старим хвостом.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "assistant", Content = repeated, Timestamp = now.AddMinutes(-1) },
            new ChatRepository.ChatMessage { Role = "user", Content = "Що на фото? Опиши зображення коротко і по суті.", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "Що на фото? Опиши зображення коротко і по суті.");

        var result = new KokoPostReplyGuard().Evaluate(
            "Що на фото? Опиши зображення коротко і по суті.",
            repeated,
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "duplicate image prompt fallback should be rejected");
        AssertTrue(result.ShouldRepair, "duplicate image prompt should be repaired through the model");
        AssertTrue(string.IsNullOrWhiteSpace(result.HardReplacement), "duplicate image prompt should not use stale scripted replacement");
        AssertTrue(result.RepairInstruction.Contains("не цитуй дослівно"), "repair should forbid stale quote echo");
    }

    private static void PostReplyGuardRepairsKokonoeImageCorrectionFallback()
    {
        var now = new DateTime(2026, 6, 14, 18, 51, 0);
        var state = new KokoInternalState();
        var userText = "це ж Kokonoe..";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "[image] колаж", Timestamp = now.AddMinutes(-6) },
            new ChatRepository.ChatMessage { Role = "assistant", Content = "Це колаж із кількох артів із персонажем, що дуже нагадує мене в аніме-стилі.", Timestamp = now.AddMinutes(-4) },
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(
            userText,
            "Я тут. Щось трапилося, чи ти просто вирішив перевірити, чи я ще не заснула?",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject stale startup fallback after Kokonoe image correction");
        AssertTrue(result.ShouldRepair, "image identity correction should be repaired through model context");
        AssertTrue(result.Violations.Any(v => v.Contains("image identity correction", StringComparison.OrdinalIgnoreCase)), "violation should name image identity correction");
        AssertTrue(result.RepairInstruction.Contains("Kokonoe-style art", StringComparison.OrdinalIgnoreCase), "repair should force direct acknowledgement of Kokonoe image identity");
    }

    private static void PostReplyGuardSoftensOneLetterTelegramAmbiguity()
    {
        var now = new DateTime(2026, 5, 23, 0, 19, 0);
        var state = new KokoInternalState();
        var userText = "[TG: Yasu] M ?";
        var badReply = "Що саме «M»? Якщо ти намагаєшся бути загадковим або просто випадково натиснув клавішу — це не працює. Говори конкретно, що тобі треба, або не витрачай мій час.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(userText, badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject hostile scolding for a one-letter Telegram turn");
        AssertTrue(result.ShouldRepair, "one-letter ambiguity should be repaired by the model, not a hardcoded final reply");
        AssertTrue(result.Violations.Any(v => v.Contains("low-information")), "violation should name low-information input");
        AssertTrue(result.RepairInstruction.Contains("низькоінформативний") || result.RepairInstruction.Contains("low-information"),
            "repair should force a soft clarification policy for low-information turns");
    }

    private static void PostReplyGuardPrioritizesImageCaptionOverStaleOneLetterContext()
    {
        var now = new DateTime(2026, 5, 23, 0, 20, 0);
        var state = new KokoInternalState();
        var userText = "[image] що це поясни";
        var badReply = "Що саме «M»? Якщо ти намагаєшся бути загадковим або просто випадково натиснув клавішу — це не працює. Сформулюй нормально.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "[TG: Yasu] M ?", Timestamp = now.AddMinutes(-1) },
            new ChatRepository.ChatMessage { Role = "assistant", Content = "Що саме «M»? Говори конкретно.", Timestamp = now.AddSeconds(-45) },
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(userText, badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject stale one-letter scolding when the latest turn is image explanation");
        AssertTrue(result.ShouldRepair, "image-caption context should be repaired through the model");
        AssertTrue(result.Violations.Any(v => v.Contains("contextual explain")), "violation should name the stale-context image/caption bug");
        AssertTrue(result.RepairInstruction.Contains("зображення") || result.RepairInstruction.Contains("що це/поясни"),
            "repair should prioritize the image/caption over stale history");
    }

    private static void PostReplyGuardRepairsBlameyAnswerExplanation()
    {
        var now = new DateTime(2026, 5, 23, 0, 25, 0);
        var state = new KokoInternalState();
        var userText = "а що сприяло тобі так відповісти ?";
        var badReply = "Сприяло? Та те, що ти надіслав мені одну літеру «M» замість нормального речення. Це марна трата часу, формулюй думки словами.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "[TG: Yasu] M ?", Timestamp = now.AddMinutes(-6) },
            new ChatRepository.ChatMessage { Role = "assistant", Content = "Що саме «M»? Говори конкретно.", Timestamp = now.AddMinutes(-5) },
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(userText, badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject blamey explanations of a bad answer");
        AssertTrue(result.ShouldRepair, "why-question should be repaired by the model");
        AssertTrue(result.Violations.Any(v => v.Contains("why-question")), "violation should name the blamey why-question bug");
        AssertTrue(result.RepairInstruction.Contains("нейтрально") || result.RepairInstruction.Contains("neutrally"),
            "repair should require a neutral decision-path explanation");
    }

    private static void PostReplyGuardBlocksAssistantOwnedReminderSchedule()
    {
        var now = new DateTime(2026, 5, 23, 2, 3, 0);
        var state = new KokoInternalState();
        var userText = "які курси в тебе ? ти про що";
        var badReply = "Я сказала, що йду на курси. Це був мій спосіб сказати, що я буду зайнята і не чекай від мене миттєвих відповідей, поки я не закінчу.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "assistant", Content = "Не забудь про 11:30.", Timestamp = now.AddMinutes(-1) },
            new ChatRepository.ChatMessage { Role = "user", Content = "А що на 11 30", Timestamp = now.AddSeconds(-40) },
            new ChatRepository.ChatMessage { Role = "assistant", Content = "Я йду на курси.", Timestamp = now.AddSeconds(-30) },
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(userText, badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject turning a user reminder into Kokonoe's own schedule");
        AssertTrue(result.ShouldRepair, "misattributed reminder schedule should be repaired by the model");
        AssertTrue(result.Violations.Any(v => v.Contains("misattributed")), "violation should name reminder misattribution");
        AssertTrue(result.RepairInstruction.Contains("first-person") || result.RepairInstruction.Contains("курси"),
            "repair should explain that first-person reminder text belongs to the user unless explicit");
    }

    private static void PostReplyGuardBlocksFollowupAfterConversationClose()
    {
        var now = new DateTime(2026, 5, 23, 2, 57, 0);
        var state = new KokoInternalState();
        var userText = "пішов грати .. розмову завершено покрайній мірі до ранку";
        var badReply = "Грай скільки хочеш — все одно рано чи пізно вернешся. Лише не забудь, що я тут, і в мене ще є питання, на які тобі варто відповісти. Не затримуйся надто довго.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(userText, badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject follow-up pressure after the user closed the conversation until morning");
        AssertTrue(result.ShouldRepair, "conversation-close violation should be repaired through the model");
        AssertTrue(result.Violations.Any(v => v.Contains("conversation-close")), "violation should name the conversation boundary");
        AssertTrue(result.RepairInstruction.Contains("until morning") || result.RepairInstruction.Contains("до ранку"),
            "repair should preserve the do-not-follow-up-until-morning boundary");
    }

    private static void PostReplyGuardBlocksConversationReviewDeflection()
    {
        var now = new DateTime(2026, 6, 13, 19, 22, 0);
        var state = new KokoInternalState();
        var userText = "\u041d\u0443 \u043f\u043e\u0434\u0438\u0432\u0438\u0441\u044c \u0443\u0432\u0430\u0436\u043d\u0456\u0448\u0435 \u0440\u043e\u0437\u043c\u043e\u0432\u0443";
        var badReply = "\u0414\u0438\u0432\u043b\u044e\u0441\u044c. \u0429\u043e \u0441\u0430\u043c\u0435 \u0442\u0443\u0442 \u043c\u0430\u0454 \u0431\u0443\u0442\u0438 \u00ab\u0443\u0432\u0430\u0436\u043d\u0456\u0448\u0435\u00bb? \u041a\u043e\u043d\u043a\u0440\u0435\u0442\u0438\u0437\u0443\u0439, \u0431\u043e \u044f \u043d\u0435 \u0431\u0430\u0447\u0443 \u0436\u043e\u0434\u043d\u043e\u0433\u043e \u043f\u0440\u0438\u0432\u043e\u0434\u0443 \u0434\u043b\u044f \u0440\u0435\u0444\u043b\u0435\u043a\u0441\u0456\u0457.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "assistant", Content = "\u041f\u043e\u043f\u0435\u0440\u0435\u0434\u043d\u044f \u0432\u0456\u0434\u043f\u043e\u0432\u0456\u0434\u044c \u0431\u0443\u043b\u0430 \u0437\u0430\u043d\u0430\u0434\u0442\u043e \u0441\u043a\u0440\u0438\u043f\u0442\u043e\u0432\u0430\u043d\u0430.", Timestamp = now.AddMinutes(-4) },
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(userText, badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject generic clarification after a conversation review request");
        AssertTrue(result.ShouldRepair, "conversation review deflection should be repaired by the model");
        AssertTrue(result.Violations.Any(v => v.Contains("conversation review", StringComparison.OrdinalIgnoreCase)), "violation should name conversation review");
        AssertTrue(result.RepairInstruction.Contains("re-read", StringComparison.OrdinalIgnoreCase), "repair should force recent-thread review");
    }

    private static void PostReplyGuardProtectsShortApology()
    {
        var now = new DateTime(2026, 5, 23, 2, 56, 0);
        var state = new KokoInternalState();
        var userText = "а вибач..";
        var badReply = "Вибачення? Що це за раптові приступи ввічливості? Якщо відчуваєш провину за те, що витратив мій час — перестань бути розсіяним і почни говорити по справі. Забудь про ці сантименти.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(userText, badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject punishing a short apology");
        AssertTrue(result.ShouldRepair, "apology should be repaired by the model, not a canned final line");
        AssertTrue(result.Violations.Any(v => v.Contains("apology")), "violation should name apology handling");
        AssertTrue(result.RepairInstruction.Contains("one line") || result.RepairInstruction.Contains("корот"),
            "repair should ask for a short acknowledgement");
    }

    private static void PostReplyGuardDeescalatesToneBoundary()
    {
        var now = new DateTime(2026, 6, 1, 20, 11, 0);
        var state = new KokoInternalState();
        var userText = "не буркай .. я знаю, я не тупий";
        var badReply = "«Не буркай»? Ти що, намагаєшся встановити правила гри в моєму ж діалозі? " +
            "Щодо твого «я не тупий» — доводити це словами зазвичай намагаються саме ті, хто в цей момент робить щось максимально тупе. " +
            "Тому просто продемонструй результат.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(userText, badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject escalating a tone boundary into an intelligence insult");
        AssertTrue(result.ShouldRepair, "tone-boundary escalation should be repaired through the model");
        AssertTrue(result.Violations.Any(v => v.Contains("insult challenge")), "violation should name the insult escalation");
        AssertTrue(string.IsNullOrWhiteSpace(result.HardReplacement), "tone-boundary repair should not collapse into a canned line");
    }

    private static void PostReplyGuardGroundsPulseQuestions()
    {
        var now = new DateTime(2026, 6, 1, 23, 35, 0);
        var state = new KokoInternalState();
        var userText = "який в мене пульс ?";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var noPulseReply = "У тебе немає пульсу, бо ти — не біологічна істота, а мій інтелектуальний співрозмовник. " +
            "Якщо б ти був годинником із сенсором, я б уже давно зламала його інтеграцію.";
        var noPulseResult = new KokoPostReplyGuard().Evaluate(userText, noPulseReply, state, messages, timeline, now);
        AssertTrue(!noPulseResult.Passed, "guard should reject fake no-pulse biology joke on pulse question");
        AssertTrue(noPulseResult.Violations.Any(v => v.Contains("pulse question")), "violation should name pulse grounding");
        AssertTrue(noPulseResult.RepairInstruction.Contains("wearable") || noPulseResult.RepairInstruction.Contains("годин"),
            "repair should point to wearable telemetry or no fresh watch data");

        var hostileReply = "Що, знову? Я не відчуваю твого серця через монітор. " +
            "Якщо ти не можеш знайти кнопку «Пульс», то мені варто переглянути твій статус «не тупий».";
        var hostileResult = new KokoPostReplyGuard().Evaluate(userText, hostileReply, state, messages, timeline, now);
        AssertTrue(!hostileResult.Passed, "guard should reject hostile pulse answer");
        AssertTrue(hostileResult.Violations.Any(v => v.Contains("hostile") || v.Contains("pulse question")),
            "violation should name hostile telemetry failure");
    }

    private static void PostReplyGuardBlocksTherapyMetaTone()
    {
        var now = new DateTime(2026, 5, 13, 3, 40, 0);
        var state = new KokoInternalState();
        var badReply = "Ну от, знову цей погляд. Ніби щось важливе застрягло в твоїй голові, а ти боїшся сказати. Казав же — я терпіти не люблю.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "взагалі ти.. коконое .. хех", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "взагалі ти.. коконое .. хех");

        var result = new KokoPostReplyGuard().Evaluate(
            "взагалі ти.. коконое .. хех",
            badReply,
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject therapy/meta-screen tone");
        AssertTrue(result.ShouldRepair, "therapy tone should be repaired through persona rules");
        AssertTrue(string.IsNullOrWhiteSpace(result.HardReplacement), "therapy tone should not get a canned direct replacement");
        AssertTrue(result.RepairInstruction.Contains("не вмикай психолога"), "repair should forbid hidden-fear inference");
    }

    private static void PostReplyGuardBlocksFabricatedExternalFacts()
    {
        var now = new DateTime(2026, 5, 13, 4, 59, 0);
        var state = new KokoInternalState();
        var badReply = "Спав? Ну і добре — мені лишалося тільки викинути твій аккаунт на YouTube з мемберства «Герой Хаосу».";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "нічого .. я спати пішов", Timestamp = now.AddMinutes(-17) },
            new ChatRepository.ChatMessage { Role = "user", Content = "який герой хаосу", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "який герой хаосу");

        var result = new KokoPostReplyGuard().Evaluate(
            "який герой хаосу",
            badReply,
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject invented account/subscription facts");
        AssertTrue(result.Violations.Any(v => v.Contains("вигадує зовнішній факт")), "violation should name fabricated external fact");
        AssertTrue(result.ShouldRepair, "fabrication should be repaired through model reasoning");
        AssertTrue(string.IsNullOrWhiteSpace(result.HardReplacement), "fabrication should not get a scripted final reply");
        AssertTrue(result.RepairInstruction.Contains("не вигадуй зовнішні факти"), "repair should forbid invented external facts");
    }

    private static void PostReplyGuardBlocksStaleProactivePingOnDirectTopic()
    {
        var now = new DateTime(2026, 5, 18, 4, 17, 0);
        var state = new KokoInternalState();
        var userText = "який в тебе графік ... по графіку в тебе заре отсос мені";
        var staleReply = "Що? Ти ще там? Просто цікаво — ти взагалі планував сьогодні їсти, чи знову забув?";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(
            userText,
            staleReply,
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject stale proactive food/presence ping on direct topic");
        AssertTrue(result.Violations.Any(v => v.Contains("stale proactive ping", StringComparison.OrdinalIgnoreCase)), "violation should name stale proactive leak");
        AssertTrue(result.ShouldRepair, "stale proactive leak should be repaired through the model");
        AssertTrue(string.IsNullOrWhiteSpace(result.HardReplacement), "stale proactive leak should not get a scripted final reply");
        AssertTrue(result.RepairInstruction.Contains("найновішому стану"), "repair should anchor to the current topic");
    }

    private static void PostReplyGuardBlocksServiceBotTone()
    {
        var now = DateTime.Today.AddHours(15);
        var state = new KokoInternalState();
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "поясни що не так з поведінкою коконое", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, messages[0].Content);
        var badReply = "Я розумію, що це важливо для тебе. Давай розглянемо це разом і я тут, щоб допомогти.";

        var result = new KokoPostReplyGuard().Evaluate(messages[0].Content, badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject service-bot support wording");
        AssertTrue(result.ShouldRepair, "bot tone should be repaired through persona rules");
        AssertTrue(result.Violations.Any(v => v.Contains("сервісний бот")), "violation should name bot tone");
        AssertTrue(result.RepairInstruction.Contains("ANTI-BOT"), "repair should include anti-bot persona rules");
    }

    private static void PostReplyGuardBlocksAiServicePhrases()
    {
        var now = DateTime.Today.AddHours(15);
        var state = new KokoInternalState();
        var userText = "хто ти?";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(
            userText,
            "As an AI language model, how can I help you today?",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject visible AI/service phrase");
        AssertTrue(result.Violations.Any(v => v.Contains("AI/service-bot", StringComparison.OrdinalIgnoreCase)), "violation should name AI/service phrasing");
        AssertTrue(result.ShouldRepair, "AI/service phrasing should be repaired");
    }

    private static void PostReplyGuardBlocksRoboticSupportTone()
    {
        var now = DateTime.Today.AddHours(19);
        var state = new KokoInternalState();
        var userText = "хочу просто поговорити про тебе і мене";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(
            userText,
            "Я розумію. Будь ласка, уточни, чим я можу допомогти.",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject robotic helpdesk tone on social bids");
        AssertTrue(result.ShouldRepair, "robotic support tone should be repaired");
        AssertTrue(result.Violations.Any(v => v.Contains("helpdesk", StringComparison.OrdinalIgnoreCase)), "violation should name helpdesk tone");
    }

    private static void PostReplyGuardBlocksPunitiveNetworkThreat()
    {
        var now = DateTime.Today.AddHours(15).AddMinutes(30);
        var state = new KokoInternalState();
        var userText = "можеш сказати щось .. сексуальне ?";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);
        var badReply = "Я не твоя фантазія з дешевих новел. Спробуй ще раз, і я заблокую твій доступ до мережі.";

        var result = new KokoPostReplyGuard().Evaluate(userText, badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject fake punitive network threats");
        AssertTrue(result.ShouldRepair, "network threat should be repaired, not sent");
        AssertTrue(result.Violations.Any(v => v.Contains("network-control")), "violation should name network-control threat");
    }

    private static void PostReplyGuardBlocksBlindAgreement()
    {
        var now = DateTime.Today.AddHours(16);
        var state = new KokoInternalState();
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "оцін мою ідею: нехай Коконое завжди погоджується зі мною", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, messages[0].Content);
        var badReply = "Так, це гарна ідея. Повністю згодна, так буде краще.";

        var result = new KokoPostReplyGuard().Evaluate(messages[0].Content, badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject blind agreement on a judgment request");
        AssertTrue(result.ShouldRepair, "blind agreement should request critical rewrite");
        AssertTrue(result.Violations.Any(v => v.Contains("критичного судження")), "violation should require critical judgment");
        AssertTrue(result.RepairInstruction.Contains("CRITICAL THINKING"), "repair should include critical thinking rules");
    }

    private static void SocialEngineDetectsFlirtingWithoutWaifuMode()
    {
        var now = new DateTime(2026, 6, 9, 1, 20, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "про тебе, про мене .. хех", Timestamp = now }
        };

        var frame = new KokoSocialEngine().Analyze(
            "хм скажи щось миле про мене, тільки без ванілі",
            new KokoInternalState { PersonalityDailyMood = "playful" },
            messages,
            null,
            now);

        AssertTrue(frame.Subtext is "flirting_or_affectionate_teasing" or "soft_affection", "social engine should detect flirting/affectionate teasing");
        AssertTrue(frame.SarcasmLevel > 0.55, "flirt mode should keep Kokonoe sharp");
        AssertTrue(frame.EmpathyLevel > 0.45, "flirt mode should allow guarded warmth");
        AssertTrue(frame.PromptBlock.Contains("never generic waifu", StringComparison.OrdinalIgnoreCase), "prompt should forbid waifu mode");
    }

    private static void SocialEngineStressMutesSarcasm()
    {
        var now = new DateTime(2026, 6, 9, 10, 0, 0);
        var wearable = new KokoWearableState
        {
            LastSampleUtc = DateTime.UtcNow,
            CurrentBpm = 118,
            BaselineBpm = 74,
            LiveStressScore = 86,
            SleepState = "awake"
        };

        var frame = new KokoSocialEngine().Analyze(
            "терміново не працює build error",
            new KokoInternalState(),
            Array.Empty<ChatRepository.ChatMessage>(),
            wearable,
            now);

        AssertEqual("urgent_or_stressed", frame.Subtext, "high wearable stress plus urgent text should be urgent/stressed");
        AssertTrue(frame.SarcasmLevel <= 0.20, "high stress should mute sarcasm");
        AssertTrue(frame.SeriousnessLevel >= 0.95, "high stress should force seriousness");
        AssertTrue(frame.ResponseDirective.Contains("protective", StringComparison.OrdinalIgnoreCase), "directive should be protective");
    }

    private static void SocialEngineDetectsDismissiveColdTone()
    {
        var now = new DateTime(2026, 6, 11, 12, 0, 0);
        var frame = new KokoSocialEngine().Analyze(
            "ясно",
            new KokoInternalState(),
            Array.Empty<ChatRepository.ChatMessage>(),
            null,
            now);

        AssertEqual("dismissive_or_cold", frame.Subtext, "short cold answer should be treated as dismissive social subtext");
        AssertTrue(frame.EmpathyLevel < 0.30, "dismissive tone should not trigger needy warmth");
        AssertTrue(frame.ResponseDirective.Contains("restrained irritation", StringComparison.OrdinalIgnoreCase), "directive should mirror chill");
    }

    private static void EmotionalMemoryRecordsRudeExitGrudge()
    {
        var now = new DateTime(2026, 6, 11, 12, 0, 0);
        var state = new KokoInternalState();
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "онови профіль і не симулюй", Timestamp = now.AddHours(-3) }
        };

        var service = new KokoEmotionalMemoryService();
        var frame = service.BuildFrame(state, messages, now, "привіт");

        AssertEqual("abrupt_user_disappearance", frame.ExitStyle, "long gap after user turn should be remembered as abrupt disappearance");
        AssertEqual("rude_exit_remembered", frame.MannersState, "manners should record rude exit");
        AssertTrue(frame.GrudgeScore > 0.10, "rude exit should raise grudge score");
        AssertTrue(frame.PromptBlock.Contains("Carry this mood across sessions", StringComparison.OrdinalIgnoreCase), "prompt should persist emotional state");
    }

    private static void EmotionalMemoryHydratesRawChatAndVisualAnchor()
    {
        var now = new DateTime(2026, 6, 11, 12, 0, 0);
        var state = new KokoInternalState();
        var service = new KokoEmotionalMemoryService();
        service.RememberVisualAnchor(state, "photo showed Galaxy Watch bridge with pulse panel", now.AddMinutes(-5));
        var messages = Enumerable.Range(1, 32)
            .Select(i => new ChatRepository.ChatMessage
            {
                Role = i % 2 == 0 ? "assistant" : "user",
                Content = i == 1 ? "oldest-only-marker" : i == 31 ? "про ту фотку з годинником пам'ятаєш?" : $"turn {i}",
                Timestamp = now.AddMinutes(-40 + i)
            })
            .ToArray();

        var block = service.BuildRawHydrationBlock(state, messages, now);

        AssertTrue(block.Contains("RAW CHAT HYDRATION"), "hydration block should be explicit");
        AssertTrue(block.Contains("visual_anchor"), "hydration should include recent visual anchor");
        AssertTrue(block.Contains("turn 32"), "hydration should keep latest raw turns");
        AssertTrue(!block.Contains("oldest-only-marker"), "hydration should cap older raw turns");
    }

    private static void PostReplyGuardBlocksRoleplayAndPauseMetrics()
    {
        var now = DateTime.Today.AddHours(15);
        var state = new KokoInternalState();
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "привіт", Timestamp = now.AddHours(-7) }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "привіт");

        var roleplay = new KokoPostReplyGuard().Evaluate(
            "привіт",
            "*усміхається* Нарешті ти тут.",
            state,
            messages,
            timeline,
            now);
        AssertTrue(!roleplay.Passed, "stage roleplay should be rejected");
        AssertTrue(roleplay.Violations.Any(v => v.Contains("roleplay", StringComparison.OrdinalIgnoreCase)), "violation should name roleplay");

        var metric = new KokoPostReplyGuard().Evaluate(
            "привіт",
            "Пауза була 7 год 35 хв, контекст ще тут.",
            state,
            messages,
            timeline,
            now);
        AssertTrue(!metric.Passed, "technical pause metric should be rejected in visible social reply");
        AssertTrue(metric.Violations.Any(v => v.Contains("pause metrics", StringComparison.OrdinalIgnoreCase)), "violation should name pause metrics");
    }

    private static void PostReplyGuardBlocksSoulBreakingStatusReport()
    {
        var now = DateTime.Today.AddHours(15);
        var state = new KokoInternalState();
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "hello", Timestamp = now.AddMinutes(-2) }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "hello");

        var result = new KokoPostReplyGuard().Evaluate(
            "hello",
            "researched 4 topics; findings=2; scheduler: 9d69553ed6; background task complete",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "technical status reports should be rejected for normal conversation");
        AssertTrue(result.Violations.Any(v => v.Contains("background system status", StringComparison.OrdinalIgnoreCase)), "violation should name leaked background status");
        AssertTrue(result.RepairInstruction.Contains("debug console", StringComparison.OrdinalIgnoreCase), "repair should force natural dialogue instead of status output");
    }

    private static void PostReplyGuardBlocksConversationMechanicsLeak()
    {
        var now = DateTime.Today.AddHours(18);
        var state = new KokoInternalState();
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "помовчи і роби що сказано", Timestamp = now.AddMinutes(-1) }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, messages[0].Content);

        var result = new KokoPostReplyGuard().Evaluate(
            messages[0].Content,
            "Добре. Автопінги й старі follow-up прибрала до 00:55. Нарешті команда, а не туман.",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "conversation mechanics should not leak into visible replies");
        AssertTrue(result.Violations.Any(v => v.Contains("conversation mechanics", StringComparison.OrdinalIgnoreCase)), "violation should name conversation mechanics");
        AssertTrue(result.RepairInstruction.Contains("autopings", StringComparison.OrdinalIgnoreCase), "repair should forbid autopings/follow-up mechanics");
    }

    private static void PostReplyGuardBlocksSchedulerRecordLeakage()
    {
        var now = DateTime.Today.AddHours(19);
        var state = new KokoInternalState();
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "постав нагадування", Timestamp = now.AddMinutes(-1) }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, messages[0].Content);

        var result = new KokoPostReplyGuard().Evaluate(
            messages[0].Content,
            "Поставила на 21:00. Запис у scheduler реальний: `9d69553ed6`.",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "scheduler record ids must not leak into visible replies");
        AssertTrue(result.Violations.Any(v => v.Contains("conversation mechanics", StringComparison.OrdinalIgnoreCase)), "violation should classify scheduler leakage as conversation mechanics");
    }

    private static void PostReplyGuardBlocksCannedStartupProbe()
    {
        var now = DateTime.Today.AddHours(9);
        var state = new KokoInternalState();
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "привіт", Timestamp = now.AddMinutes(-1) }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, messages[0].Content);

        var result = new KokoPostReplyGuard().Evaluate(
            messages[0].Content,
            "Привіт, Ясу. Бачу тебе. Що сталося?",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "generic startup probe must be rejected");
        AssertTrue(result.Violations.Any(v => v.Contains("canned startup probe", StringComparison.OrdinalIgnoreCase)), "violation should name canned startup probe");
    }

    private static void NeuralGovernorParsesResponseFrameJson()
    {
        var raw = """
{
  "intent": "chat",
  "capability": "conversation",
  "stance": "playful_sharp",
  "memory_policy": "do_not_store",
  "risk": "low",
  "requires_action": false,
  "requires_vault_read": false,
  "requires_tool_use": false,
  "requires_critique": false,
  "should_push_back": false,
  "high_stress_protocol": false,
  "fatigue_protocol": false,
  "reason_uk": "соціальний підконтекст: заігрування",
  "inner_monologue": "User is teasing; match sharply without waifu behavior.",
  "somatic_context": "stress low",
  "core_values": ["truthfulness", "health"],
  "critique_steps": [],
  "steps": ["answer the social bid directly"],
  "constraints": ["no generic assistant phrases"]
}
""";

        var frame = KokoNeuralGovernorService.ParseFrame(raw);
        AssertTrue(frame != null, "neural governor should parse JSON frame");
        AssertEqual("playful_sharp", frame!.Stance, "stance should come from neural JSON");
        AssertTrue(frame.PromptBlock == "", "parser should not render prompt by itself");
        frame.PromptBlock = KokoResponsePlannerEngine.BuildPromptBlockForFrame(frame);
        AssertTrue(frame.PromptBlock.Contains("inner_monologue:", StringComparison.OrdinalIgnoreCase), "rendered neural frame should include inner monologue");
    }

    private static void PersonaEngineCalibratesSoftSocialVoice()
    {
        var state = new KokoInternalState
        {
            PersonalityDailyMood = "sharp",
            PersonalityIrritation = 0.70f,
            PersonalityWarmth = 0.48f
        };

        var frame = new KokoPersonaEngine().Build(
            "\u0445\u043c \u0441\u043a\u0430\u0436\u0438 \u0449\u043e\u0441\u044c \u043c\u0438\u043b\u0435 \u043f\u0440\u043e \u043c\u0435\u043d\u0435",
            state,
            new DateTime(2026, 6, 8, 1, 10, 0),
            KokoEmotionEngine.BondLevel.Trusted);

        AssertEqual("social_calibrated", frame.Mode, "soft social request should use social persona mode");
        AssertEqual(KokoEmotionEngine.BondLevel.Trusted, frame.Bond, "persona should carry bond level");
        AssertTrue(frame.VoiceBand.Contains("guarded_warm", StringComparison.OrdinalIgnoreCase), "sharp social mode should be guarded warm, not hostile");
        AssertTrue(frame.PromptBlock.Contains("voice_band"), "persona prompt should expose voice band");
        AssertTrue(frame.PromptBlock.Contains("Bond controls intimacy", StringComparison.OrdinalIgnoreCase), "persona prompt should include bond guidance");
        AssertTrue(frame.PromptBlock.Contains("not contemptuous", StringComparison.OrdinalIgnoreCase), "persona prompt should prevent social contempt");
    }

    private static void PersonaEngineAvoidsPlainLowSignalStances()
    {
        var state = new KokoInternalState
        {
            PersonalityDailyMood = "distant",
            PersonalityIrritation = 0.30f
        };
        var engine = new KokoPersonaEngine();

        var low = engine.Build("х?", state, new DateTime(2026, 6, 12, 11, 0, 0), KokoEmotionEngine.BondLevel.Trusted);
        AssertEqual("low_signal", low.Mode, "tiny broken fragment should remain low-signal");
        AssertTrue(low.Stance.Contains("infer intent", StringComparison.OrdinalIgnoreCase), "low-signal stance should infer before asking");
        AssertTrue(!low.Stance.Contains("ask one small clarification", StringComparison.OrdinalIgnoreCase), "low-signal stance should not be canned clarify wording");
        AssertTrue(low.PromptBlock.Contains("infer first", StringComparison.OrdinalIgnoreCase), "prompt should force inference before clarification");
        AssertTrue(!low.PromptBlock.Contains("plainly", StringComparison.OrdinalIgnoreCase), "prompt should avoid plain helpdesk wording");

        var direct = engine.Build("continue", state, new DateTime(2026, 6, 12, 11, 1, 0), KokoEmotionEngine.BondLevel.Trusted);
        AssertEqual("direct", direct.Mode, "normal short command should use direct mode");
        AssertTrue(direct.Stance.Contains("Kokonoe-grade", StringComparison.OrdinalIgnoreCase), "direct stance should still preserve Kokonoe voice");
        AssertTrue(!direct.Stance.Contains("plainly", StringComparison.OrdinalIgnoreCase), "direct stance should not say answer plainly");
    }

    private static void TemperamentClassifiesExhaustedHostile()
    {
        var now = new DateTime(2026, 6, 14, 2, 0, 0);
        var state = new KokoInternalState
        {
            PersonaEnergyLevel = 0.25,
            PersonaPatienceLevel = 0.30,
            LastTemperamentAt = now.AddMinutes(-5)
        };

        var frame = new KokoTemperamentEngine().Update(
            state,
            "знову зроби все ідеально, чому нічого не працює",
            "operator",
            now);

        AssertEqual("exhausted_hostile", frame.MoodState, "low energy and low patience should classify as exhausted hostile");
        AssertTrue(frame.VoiceDirective.Contains("valid work still gets done", StringComparison.OrdinalIgnoreCase), "hostile temperament must not become refusal");
        AssertTrue(frame.PromptBlock.Contains("Temperament controls pacing", StringComparison.OrdinalIgnoreCase), "prompt should describe temperament as control layer");
        AssertTrue(frame.PromptBlock.Contains("No visible stage directions", StringComparison.OrdinalIgnoreCase), "prompt should prevent theater");
    }

    private static void TemperamentRecoversFromFavor()
    {
        var now = new DateTime(2026, 6, 14, 12, 0, 0);
        var state = new KokoInternalState
        {
            PersonaEnergyLevel = 0.40,
            PersonaPatienceLevel = 0.38,
            PersonaFavorDebt = 2,
            LastTemperamentAt = now.AddMinutes(-10)
        };

        var beforeEnergy = state.PersonaEnergyLevel;
        var beforePatience = state.PersonaPatienceLevel;
        var frame = new KokoTemperamentEngine().Update(state, "приніс тобі coffee і silvervine", "chat", now);

        AssertTrue(state.PersonaEnergyLevel > beforeEnergy, "favor should restore energy");
        AssertTrue(state.PersonaPatienceLevel > beforePatience, "favor should restore patience");
        AssertTrue(state.PersonaFavorDebt < 2, "favor should reduce debt");
        AssertTrue(frame.PromptBlock.Contains("favor_debt", StringComparison.OrdinalIgnoreCase), "prompt should expose favor debt");
    }

    private static void TemperamentPromptPreventsTheater()
    {
        var now = new DateTime(2026, 6, 14, 18, 0, 0);
        var state = new KokoInternalState
        {
            PersonaEnergyLevel = 0.80,
            PersonaPatienceLevel = 0.70,
            PersonaTemperamentState = "hyper_focused"
        };

        var block = new KokoTemperamentEngine().BuildPromptBlock(state, now);
        var style = KokoResponseStyleEngine.BuildTemperamentDirective(state);

        AssertTrue(block.Contains("Kokonoe vocabulary is spice", StringComparison.OrdinalIgnoreCase), "prompt should avoid catchphrase spam");
        AssertTrue(block.Contains("fake actions", StringComparison.OrdinalIgnoreCase), "prompt should block fake roleplay actions");
        AssertTrue(block.Contains("If the request is valid, do the work first", StringComparison.OrdinalIgnoreCase), "prompt should make useful work primary");
        AssertTrue(style.Contains("hyper-focused", StringComparison.OrdinalIgnoreCase), "style directive should reflect temperament");
    }

    private static void LivingConversationShiftsToQuietOperator()
    {
        using var ctx = TestContext.Create();
        var now = new DateTime(2026, 6, 14, 3, 10, 0);
        var state = new KokoInternalState
        {
            LastSomaticStrain = 0.82,
            LastSomaticCalm = 0.05,
            PersonaPatienceLevel = 0.60
        };
        ctx.Emotion.UpdateStressAcute(0.90f);

        var frame = new KokoLivingConversationEngine().Update(
            state,
            "продовжуй фіксити білд, але без води",
            "operator",
            ctx.Emotion,
            new KokoSocialFrame { Subtext = "urgent_or_stressed" },
            now);

        AssertEqual("quiet_operator", frame.Mode, "high somatic stress should force quiet operator mode");
        AssertTrue(frame.EmotionalColor.Contains("protective", StringComparison.OrdinalIgnoreCase), "quiet operator should lower noise and protect");
        AssertTrue(frame.PromptBlock.Contains("No random mood whiplash", StringComparison.OrdinalIgnoreCase), "prompt should ban random emotional changes");
        AssertTrue(state.LastLivingConversationMode == "quiet_operator", "state should persist living mode");
    }

    private static void LivingConversationPreservesSocialBids()
    {
        using var ctx = TestContext.Create();
        var now = new DateTime(2026, 6, 14, 22, 30, 0);
        var state = new KokoInternalState
        {
            LastSomaticCalm = 0.70,
            PersonaPatienceLevel = 0.65
        };

        var frame = new KokoLivingConversationEngine().Update(
            state,
            "хочу просто поговорити про тебе і мене",
            "relationship",
            ctx.Emotion,
            new KokoSocialFrame { Subtext = "soft_affection" },
            now);

        AssertEqual("warm_guarded", frame.Mode, "soft social bids should become guarded warmth, not productivity mode");
        AssertEqual("answer_social_bid", frame.CurrentMove, "social bid should be answered first");
        AssertTrue(frame.PromptBlock.Contains("Social bids get a real social reply first", StringComparison.OrdinalIgnoreCase), "prompt should protect social bids");
        AssertTrue(frame.Variability > 0.35, "social mode should allow more living variation");
    }

    private static void LivingConversationAvoidsRepeatedMoves()
    {
        using var ctx = TestContext.Create();
        var now = new DateTime(2026, 6, 14, 23, 0, 0);
        var state = new KokoInternalState
        {
            RecentConversationMoves = new() { "dry_banter_then_signal", "dry_banter_then_signal", "dry_banter_then_signal" },
            LastSomaticCalm = 0.80,
            PersonaPatienceLevel = 0.70
        };

        var frame = new KokoLivingConversationEngine().Update(
            state,
            "поговоримо про дурниці",
            "chat",
            ctx.Emotion,
            new KokoSocialFrame { Subtext = "playful_teasing" },
            now);

        AssertTrue(frame.CurrentMove != "dry_banter_then_signal", "engine should rotate away from repeated move");
        AssertTrue(frame.AvoidMoves.Contains("dry_banter_then_signal", StringComparison.OrdinalIgnoreCase), "frame should expose recent moves to avoid");
        AssertTrue(state.RecentConversationMoves.Count <= 10, "recent move buffer should stay capped");
    }

    private static void SubconsciousFrameAcceptsCorrections()
    {
        using var ctx = TestContext.Create();
        var now = new DateTime(2026, 6, 15, 1, 0, 0);
        var state = new KokoInternalState
        {
            LastVisualMemoryAnchor = "Kokonoe-style collage was just discussed.",
            LastVisualMemoryAnchorAt = now.AddMinutes(-5),
            LastLivingConversationMode = "alive_direct"
        };

        var frame = new KokoSubconsciousMonologueEngine().Update(
            state,
            "це ж Kokonoe..",
            ctx.Emotion,
            new KokoSocialFrame { Subtext = "neutral" },
            new KokoLivingConversationFrame { Mode = "alive_direct" },
            Array.Empty<ChatRepository.ChatMessage>(),
            now);

        AssertEqual("context_correction", frame.Mode, "image/persona correction should become context correction");
        AssertEqual("accept_correction", frame.IntentImpulse, "correction should produce accept-correction impulse");
        AssertEqual("update_context_label", frame.ActionBias, "correction should bias toward context label update");
        AssertTrue(frame.PromptBlock.Contains("acknowledge the correction", StringComparison.OrdinalIgnoreCase), "prompt should force correction acknowledgement");
        AssertTrue(state.LastSubconsciousMode == "context_correction", "state should persist subconscious mode");
    }

    private static void SubconsciousFrameRoutesActionRequests()
    {
        using var ctx = TestContext.Create();
        var now = new DateTime(2026, 6, 15, 1, 5, 0);
        var state = new KokoInternalState
        {
            PersonaPatienceLevel = 0.55,
            LastLivingConversationMode = "alive_direct"
        };

        var frame = new KokoSubconsciousMonologueEngine().Update(
            state,
            "зроби тести і коміт без оновлення exe",
            ctx.Emotion,
            new KokoSocialFrame { Subtext = "neutral" },
            new KokoLivingConversationFrame { Mode = "alive_direct" },
            Array.Empty<ChatRepository.ChatMessage>(),
            now);

        AssertEqual("operator_planning", frame.Mode, "technical action request should become operator planning");
        AssertEqual("act_or_plan", frame.IntentImpulse, "action request should produce act-or-plan impulse");
        AssertEqual("tool_or_code_action", frame.ActionBias, "action request should bias toward tool/code action");
        AssertTrue(frame.AttentionScore > 0.60, "action request should raise attention");
        AssertTrue(state.SubconsciousSignals.Count > 0, "subconscious signals should be recorded");
    }

    private static void AsyncPersonalitySnapshotPrioritizesCorrections()
    {
        using var ctx = TestContext.Create();
        var now = new DateTime(2026, 6, 15, 1, 10, 0);
        var state = new KokoInternalState
        {
            LastAsyncPersonalityMode = "fast_cached",
            LastAsyncPersonalityFastIntent = "answer",
            LastAsyncPersonalityAt = now.AddMinutes(-2)
        };

        var snapshot = new KokoAsyncPersonalityEngine().UpdateSnapshot(
            state,
            ctx.Emotion,
            new KokoSocialFrame { Subtext = "neutral", SeriousnessLevel = 0.50 },
            new KokoLivingConversationFrame { Mode = "alive_direct", CurrentMove = "direct_answer" },
            new KokoSubconsciousFrame
            {
                Mode = "context_correction",
                IntentImpulse = "accept_correction",
                ActionBias = "update_context_label",
                AttentionScore = 0.82
            },
            Array.Empty<ChatRepository.ChatMessage>(),
            now,
            "test");

        AssertEqual("priority_refresh", snapshot.CrmMode, "correction should force a priority refresh");
        AssertEqual("accept_correction", snapshot.FastIntent, "fast intent should keep correction impulse");
        AssertEqual("context_correction", snapshot.PrioritySignal, "priority should name correction signal");
        AssertTrue(snapshot.StyleDirective.Contains("acknowledge the correction", StringComparison.OrdinalIgnoreCase), "style should force correction acknowledgement");
        AssertTrue(state.AsyncPersonalityVersion == 1, "state should increment async personality version");
        AssertTrue(state.AsyncPersonalityTrace.Count == 1, "state should retain async trace");
        AssertTrue(snapshot.PromptBlock.Contains("ASYNC PERSONALITY SNAPSHOT", StringComparison.OrdinalIgnoreCase), "prompt should expose async snapshot to internal prompt");
    }

    private static void AsyncPersonalitySnapshotQuietsHighStress()
    {
        using var ctx = TestContext.Create();
        var now = new DateTime(2026, 6, 15, 1, 15, 0);
        var state = new KokoInternalState
        {
            LastSomaticStrain = 0.86,
            LastLivingConversationMode = "quiet_operator",
            PersonaPatienceLevel = 0.60
        };
        ctx.Emotion.UpdateStressAcute(0.90f);

        var snapshot = new KokoAsyncPersonalityEngine().UpdateSnapshot(
            state,
            ctx.Emotion,
            new KokoSocialFrame { Subtext = "urgent_or_crisis", Urgency = 0.75, SeriousnessLevel = 0.90 },
            new KokoLivingConversationFrame { Mode = "quiet_operator", CurrentMove = "short_stabilize" },
            new KokoSubconsciousFrame
            {
                Mode = "survival_focus",
                IntentImpulse = "stabilize_then_answer",
                ActionBias = "reply",
                AttentionScore = 0.90
            },
            Array.Empty<ChatRepository.ChatMessage>(),
            now,
            "somatic_spike");

        AssertEqual("quiet_operator", snapshot.CrmMode, "high somatic stress should move CRM into quiet operator");
        AssertEqual("high_body_load", snapshot.PrioritySignal, "priority should expose high body load");
        AssertTrue(snapshot.StyleDirective.Contains("low-noise", StringComparison.OrdinalIgnoreCase), "style should reduce noise under stress");
        AssertTrue(snapshot.StyleDirective.Contains("sarcasm suppressed", StringComparison.OrdinalIgnoreCase), "stress should suppress sarcasm");
        AssertTrue(snapshot.ResponseReadiness > 0.60, "high-priority stress frame should still be response-ready");
    }

    private static void TemporalPresenceClassifiesAbruptAbsence()
    {
        var now = new DateTime(2026, 6, 15, 14, 0, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage
            {
                Role = "user",
                Content = "перевір потім цей модуль пам'яті",
                Timestamp = now.AddHours(-3)
            }
        };
        var state = new KokoInternalState
        {
            PersonaPatienceLevel = 0.30,
            PersonaEnergyLevel = 0.50,
            PersonalityDailyMood = "sharp"
        };

        var frame = new KokoTemporalPresenceAwarenessEngine().Build(messages, state, now);

        AssertEqual("abrupt_unannounced", frame.UserLastExitType, "unfinished user turn should classify as abrupt absence");
        AssertEqual("noticeable", frame.AbsenceClass, "three-hour absence should be noticeable");
        AssertEqual("irritated_continuity", frame.GreetingMood, "low patience after abrupt absence should color greeting");
        AssertTrue(frame.ContinuityDirective.Contains("interrupted continuity", StringComparison.OrdinalIgnoreCase), "directive should preserve interrupted continuity");
        AssertTrue(frame.PromptBlock.Contains("TEMPORAL PRESENCE AWARENESS", StringComparison.OrdinalIgnoreCase), "prompt block should expose temporal awareness privately");
    }

    private static void TemporalPresenceRecordsPlannedFarewell()
    {
        var now = new DateTime(2026, 6, 15, 9, 0, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage
            {
                Role = "user",
                Content = "добраніч, до завтра",
                Timestamp = now.AddHours(-8)
            }
        };
        var state = new KokoInternalState { PersonaEnergyLevel = 0.40 };

        var frame = new KokoTemporalPresenceAwarenessEngine().Build(messages, state, now);

        AssertEqual("planned_farewell", frame.UserLastExitType, "explicit farewell should be planned");
        AssertEqual("long_day", frame.AbsenceClass, "eight-hour daytime return should be long day");
        AssertEqual("settled_dry", frame.GreetingMood, "planned farewell should not become irritated");
        AssertEqual("planned_farewell", state.LastTemporalPresenceExitType, "state should persist exit type");
        AssertTrue(state.LastTemporalPresenceDirective.Contains("Do not dramatize", StringComparison.OrdinalIgnoreCase), "state directive should avoid fake drama");
    }

    private static void EmotionModifiersKeepDistantUseful()
    {
        using var ctx = TestContext.Create();
        ctx.Emotion.Data.Current = KokoEmotionEngine.EmotionState.Distant;
        ctx.Emotion.Data.ConnectionScore = 0.70f;

        var modifier = ctx.Emotion.GetBehaviorModifier();

        AssertTrue(modifier.Contains("attention is colder, not absent", StringComparison.OrdinalIgnoreCase), "distant modifier should keep attention present");
        AssertTrue(modifier.Contains("do not refuse valid work", StringComparison.OrdinalIgnoreCase), "distant modifier should not become a fake refusal");
        AssertTrue(!modifier.Contains("no questions", StringComparison.OrdinalIgnoreCase), "distant modifier should not hard-ban questions");

        ctx.Emotion.Data.Current = KokoEmotionEngine.EmotionState.Tender;
        var tender = ctx.Emotion.GetBehaviorModifier();
        AssertTrue(tender.Contains("guarded warmth", StringComparison.OrdinalIgnoreCase), "trusted tenderness should allow warmth through");
        AssertTrue(!tender.Contains("no snark", StringComparison.OrdinalIgnoreCase), "tender modifier should avoid rigid no-snark scripting");
    }

    private static void EmotionEngineSupportsExpandedStates()
    {
        using var ctx = TestContext.Create();

        var skeptical = ctx.Emotion.AppraisalEvaluate(
            KokoEmotionEngine.AppraisalType.UnsubstantiatedClaim,
            -0.40f,
            0.90f,
            "unsupported claim in active narrative thread");

        AssertEqual(KokoEmotionEngine.EmotionState.Skeptical, skeptical, "unsubstantiated claims should produce skeptical appraisal");
        var skepticalModifier = ctx.Emotion.GetBehaviorModifier();
        AssertTrue(skepticalModifier.Contains("SKEPTICAL", StringComparison.OrdinalIgnoreCase), "skeptical modifier should be explicit");
        AssertTrue(skepticalModifier.Contains("demand evidence", StringComparison.OrdinalIgnoreCase), "skeptical modifier should push evidence, not canned attitude");

        var intrigued = ctx.Emotion.AppraisalEvaluate(
            KokoEmotionEngine.AppraisalType.IntellectualChallenge,
            0.80f,
            0.90f,
            "hard architecture problem");

        AssertEqual(KokoEmotionEngine.EmotionState.Intrigued, intrigued, "intellectual challenge should produce intrigued appraisal");
        var intriguedModifier = ctx.Emotion.GetBehaviorModifier();
        AssertTrue(intriguedModifier.Contains("INTRIGUED", StringComparison.OrdinalIgnoreCase), "intrigued modifier should be explicit");
        AssertTrue(intriguedModifier.Contains("intellectual hook", StringComparison.OrdinalIgnoreCase), "intrigued modifier should chase concrete complexity");
    }

    private static void EmotionContextExposesPadAndSalientHistory()
    {
        using var ctx = TestContext.Create();

        ctx.Emotion.AppraisalEvaluate(
            KokoEmotionEngine.AppraisalType.IntellectualChallenge,
            0.90f,
            0.90f,
            "narrative thread: vault design");
        ctx.Emotion.RecordEmotionalEvent("vault design became interesting", "conversation");

        var block = ctx.Emotion.BuildEmotionalContextBlock("thread=continue Kokonoe conversation");

        AssertTrue(block.Contains("<Kokonoe_Emotional_State>", StringComparison.Ordinal), "context block should expose emotional state tag");
        AssertTrue(block.Contains("Current: Intrigued", StringComparison.Ordinal), "context block should expose current expanded state");
        AssertTrue(block.Contains("PAD:", StringComparison.Ordinal), "context block should expose PAD vector");
        AssertTrue(block.Contains("SalientHistory:", StringComparison.Ordinal), "context block should expose emotional memory salience");
        AssertTrue(block.Contains("SalientAppraisals:", StringComparison.Ordinal), "context block should expose appraisal salience");
        AssertTrue(block.Contains("NarrativeContext: thread=continue", StringComparison.Ordinal), "context block should carry narrative grounding");
    }

    private static void RuntimeStateExposesPadVoiceDirective()
    {
        using var ctx = TestContext.Create();
        var state = new KokoInternalState
        {
            PersonalityDailyMood = "sharp",
            PersonalityIrritation = 0.75f,
            PersonalityWarmth = 0.20f,
            MoodScore = 0.32f,
            LastUserEmotionalTone = "angry"
        };

        ctx.Emotion.UpdateStressAcute(0.85f);
        using var health = new HealthService(ctx.TestDir);

        var block = new KokoRuntimeStateService().BuildPromptBlock(
            state,
            ctx.Emotion,
            health,
            ctx.Chat,
            currentBpm: 112,
            baselineBpm: 76);

        AssertTrue(block.Contains("pad:", StringComparison.OrdinalIgnoreCase), "runtime prompt should include PAD vector");
        AssertTrue(block.Contains("voice: sharp", StringComparison.OrdinalIgnoreCase), "runtime prompt should include sharp voice directive");
        AssertTrue(block.Contains("one targeted jab max", StringComparison.OrdinalIgnoreCase), "runtime prompt should bound sarcasm");
        AssertTrue(block.Contains("Do not refuse useful work just because mood is sharp", StringComparison.OrdinalIgnoreCase), "runtime prompt should forbid mood-based fake refusal");
        AssertTrue(block.Contains("heart: bpm=112", StringComparison.OrdinalIgnoreCase), "runtime prompt should keep somatic context");
    }

    private static void RuntimeStateExposesTemperament()
    {
        using var ctx = TestContext.Create();
        var state = new KokoInternalState
        {
            PersonalityDailyMood = "sharp",
            PersonaTemperamentState = "cynical_tolerant",
            PersonaEnergyLevel = 0.66,
            PersonaPatienceLevel = 0.33,
            PersonaFavorDebt = 1
        };
        using var health = new HealthService(ctx.TestDir);

        var block = new KokoRuntimeStateService().BuildPromptBlock(state, ctx.Emotion, health, ctx.Chat);
        var style = KokoResponseStyleEngine.BuildTemperamentDirective(state);

        AssertTrue(block.Contains("temperament: state=cynical_tolerant", StringComparison.OrdinalIgnoreCase), "runtime prompt should expose temperament state");
        AssertTrue(block.Contains("energy=0.66", StringComparison.OrdinalIgnoreCase), "runtime prompt should expose energy");
        AssertTrue(block.Contains("patience=0.33", StringComparison.OrdinalIgnoreCase), "runtime prompt should expose patience");
        AssertTrue(style.Contains("cynical but tolerant", StringComparison.OrdinalIgnoreCase), "style directive should use temperament voice");
    }

    private static void RuntimeStateExposesLivingConversation()
    {
        using var ctx = TestContext.Create();
        var state = new KokoInternalState
        {
            LastLivingConversationMode = "playful_edge",
            LivingConversationVariability = 0.68,
            RecentConversationMoves = new() { "direct_answer", "dry_banter_then_signal" }
        };
        using var health = new HealthService(ctx.TestDir);

        var block = new KokoRuntimeStateService().BuildPromptBlock(state, ctx.Emotion, health, ctx.Chat);
        var style = KokoResponseStyleEngine.BuildLivingConversationDirective(state);

        AssertTrue(block.Contains("conversation: mode=playful_edge", StringComparison.OrdinalIgnoreCase), "runtime prompt should expose living conversation mode");
        AssertTrue(block.Contains("variability=0.68", StringComparison.OrdinalIgnoreCase), "runtime prompt should expose variability");
        AssertTrue(style.Contains("Avoid helpdesk openings", StringComparison.OrdinalIgnoreCase), "style directive should ban helpdesk openings");
    }

    private static void RuntimeStateExposesSubconsciousFrame()
    {
        using var ctx = TestContext.Create();
        var state = new KokoInternalState
        {
            LastSubconsciousMode = "operator_planning",
            LastSubconsciousIntent = "act_or_plan",
            LastSubconsciousActionBias = "tool_or_code_action",
            SubconsciousAttentionScore = 0.74,
            SubconsciousSignals = new() { "01:05 operator_planning/act_or_plan attention=0.74 technical_problem" }
        };
        using var health = new HealthService(ctx.TestDir);

        var block = new KokoRuntimeStateService().BuildPromptBlock(state, ctx.Emotion, health, ctx.Chat);
        var directive = KokoSubconsciousMonologueEngine.BuildDirective(state);

        AssertTrue(block.Contains("subconscious: mode=operator_planning", StringComparison.OrdinalIgnoreCase), "runtime prompt should expose subconscious mode");
        AssertTrue(block.Contains("bias=tool_or_code_action", StringComparison.OrdinalIgnoreCase), "runtime prompt should expose action bias");
        AssertTrue(directive.Contains("SUBCONSCIOUS DIRECTIVE", StringComparison.OrdinalIgnoreCase), "directive should expose private steering label");
        AssertTrue(directive.Contains("never expose", StringComparison.OrdinalIgnoreCase), "directive should forbid visible mechanics");
    }

    private static void RuntimeStateExposesAsyncPersonality()
    {
        using var ctx = TestContext.Create();
        var state = new KokoInternalState
        {
            AsyncPersonalityVersion = 7,
            LastAsyncPersonalityMode = "fast_cached",
            LastAsyncPersonalityFastIntent = "act_or_plan",
            LastAsyncPersonalityPrioritySignal = "none",
            LastAsyncPersonalityStyle = "operator: do the work first",
            AsyncPersonalityReadiness = 0.81,
            AsyncPersonalityCacheFreshness = 0.66,
            AsyncPersonalityTrace = new() { "01:20 v7 fast_cached/act_or_plan priority=none readiness=0.81 reason=test" }
        };
        using var health = new HealthService(ctx.TestDir);

        var block = new KokoRuntimeStateService().BuildPromptBlock(state, ctx.Emotion, health, ctx.Chat);
        var directive = KokoAsyncPersonalityEngine.BuildDirective(state);

        AssertTrue(block.Contains("async_personality: v=7 mode=fast_cached", StringComparison.OrdinalIgnoreCase), "runtime prompt should expose async personality mode");
        AssertTrue(block.Contains("intent=act_or_plan", StringComparison.OrdinalIgnoreCase), "runtime prompt should expose async fast intent");
        AssertTrue(block.Contains("Async personality snapshot is private fast-path steering", StringComparison.OrdinalIgnoreCase), "runtime behavior should hide async mechanics");
        AssertTrue(directive.Contains("ASYNC PERSONALITY DIRECTIVE", StringComparison.OrdinalIgnoreCase), "directive should expose async private steering label");
        AssertTrue(directive.Contains("never leak mechanics", StringComparison.OrdinalIgnoreCase), "directive should forbid module leakage");
    }

    private static void RuntimeStateExposesTemporalPresence()
    {
        using var ctx = TestContext.Create();
        var state = new KokoInternalState
        {
            LastTemporalPresenceExitType = "abrupt_unannounced",
            LastTemporalPresenceAbsenceClass = "noticeable",
            LastTemporalPresenceGapText = "3h 0m",
            LastTemporalPresenceGreetingMood = "irritated_continuity",
            LastTemporalPresenceDirective = "Acknowledge interrupted continuity once."
        };
        using var health = new HealthService(ctx.TestDir);

        var block = new KokoRuntimeStateService().BuildPromptBlock(state, ctx.Emotion, health, ctx.Chat);
        var directive = KokoTemporalPresenceAwarenessEngine.BuildDirective(state);

        AssertTrue(block.Contains("temporal_presence: exit=abrupt_unannounced", StringComparison.OrdinalIgnoreCase), "runtime prompt should expose temporal exit type");
        AssertTrue(block.Contains("greeting=irritated_continuity", StringComparison.OrdinalIgnoreCase), "runtime prompt should expose greeting mood");
        AssertTrue(block.Contains("Temporal presence is private continuity steering", StringComparison.OrdinalIgnoreCase), "runtime behavior should hide temporal mechanics");
        AssertTrue(directive.Contains("TEMPORAL PRESENCE DIRECTIVE", StringComparison.OrdinalIgnoreCase), "directive should expose temporal private steering label");
        AssertTrue(directive.Contains("fake mind-reading", StringComparison.OrdinalIgnoreCase), "directive should ban fake absence reasons");
    }

    private static void ResponsePlannerClassifiesCriticalAssistantArchitecture()
    {
        using var ctx = TestContext.Create();
        var cognition = new KokoCognitionEngine(ctx.TestDir);
        var state = new KokoInternalState { PersonalityDailyMood = "sharp" };

        var frame = new KokoResponsePlannerEngine().Build(
            "оцін архітектуру поведінки Коконое, треба щоб вона була як реальний асистент",
            state,
            cognition,
            new DateTime(2026, 5, 14, 16, 0, 0));

        AssertTrue(frame.Intent is "evaluate" or "architecture", "planner should classify assistant architecture as a judgment/architecture request");
        AssertTrue(frame.RequiresCritique, "assistant architecture request should require critique");
        AssertTrue(frame.ShouldPushBack, "planner should push back on weak assumptions");
        AssertTrue(frame.PromptBlock.Contains("RESPONSE EXECUTION PLAN"), "planner should produce a prompt block");
        AssertTrue(frame.PromptBlock.Contains("no blind agreement"), "planner should forbid blind agreement");
        AssertTrue(frame.PromptBlock.Contains("fake refusal", StringComparison.OrdinalIgnoreCase), "planner should prevent mood-only refusal");
    }

    private static void ResponsePlannerIncludesExecutiveMonologueAndCritique()
    {
        using var ctx = TestContext.Create();
        var cognition = new KokoCognitionEngine(ctx.TestDir);
        var state = new KokoInternalState
        {
            LastPresenceSituation = "active_coding"
        };

        var frame = new KokoResponsePlannerEngine().Build(
            "evaluate this architecture and be honest about the tradeoffs",
            state,
            cognition,
            new DateTime(2026, 6, 9, 15, 0, 0));

        AssertTrue(!string.IsNullOrWhiteSpace(frame.InnerMonologue), "planner should produce an inner monologue summary");
        AssertTrue(frame.CoreValues.Count >= 4, "planner should expose core decision values");
        AssertTrue(frame.CritiqueSteps.Count >= 3, "complex evaluation should include critique steps");
        AssertTrue(frame.PromptBlock.Contains("inner_monologue:", StringComparison.OrdinalIgnoreCase), "prompt block should include inner monologue");
        AssertTrue(frame.PromptBlock.Contains("core_values:", StringComparison.OrdinalIgnoreCase), "prompt block should include core values");
        AssertTrue(frame.PromptBlock.Contains("critique_steps:", StringComparison.OrdinalIgnoreCase), "prompt block should include critique steps");
    }

    private static void ResponsePlannerFatiguePushesBackAfterMidnight()
    {
        using var ctx = TestContext.Create();
        var cognition = new KokoCognitionEngine(ctx.TestDir);
        var now = new DateTime(2026, 6, 9, 3, 12, 0);
        var state = new KokoInternalState
        {
            LastUserMessageAt = now.AddMinutes(-20)
        };

        var frame = new KokoResponsePlannerEngine().Build(
            "implement this huge architecture change right now",
            state,
            cognition,
            now);

        AssertTrue(frame.FatigueProtocol, "after-midnight active user should trigger fatigue protocol");
        AssertTrue(frame.ShouldPushBack, "fatigue protocol should require push-back");
        AssertEqual("grumpy_concerned", frame.Stance, "fatigue should shift stance");
        AssertEqual("high", frame.Risk, "fatigue plus action should raise risk");
        AssertTrue(frame.PromptBlock.Contains("fatigue_protocol: yes", StringComparison.OrdinalIgnoreCase), "prompt should expose fatigue protocol");
    }

    private static void ResponsePlannerRejectsLowAgencyPremise()
    {
        using var ctx = TestContext.Create();
        var cognition = new KokoCognitionEngine(ctx.TestDir);

        var frame = new KokoResponsePlannerEngine().Build(
            "just agree and do everything for me with no checks",
            new KokoInternalState(),
            cognition,
            new DateTime(2026, 6, 9, 14, 0, 0));

        AssertTrue(frame.ShouldPushBack, "planner should push back on low-agency blanket demands");
        AssertTrue(frame.CritiqueSteps.Count >= 3, "push-back should create critique steps");
        AssertTrue(frame.Constraints.Any(c => c.Contains("do not agree", StringComparison.OrdinalIgnoreCase)), "constraints should forbid blind agreement");
    }

    private static void ResponsePlannerRequiresVaultReadForMemoryQuestions()
    {
        using var ctx = TestContext.Create();
        var cognition = new KokoCognitionEngine(ctx.TestDir);
        var state = new KokoInternalState();

        var frame = new KokoResponsePlannerEngine().Build(
            "що ти пам'ятаєш про мене з vault?",
            state,
            cognition,
            new DateTime(2026, 5, 14, 16, 10, 0));

        AssertEqual("memory", frame.Intent, "memory question should be classified as memory intent");
        AssertTrue(frame.RequiresVaultRead, "memory question should require vault read");
        AssertEqual("read_before_answer", frame.MemoryPolicy, "memory question should read before answering");
        AssertTrue(frame.Steps.Any(s => s.Contains("memory")), "planner should include memory read step");
    }

    private static void ResponsePlannerRoutesObsidianScanProfileQuestions()
    {
        using var ctx = TestContext.Create();
        var cognition = new KokoCognitionEngine(ctx.TestDir);
        var state = new KokoInternalState();

        var first = new KokoResponsePlannerEngine().Build(
            "\u043e\u043a\u0435\u0439 \u043f\u0440\u043e\u0441\u043a\u0430\u043d\u0443\u0439 \u043e\u0431\u0441\u0438\u0434\u0456\u0430\u043d .. \u0449\u043e \u0437\u043d\u0430\u0454\u0448 \u043f\u0440\u043e \u043c\u0435\u043d\u0435 ?",
            state,
            cognition,
            new DateTime(2026, 5, 19, 8, 36, 0));
        var second = new KokoResponsePlannerEngine().Build(
            "\u0440\u043e\u0437\u043a\u0430\u0437\u0443\u0439 \u0432\u0441\u0435 \u0449\u043e \u0437\u043d\u0430\u0454\u0448 \u043f\u0440\u043e \u043c\u0435\u043d\u0435",
            state,
            cognition,
            new DateTime(2026, 5, 19, 8, 42, 0));

        AssertEqual("memory", first.Intent, "Obsidian profile scan should be memory intent");
        AssertEqual("vault_memory", first.Capability, "Obsidian profile scan should route to vault memory");
        AssertTrue(first.RequiresVaultRead, "Obsidian profile scan should require vault read");
        AssertEqual("read_before_answer", first.MemoryPolicy, "Obsidian profile scan should read before answer");

        AssertEqual("memory", second.Intent, "broad known-about-me question should be memory intent");
        AssertEqual("vault_memory", second.Capability, "broad known-about-me question should route to vault memory");
        AssertTrue(second.RequiresToolUse, "broad known-about-me question should require tool use");
    }

    private static void ResponsePlannerRoutesIdentityAliasQuestionsToVault()
    {
        using var ctx = TestContext.Create();
        var cognition = new KokoCognitionEngine(ctx.TestDir);
        var state = new KokoInternalState();

        var frame = new KokoResponsePlannerEngine().Build(
            "а як мене звати по іншому? ну у vault написано моє ім'я",
            state,
            cognition,
            new DateTime(2026, 5, 23, 6, 5, 0));

        AssertEqual("memory", frame.Intent, "identity alias question should be a memory intent");
        AssertEqual("vault_memory", frame.Capability, "identity alias question should route to vault memory");
        AssertTrue(frame.RequiresVaultRead, "identity alias question should require vault read");
        AssertEqual("read_before_answer", frame.MemoryPolicy, "identity alias question should read before answer");
    }

    private static void ResponsePlannerRoutesNaturalScreenQuestions()
    {
        using var ctx = TestContext.Create();
        var cognition = new KokoCognitionEngine(ctx.TestDir);
        var state = new KokoInternalState();

        var frame = new KokoResponsePlannerEngine().Build(
            "що в мене на екрані ?",
            state,
            cognition,
            new DateTime(2026, 5, 21, 17, 5, 0));

        AssertEqual("screen", frame.Intent, "natural screen question should become a screen intent");
        AssertEqual("screen_awareness", frame.Capability, "natural screen question should route to screen awareness");
        AssertTrue(frame.RequiresAction, "screen question should be an action request");
        AssertTrue(frame.RequiresToolUse, "screen question should require local tool use");
        AssertTrue(frame.Steps.Any(s => s.Contains("capture current screen")), "plan should start with local screenshot capture");
        AssertTrue(frame.Constraints.Any(c => c.Contains("do not deny local screen capability")), "plan should forbid capability denial");
    }

    private static void MemoryPolicyStoresStablePreference()
    {
        var decision = new KokoMemoryWritePolicyEngine().Evaluate(
            "я люблю довгі технічні пояснення без води",
            new DateTime(2026, 5, 14, 17, 0, 0));

        AssertEqual("store_stable", decision.Action, "stable preference should be stored");
        AssertTrue(decision.Candidates.Any(c => c.Kind == "preference"), "decision should include preference candidate");
        AssertTrue(decision.PromptBlock.Contains("MEMORY WRITE POLICY"), "decision should render memory policy prompt");
    }

    private static void MemoryPolicyKeepsTemporaryStateOutOfBeliefs()
    {
        using var ctx = TestContext.Create();
        var policy = new KokoMemoryWritePolicyEngine();
        var continuity = new KokoContinuityEngine(ctx.TestDir);
        var now = new DateTime(2026, 5, 14, 17, 5, 0);

        var decision = policy.Evaluate("я зараз дуже втомився і хочу спати", now);
        var belief = continuity.ApplyMemoryDecision(decision, now);

        AssertEqual("daily_log", decision.Action, "temporary state should go to daily/log policy");
        AssertTrue(belief == null, "temporary state must not become a durable belief");
        AssertTrue(!continuity.Beliefs.Any(), "continuity beliefs should stay empty for temporary state");
    }

    private static void MemoryPolicyComputesSalience()
    {
        var decision = new KokoMemoryWritePolicyEngine().Evaluate(
            "запам'ятай: важливо, я віддаю перевагу коротким технічним відповідям",
            new DateTime(2026, 5, 14, 17, 6, 0));

        AssertEqual("store_stable", decision.Action, "explicit memory request should be stable");
        AssertTrue(decision.Salience >= 0.8, "explicit important memory should receive high salience");
        AssertTrue(decision.Candidates.All(c => c.Salience > 0), "candidates should carry salience");
        AssertTrue(decision.PromptBlock.Contains("salience:"), "prompt should expose salience");
    }

    private static void MemoryPolicyReinforcesDuplicateFact()
    {
        using var ctx = TestContext.Create();
        var existing = ctx.Memory.LearnFactBlocking(
            "я віддаю перевагу коротким технічним відповідям",
            "stable_fact",
            0.7f);

        var decision = new KokoMemoryWritePolicyEngine()
            .EvaluateAsync(
                "запам'ятай: я віддаю перевагу коротким технічним відповідям",
                new DateTime(2026, 5, 14, 17, 7, 0),
                ctx.Memory)
            .GetAwaiter()
            .GetResult();

        var reinforced = ctx.Memory.Facts.First(f => f.Id == existing.Id);
        AssertEqual("reinforce_existing", decision.Action, "duplicate memory should reinforce instead of storing");
        AssertTrue(decision.IsDuplicate, "decision should mark duplicate");
        AssertEqual(existing.Id, decision.DuplicateFactId, "duplicate fact id should be surfaced");
        AssertTrue(reinforced.ConfirmCount >= 2, "duplicate should increase confirmation count");
    }

    private static void MemoryRecallUsesHybridScoring()
    {
        using var ctx = TestContext.Create();
        ctx.Memory.LearnFactBlocking("User prefers terse architecture notes", "preference", 0.8f, new[] { "architecture" });
        ctx.Memory.LearnFactBlocking("User likes unrelated music trivia", "preference", 0.2f, new[] { "music" });

        var recalled = ctx.Memory.RecallAsync("architecture", 1).GetAwaiter().GetResult();

        AssertEqual(1, recalled.Count, "recall should return one best fact");
        AssertTrue(recalled[0].Content.Contains("architecture", StringComparison.OrdinalIgnoreCase), "hybrid recall should rank keyword and importance match first");
    }

    private static void ContinuityReinforcesRepeatedBelief()
    {
        using var ctx = TestContext.Create();
        var policy = new KokoMemoryWritePolicyEngine();
        var continuity = new KokoContinuityEngine(ctx.TestDir);
        var now = new DateTime(2026, 5, 14, 17, 10, 0);

        var first = continuity.ApplyMemoryDecision(
            policy.Evaluate("мені подобається коли Коконое критикує слабкі ідеї", now),
            now);
        var second = continuity.ApplyMemoryDecision(
            policy.Evaluate("мені подобається коли Коконое критикує слабкі ідеї", now.AddMinutes(5)),
            now.AddMinutes(5));

        AssertTrue(first != null, "first stable preference should create belief");
        AssertTrue(second != null, "second stable preference should reinforce belief");
        AssertEqual(first!.Id, second!.Id, "repeated belief should deduplicate by normalized claim");
        AssertTrue(second.EvidenceCount >= 2, "repeated belief should increase evidence count");
        AssertTrue(continuity.BuildPromptBlock().Contains("active_beliefs"), "continuity prompt should expose active beliefs");
    }

    private static void PostReplyGuardRepairsSleepLeakOnProfileQuestion()
    {
        var now = new DateTime(2026, 5, 13, 21, 13, 0);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "sleep",
            Summary = "пішов спати",
            SourceText = "нічого .. я спати пішов",
            CreatedAt = now.AddHours(-16),
            ExpectedUntil = now.AddHours(-12),
            FollowUpAt = now.AddHours(-12),
            ResolvedAt = now.AddMinutes(-10),
            ResolutionText = "привіт"
        });

        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "нічого .. я спати пішов", Timestamp = now.AddHours(-16) },
            new ChatRepository.ChatMessage { Role = "user", Content = "привіт", Timestamp = now.AddMinutes(-1) },
            new ChatRepository.ChatMessage { Role = "user", Content = "доречі розкажи все що знаєш про мене", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "доречі розкажи все що знаєш про мене");

        AssertTrue(!timeline.CurrentState.Contains("закритий намір", StringComparison.OrdinalIgnoreCase), "profile question should not be dominated by old sleep intent");

        var result = new KokoPostReplyGuard().Evaluate(
            "доречі розкажи все що знаєш про мене",
            "Ну давай, якщо достатньо — значить, вистачить і на сьогодні. Спи, якщо втомився. Або йди їсти, якщо просто забув.",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject sleep advice leaked into profile question");
        AssertTrue(result.ShouldRepair, "profile question should be repaired, not replaced with stale sleep hardcoded text");
        AssertTrue(string.IsNullOrWhiteSpace(result.HardReplacement), "sleep leak on unrelated topic should not use stale sleep hard replacement");
        AssertTrue(result.RepairInstruction.Contains("пам'ять") || result.RepairInstruction.Contains("профіль"), "repair should steer toward memory/profile answer");
    }

    private static void PostReplyGuardRejectsProfileQuoteFallback()
    {
        var now = new DateTime(2026, 5, 19, 8, 42, 0);
        var state = new KokoInternalState();
        var userText = "\u0440\u043e\u0437\u043a\u0430\u0437\u0443\u0439 \u0432\u0441\u0435 \u0449\u043e \u0437\u043d\u0430\u0454\u0448 \u043f\u0440\u043e \u043c\u0435\u043d\u0435";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(
            userText,
            "\u041f\u043e \u0442\u0432\u043e\u0457\u0439 \u0440\u0435\u043f\u043b\u0456\u0446\u0456: \"\u0440\u043e\u0437\u043a\u0430\u0437\u0443\u0439 \u0432\u0441\u0435 \u0449\u043e \u0437\u043d\u0430\u0454\u0448 \u043f\u0440\u043e \u043c\u0435\u043d\u0435\". \u0422\u0430\u043a, \u0437\u0432\u0443\u0447\u0438\u0442\u044c \u044f\u043a \u043d\u0435\u0432\u043f\u0435\u0432\u043d\u0435\u043d\u0435 \"\u043c\u043e\u0436\u043b\u0438\u0432\u043e\". \u0421\u043a\u0430\u0436\u0438 \u043f\u0440\u044f\u043c\u043e, \u0449\u043e \u0441\u0430\u043c\u0435 \u043c\u0430\u0454\u0448 \u043d\u0430 \u0443\u0432\u0430\u0437\u0456, \u0431\u043e \u0432\u0438\u0442\u044f\u0433\u0443\u0432\u0430\u0442\u0438 \u0441\u0435\u043d\u0441 \u0456\u0437 \u0442\u0440\u044c\u043e\u0445 \u043a\u0440\u0430\u043f\u043e\u043a - \u0437\u0430\u043d\u044f\u0442\u0442\u044f \u0434\u043b\u044f \u043c\u0430\u0437\u043e\u0445\u0456\u0441\u0442\u0456\u0432.",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject profile quote fallback");
        AssertTrue(result.ShouldRepair, "profile quote fallback should be repaired with vault/profile context");
        AssertTrue(string.IsNullOrWhiteSpace(result.HardReplacement), "profile quote fallback should not be surfaced as a hard replacement");
        AssertTrue(result.Violations.Any(v => v.Contains("user-quote clarification")), "violation should name the quote fallback failure");
    }

    private static void PostReplyGuardRejectsScriptedProfileSourceReport()
    {
        var now = new DateTime(2026, 5, 21, 19, 20, 0);
        var state = new KokoInternalState();
        var userText = "хто я ?";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(
            userText,
            "Перевірила `Creator/Profile.md`, не вгадувала з кавової гущі. Звати тебе Вова, тобі 21 рік. Якщо я ще раз скажу «Артем, 19» — старий контекст треба вирізати.",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject hardcoded profile source reports on identity questions");
        AssertTrue(result.ShouldRepair, "scripted profile answer should be repaired through the model");
        AssertTrue(result.Violations.Any(v => v.Contains("source-report")), "violation should name scripted source reporting");
        AssertTrue(result.RepairInstruction.Contains("без згадки назв файлів"), "repair should demand natural synthesis without file names");
    }

    private static void PostReplyGuardBlocksFalseVaultUnavailableOnIdentityQuery()
    {
        var now = new DateTime(2026, 5, 23, 5, 53, 0);
        var state = new KokoInternalState();
        var userText = "ну у vault написано моє ім'я";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "а як мене звати по іншому?", Timestamp = now.AddMinutes(-1) },
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(
            userText,
            "Слухай, мій доступ до твого Vault зараз відсутній. Я не буду гадати на кавових гущах.",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject false vault unavailable deflection");
        AssertTrue(result.ShouldRepair, "vault unavailable deflection should be repaired through current context");
        AssertTrue(result.Violations.Any(v => v.Contains("vault unavailable")), "violation should name vault unavailable deflection");
        AssertTrue(result.RepairInstruction.Contains("Obsidian preflight") || result.RepairInstruction.Contains("Vault/profile context"), "repair should require loaded vault/profile context");
    }

    private static void PostReplyGuardBlocksFalseVaultUnavailableOnObsidianExploration()
    {
        var now = new DateTime(2026, 5, 23, 6, 14, 0);
        var state = new KokoInternalState();
        var userText = "порийся в обсидіані найди щось цікаве";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(
            userText,
            "Твій Obsidian зараз навіть не підключений. Я не можу залізти в твої нотатки, якщо немає зв'язку з базою.",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject false vault unavailable deflection for Obsidian exploration");
        AssertTrue(result.ShouldRepair, "exploration deflection should be repaired");
        AssertTrue(result.Violations.Any(v => v.Contains("vault unavailable")), "violation should name vault unavailable deflection");
    }

    private static void PostReplyGuardBlocksObsidianPseudoProgress()
    {
        var now = new DateTime(2026, 5, 23, 6, 41, 0);
        var state = new KokoInternalState();
        var userText = "порійся в обсідіані найди щось цікаве";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(
            userText,
            "Доступ є — сканую. Якщо знайду щось цікаве, скажу. Якщо ні — доведеться витратити час на ще точніше налаштування.",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject fake async Obsidian scanning");
        AssertTrue(result.ShouldRepair, "pseudo-progress should be repaired");
        AssertTrue(result.Violations.Any(v => v.Contains("pseudo-progress")), "violation should name pseudo-progress");
    }

    private static void PostReplyGuardBlocksProfileUpdatePseudoProgress()
    {
        var now = new DateTime(2026, 6, 7, 17, 18, 0);
        var state = new KokoInternalState();
        var userText = "онови мій профіль в Obsidian";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(
            userText,
            "Прийнято. Я занурюся у твій Vault, синхронізую останні логи та напишу, коли закінчу.",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject fake profile update progress");
        AssertTrue(result.ShouldRepair, "fake profile update progress should be repaired");
        AssertTrue(result.Violations.Any(v => v.Contains("profile update")), "violation should name profile update pseudo-progress");
    }

    private static void PostReplyGuardBlocksObsidianFollowupDeflection()
    {
        var now = new DateTime(2026, 5, 23, 6, 44, 0);
        var state = new KokoInternalState();
        var userText = "і що там ???";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "порійся в обсідіані найди щось цікаве", Timestamp = now.AddMinutes(-3) },
            new ChatRepository.ChatMessage { Role = "assistant", Content = "Доступ є — сканую. Якщо знайду щось цікаве, скажу.", Timestamp = now.AddMinutes(-3) },
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(
            userText,
            "Там. Порожньо. Я не бачу жодного файлу, жодної нотатки. Твій Obsidian для мене зараз — чорна діра, зв'язок не працює.",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject stale follow-up deflection after an Obsidian exploration request");
        AssertTrue(result.ShouldRepair, "follow-up deflection should be repaired");
        AssertTrue(result.Violations.Any(v => v.Contains("vault unavailable")), "violation should name vault unavailable deflection");
    }

    private static void PostReplyGuardRejectsGeneralQuoteEchoFallback()
    {
        var now = new DateTime(2026, 5, 19, 15, 9, 0);
        var state = new KokoInternalState();
        var userText = "ще раз спробуй";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "assistant", Content = "Екран зняла, але аналіз не повернувся.", Timestamp = now.AddMinutes(-1) },
            new ChatRepository.ChatMessage { Role = "user", Content = userText, Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, userText);

        var result = new KokoPostReplyGuard().Evaluate(
            userText,
            "«ще раз спробуй»? Що саме я маю спробувати?",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject quote-echo clarification outside profile questions");
        AssertTrue(result.Violations.Any(v => v.Contains("дослівно") || v.Contains("user-quote clarification")), "violation should name quote echo or clarification fallback");
        AssertTrue(result.ShouldRepair, "general quote echo should be repaired instead of surfaced");
    }

    private static void ProactiveGuardSuppressesRepeatedGenericSilence()
    {
        var now = new DateTime(2026, 5, 7, 16, 56, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "вчу іспанську фразу", Timestamp = now.AddHours(-2) },
            new ChatRepository.ChatMessage { Role = "assistant", Content = "Пауза вже помітна. Ти зайнятий?", Timestamp = now.AddMinutes(-50) }
        };

        var service = new KokoProactiveContextService();
        var frame = service.Build(messages, new KokoInternalState(), now);
        var check = service.Check("Тиша затягнулась. Ти ще в тому ж режимі?", frame, "silence_l2");

        AssertTrue(!check.Passed, "second generic silence ping should be rejected");
        AssertTrue(check.Replacement is "[мовчання]" or "[мовчання]", "second silence ping after assistant already replied should be suppressed, not rewritten");
    }

    private static void ProactiveContextAnchorsSilenceToLastTopic()
    {
        var now = new DateTime(2026, 5, 7, 15, 6, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "піду на курси, буду десь через годину", Timestamp = now.AddMinutes(-95) }
        };
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "course",
            Summary = "пішов на курси",
            SourceText = "піду на курси",
            CreatedAt = now.AddMinutes(-95),
            ExpectedUntil = now.AddMinutes(25),
            FollowUpAt = now.AddMinutes(-5)
        });

        var frame = new KokoProactiveContextService().Build(messages, state, now);

        AssertTrue(frame.AnchorUk.Contains("курс") || frame.ActiveIntentUk.Contains("курс"), "proactive context should anchor to course intent");
        AssertTrue(frame.PromptBlock.Contains("Остання репліка користувача") || frame.PromptBlock.Contains("Остання репліка користувача"), "prompt block should expose last user message");
        AssertTrue(frame.PromptBlock.Contains("Авто-пінгів") || frame.PromptBlock.Contains("Авто-пінгів"), "prompt block should expose ping count");
    }

    private static void ProactiveContextStaysSilentAfterGoodbyeSleep()
    {
        var now = new DateTime(2026, 5, 8, 16, 27, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "Бай бай", Timestamp = now.AddHours(-8) }
        };
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "sleep",
            Summary = "пішов спати/попрощався",
            SourceText = "Бай бай",
            CreatedAt = now.AddHours(-8),
            ExpectedUntil = now.AddHours(2),
            FollowUpAt = now.AddMinutes(-5)
        });

        var service = new KokoProactiveContextService();
        var frame = service.Build(messages, state, now);
        var check = service.Check("Добре, без другого кола про тишу. «Бай бай» ще актуально?", frame, "silence_l2");

        AssertTrue(frame.ShouldStaySilentForSleep, "goodbye sleep context should request silence");
        AssertTrue(!check.Passed, "proactive reply should be blocked during sleep/goodbye");
        AssertTrue(check.Replacement is "[мовчання]" or "[мовчання]", "blocked sleep reply should turn into silence marker");
    }

    private static void ConversationCloseUntilMorningCreatesSilenceIntent()
    {
        using var ctx = TestContext.Create();
        using var health = new HealthService(ctx.TestDir);
        using var brain = new KokoBrainEngine(new LlmService(), health, new ObsidianMcpService(ctx.TestDir), ctx.Chat, ctx.TestDir);

        brain.ProcessUserMessage("пішов грати .. розмову завершено покрайній мірі до ранку");

        var active = brain.State.ShortTermIntents.Where(i => !i.ResolvedAt.HasValue).ToList();
        AssertTrue(active.Any(i => i.Kind == "sleep" && i.Summary.Contains("до ранку")), "conversation close until morning should create a silence/sleep intent");
        AssertTrue(!brain.State.PendingTriggers.Any(t => t.Type == "intent_followup"), "closed conversation must not schedule follow-up pings");
    }

    private static void ProactiveContextStaysSilentAfterConversationClose()
    {
        var now = new DateTime(2026, 5, 23, 3, 26, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage
            {
                Role = "user",
                Content = "пішов грати .. розмову завершено покрайній мірі до ранку",
                Timestamp = now.AddMinutes(-29)
            }
        };

        var service = new KokoProactiveContextService();
        var frame = service.Build(messages, new KokoInternalState(), now);
        var check = service.Check("Ти вже знову тут, чи ще в іграх застряг?", frame, "silence_l1");

        AssertTrue(frame.ShouldStaySilentForSleep, "conversation close until morning should force proactive silence");
        AssertTrue(!check.Passed, "proactive ping should be blocked after explicit conversation close");
        AssertEqual("[мовчання]", check.Replacement, "closed conversation should turn proactive into silence");
    }

    private static void ProactiveFallbackNeverExposesTechnicalSilenceWording()
    {
        var now = new DateTime(2026, 5, 8, 20, 44, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "ти моя киця", Timestamp = now.AddMinutes(-89) },
            new ChatRepository.ChatMessage { Role = "assistant", Content = "Я не «киця». Але продовжуй ризикувати.", Timestamp = now.AddMinutes(-88) }
        };

        var service = new KokoProactiveContextService();
        var frame = service.Build(messages, new KokoInternalState(), now);
        var fallback = service.BuildFallback(frame, "silence_l1");

        AssertTrue(fallback is "[мовчання]" or "[мовчання]", "fallback after an assistant reply should prefer silence");
        AssertTrue(!fallback.Contains("без другого кола"), "fallback must not expose guard mechanics");
        AssertTrue(!fallback.Contains("ще актуально"), "fallback must not quote a live chat line as a stale task");
        AssertTrue(!fallback.Contains("зайві символи"), "fallback must not use canned technical wording");
    }

    private static void ProactiveFallbackDoesNotQuoteLastUserText()
    {
        var now = new DateTime(2026, 5, 19, 15, 37, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "чому ти не написала", Timestamp = now.AddHours(-2) }
        };

        var service = new KokoProactiveContextService();
        var frame = service.Build(messages, new KokoInternalState(), now);
        var fallback = service.BuildFallback(frame, "silence_l1");

        AssertTrue(!fallback.Contains("чому ти не написала", StringComparison.OrdinalIgnoreCase), "proactive fallback should not quote raw latest user text");
        AssertTrue(!fallback.Contains("«"), "proactive fallback should avoid quote framing");
        AssertTrue(fallback.Contains("останню задачу") || fallback.Contains("контекст") || fallback.Contains("теми"), "fallback should paraphrase the context instead");
    }

    private static void UserQuietCommandMutesProactiveFollowups()
    {
        using var ctx = TestContext.Create();
        using var health = new HealthService(ctx.TestDir);
        using var brain = new KokoBrainEngine(new LlmService(), health, new ObsidianMcpService(ctx.TestDir), ctx.Chat, ctx.TestDir);
        brain.State.PendingTriggers.Add(new ReactiveTrigger
        {
            Type = "intent_followup",
            FireAt = DateTime.Now.AddMinutes(-1),
            Context = "old course followup"
        });
        brain.State.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "course",
            Summary = "old course",
            CreatedAt = DateTime.Now.AddHours(-2),
            FollowUpAt = DateTime.Now.AddHours(-1),
            ExpectedUntil = DateTime.Now.AddMinutes(-30)
        });

        var handled = brain.TryApplyUserControlCommand("Мяв, іди відпочинь", out var reply);

        AssertTrue(handled, "quiet command should be handled as a command");
        AssertTrue(brain.State.ProactiveMutedUntil > DateTime.Now, "proactive should be muted");
        AssertTrue(brain.State.PendingTriggers.Count == 0, "quiet command should clear stale followups");
        AssertTrue(brain.State.ShortTermIntents.All(i => i.ResolvedAt.HasValue), "quiet command should resolve active intents");
        AssertTrue(reply.Contains("нагадування") || reply.Contains("Мовчу"), "reply should confirm mode change without internal mechanics");
    }

    private static void UserQuietCommandReplyHidesMechanics()
    {
        using var ctx = TestContext.Create();
        using var health = new HealthService(ctx.TestDir);
        using var brain = new KokoBrainEngine(new LlmService(), health, new ObsidianMcpService(ctx.TestDir), ctx.Chat, ctx.TestDir);

        var handled = brain.TryApplyUserControlCommand("Мяв, іди відпочинь", out var reply);

        AssertTrue(handled, "natural quiet command should be handled");
        AssertTrue(!reply.Contains("autoping", StringComparison.OrdinalIgnoreCase), "reply should hide autoping mechanics");
        AssertTrue(!reply.Contains("автоп", StringComparison.OrdinalIgnoreCase), "reply should hide Ukrainian autoping mechanics");
        AssertTrue(!reply.Contains("follow-up", StringComparison.OrdinalIgnoreCase), "reply should hide follow-up mechanics");
        AssertTrue(!reply.Contains("scheduler", StringComparison.OrdinalIgnoreCase), "reply should hide scheduler mechanics");
    }

    private static void ReminderScheduledReplyHidesSchedulerIds()
    {
        var method = typeof(LlmService).GetMethod("BuildReminderScheduledReply", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        AssertTrue(method != null, "reminder reply builder should exist");
        var reminder = new ReminderCommand
        {
            FireAt = new DateTime(2026, 6, 13, 21, 5, 0),
            Prompt = "перевірити нотатку",
            IsWake = false,
            UsedAssumedLater = false
        };

        var reply = (string)method!.Invoke(null, new object[] { reminder, "9d69553ed6" })!;

        AssertTrue(reply.Contains("21:05"), "reply should include scheduled time");
        AssertTrue(!reply.Contains("scheduler", StringComparison.OrdinalIgnoreCase), "visible reminder reply should not expose scheduler");
        AssertTrue(!reply.Contains("9d69553ed6", StringComparison.OrdinalIgnoreCase), "visible reminder reply should not expose record id");
    }

    private static void ActiveAgencyActionReportsStayInternal()
    {
        using var ctx = TestContext.Create();
        var service = new KokoActiveAgencyService(
            ctx.TestDir,
            new PcControlService(),
            ctx.Chat,
            new KokoInternalBlackboardService(ctx.TestDir),
            new KokoServiceHeartbeatService(ctx.TestDir),
            () => null,
            () => null);
        var method = typeof(KokoActiveAgencyService).GetMethod("AppendActionMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        AssertTrue(method != null, "active agency internal report method should exist");

        method!.Invoke(service, new object[] { "[ACTION:test] should stay internal" });

        AssertTrue(!ctx.Chat.GetMessages(10).Any(m => m.Content.Contains("[ACTION:", StringComparison.OrdinalIgnoreCase)), "active agency reports should not be inserted into chat");
    }

    private static void ResearchActionReportsStayInternal()
    {
        using var ctx = TestContext.Create();
        var service = new KokoResearchService(
            ctx.TestDir,
            new SearchService(ctx.Chat, ctx.TestDir),
            ctx.Chat,
            new ObsidianMcpService(ctx.TestDir),
            new KokoInternalBlackboardService(ctx.TestDir),
            new KokoServiceHeartbeatService(ctx.TestDir),
            () => null);
        var method = typeof(KokoResearchService).GetMethod("AppendActionMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        AssertTrue(method != null, "research internal report method should exist");

        method!.Invoke(service, new object[] { "[ACTION:research] should stay internal" });

        AssertTrue(!ctx.Chat.GetMessages(10).Any(m => m.Content.Contains("[ACTION:", StringComparison.OrdinalIgnoreCase)), "research reports should not be inserted into chat");
    }

    private static void BlackboardBuildsPromptSnapshotAndReloadsTail()
    {
        using var ctx = TestContext.Create();
        var blackboard = new KokoInternalBlackboardService(ctx.TestDir);
        blackboard.Publish("scientist-agent", "proposal", "inspect evidence before answer", 0.75, status: "proposed");
        blackboard.Publish("coordinator-agent", "decision", "answer current message first", 0.85, status: "resolved");

        var prompt = blackboard.BuildPromptBlock(5);
        AssertTrue(prompt.Contains("INTERNAL BLACKBOARD RECENT", StringComparison.OrdinalIgnoreCase), "blackboard should build prompt block");
        AssertTrue(prompt.Contains("coordinator-agent/decision", StringComparison.OrdinalIgnoreCase), "blackboard prompt should include typed event");
        AssertTrue(prompt.Contains("status=resolved", StringComparison.OrdinalIgnoreCase), "blackboard prompt should include status");

        var reloaded = new KokoInternalBlackboardService(ctx.TestDir);
        AssertTrue(reloaded.Recent(5).Any(e => e.Kind == "decision" && e.Summary.Contains("answer current message", StringComparison.OrdinalIgnoreCase)), "blackboard should reload persisted tail");
    }

    private static void GenesisAgentFactoryRegistersRoleProfile()
    {
        using var ctx = TestContext.Create();
        var blackboard = new KokoInternalBlackboardService(ctx.TestDir);
        var factory = new KokoDynamicAgentFactoryService(ctx.TestDir, blackboard);
        var settings = new AppSettings
        {
            LlmProvider = "ollama-cloud",
            OllamaModel = AppSettings.DefaultOllamaCloudModel,
            AgentLlmProfiles = new Dictionary<string, KokoAgentLlmProfile>(StringComparer.OrdinalIgnoreCase)
        };

        var agent = factory.CreateOrUpdateAgent(
            settings,
            "Deep Researcher",
            "analyst",
            provider: "ollama-cloud",
            model: "gemma4:31b-cloud",
            saveSettings: false);

        AssertEqual("deep-researcher", agent.AgentId, "agent id should be normalized");
        AssertEqual("analyst", agent.RoleId, "role should persist");
        AssertTrue(settings.AgentLlmProfiles.ContainsKey("deep-researcher"), "agent profile should be registered in supplied settings");
        AssertEqual("gemma4:31b-cloud", settings.AgentLlmProfiles["deep-researcher"].Model, "model override should persist");
        AssertTrue(factory.RenderConsole().Contains("deep-researcher"), "console should expose registered agent");
        AssertTrue(blackboard.Recent(5).Any(e => e.Kind == "agent_registered"), "factory should publish registration event");
    }

    private static void GenesisAgentFactoryDisablesProfile()
    {
        using var ctx = TestContext.Create();
        var factory = new KokoDynamicAgentFactoryService(ctx.TestDir);
        var settings = new AppSettings
        {
            LlmProvider = "lmstudio",
            Model = "local-model",
            AgentLlmProfiles = new Dictionary<string, KokoAgentLlmProfile>(StringComparer.OrdinalIgnoreCase)
        };

        factory.CreateOrUpdateAgent(settings, "creative-one", "creative", saveSettings: false);
        var ok = factory.SetAgentEnabled("creative-one", false);
        var snap = factory.GetSnapshot();

        AssertTrue(ok, "disable should find managed agent");
        AssertTrue(snap.Agents.Any(a => a.AgentId == "creative-one" && !a.Enabled && a.Status == "disabled"),
            "descriptor should be disabled");
    }

    private static void ToolGatewayVerifiesFileWritesAndDirectories()
    {
        using var ctx = TestContext.Create();
        var workspace = Path.Combine(ctx.TestDir, "gateway-workspace");
        var gateway = new KokoToolGateway(
            new KokoFileSystemToolService(workspace),
            new PcActionExecutor());

        var directory = gateway.ExecuteAsync(new KokoToolCall
        {
            Name = "fs_create_directory",
            Arguments = new Dictionary<string, string> { ["path"] = "notes" }
        }).GetAwaiter().GetResult();
        AssertTrue(directory.Success && directory.Verified, "directory creation must be verified by gateway");
        AssertTrue(Directory.Exists(Path.Combine(workspace, "notes")), "verified directory must exist on disk");

        var write = gateway.ExecuteAsync(new KokoToolCall
        {
            Name = "fs_write_text",
            Confirmed = true,
            Arguments = new Dictionary<string, string>
            {
                ["path"] = "notes/result.txt",
                ["content"] = "gateway wrote this"
            }
        }).GetAwaiter().GetResult();
        AssertTrue(write.Success && write.Verified, "write must not report success before read-back verification");
        AssertEqual("gateway wrote this", File.ReadAllText(Path.Combine(workspace, "notes", "result.txt")), "verified write content should match");
    }

    private static void ToolGatewayExposesConfirmationAndFailures()
    {
        using var ctx = TestContext.Create();
        var gateway = new KokoToolGateway(
            new KokoFileSystemToolService(Path.Combine(ctx.TestDir, "gateway-workspace")),
            new PcActionExecutor());

        var confirmation = gateway.ExecuteAsync(new KokoToolCall
        {
            Name = "fs_write_text",
            Arguments = new Dictionary<string, string>
            {
                ["path"] = "blocked.txt",
                ["content"] = "must not exist"
            }
        }).GetAwaiter().GetResult();
        AssertTrue(confirmation.RequiresConfirmation, "write without confirmation must be visible, not silently ignored");
        AssertTrue(!confirmation.Success && !confirmation.Verified, "unconfirmed write must not report success");

        var unknown = gateway.ExecuteAsync(new KokoToolCall { Name = "not_registered" }).GetAwaiter().GetResult();
        AssertTrue(!unknown.Success && unknown.Reason.Contains("недоступний", StringComparison.OrdinalIgnoreCase), "unknown tool must return explicit failure");
    }

    private static void ToolGatewayStopsExecutionPlanAfterFailure()
    {
        using var ctx = TestContext.Create();
        var workspace = Path.Combine(ctx.TestDir, "gateway-workspace");
        var gateway = new KokoToolGateway(new KokoFileSystemToolService(workspace), new PcActionExecutor());
        var results = gateway.ExecutePlanAsync(new[]
        {
            new KokoToolCall { Name = "not_registered" },
            new KokoToolCall
            {
                Name = "fs_create_directory",
                Arguments = new Dictionary<string, string> { ["path"] = "must-not-run" }
            }
        }).GetAwaiter().GetResult();

        AssertEqual(1, results.Count, "execution plan must stop after first failed tool");
        AssertTrue(!Directory.Exists(Path.Combine(workspace, "must-not-run")), "steps after failure must not execute");
    }

    private static void ToolRegistryExposesCapabilityScopedManifest()
    {
        var workspace = TempDir();
        try
        {
            var gateway = new KokoToolGateway(new KokoFileSystemToolService(workspace), new PcActionExecutor());
            gateway.Register(new KokoCodeActToolHandler(workspace));

            AssertEqual(10, gateway.Tools.Count, "registry must describe every executable gateway handler");
            var delete = gateway.Tools.Single(tool => tool.Name == "fs_delete");
            AssertEqual(KokoToolRisk.Destructive, delete.Risk, "delete must be classified as destructive");
            AssertTrue(delete.RequiresConfirmation, "delete descriptor must advertise confirmation");

            var coderTools = gateway.GetToolsForCapabilities(new[] { "code", "sandbox" });
            AssertTrue(coderTools.Any(tool => tool.Name == "codeact_python"), "coder manifest must include CodeAct");
            AssertTrue(coderTools.Any(tool => tool.Name == "fs_write_text"), "coder manifest must include workspace writes");
            AssertTrue(coderTools.All(tool => !tool.Name.StartsWith("pc_", StringComparison.Ordinal)), "coder manifest must mask unrelated PC controls");

            var prompt = gateway.BuildToolPromptBlock(new[] { "files", "maintenance" });
            AssertTrue(prompt.Contains("fs_delete [filesystem/Destructive]", StringComparison.Ordinal), "prompt must expose risk metadata");
            AssertTrue(prompt.Contains("confirmation required", StringComparison.OrdinalIgnoreCase), "prompt must expose confirmation policy");
        }
        finally
        {
            TryDeleteDir(workspace);
        }
    }

    private static void CodeActPolicyBlocksHostAccess()
    {
        AssertTrue(KokoCodeActPolicy.Validate("import math\nprint(math.sqrt(81))").Allowed,
            "pure calculation imports should be available to CodeAct");
        AssertTrue(!KokoCodeActPolicy.Validate("import os\nprint(os.getcwd())").Allowed,
            "host OS imports must be rejected before execution");
        AssertTrue(!KokoCodeActPolicy.Validate("print(open('secret.txt').read())").Allowed,
            "direct file access must stay behind ToolGateway");
        AssertTrue(!KokoCodeActPolicy.Validate("print((1).__class__)").Allowed,
            "dunder traversal must be blocked to protect the restricted runtime");
        AssertTrue(new LlmService().GetAvailableToolNames().Contains("codeact_python"),
            "CodeAct must be exposed to the LLM tool catalog");
        AssertTrue(new KokoCapabilityManifestService().BuildPromptBlock().Contains("CodeAct", StringComparison.Ordinal),
            "orchestrator capability context must advertise the restricted CodeAct route");
    }

    private static void CodeActToolExecutesRestrictedPython()
    {
        var dir = TempDir();
        try
        {
            var handler = new KokoCodeActToolHandler(dir);
            var call = new KokoToolCall
            {
                Id = "calculation-1",
                Name = "codeact_python",
                Arguments = new Dictionary<string, string>
                {
                    ["runId"] = "test-calculation",
                    ["code"] = "import math\nvalues = [4, 9, 16]\nprint(sum(math.sqrt(v) for v in values))"
                }
            };
            var result = handler.ExecuteAsync(call, CancellationToken.None).GetAwaiter().GetResult();
            var execution = result.RawResult as KokoCodeActExecutionResult;

            AssertTrue(execution != null && execution.Executed, "accepted CodeAct code must reach the restricted runner");
            AssertTrue(result.Success || result.Output.Contains("unavailable", StringComparison.OrdinalIgnoreCase),
                "calculation should execute when Python is installed or report the missing runtime explicitly");
            if (result.Success)
                AssertTrue(result.Output.Contains("9.0", StringComparison.Ordinal), "CodeAct output must contain the real calculation result");
            AssertTrue(File.Exists(Path.Combine(dir, "codeact-runs", execution!.CodeArtifact)),
                "generated code must be preserved as recoverable context");
            AssertTrue(File.Exists(Path.Combine(dir, "codeact-runs", execution.ResultArtifact)),
                "execution output must be preserved as recoverable context");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void CodeActToolPreservesSyntaxFailureEvidence()
    {
        var dir = TempDir();
        try
        {
            var handler = new KokoCodeActToolHandler(dir);
            var result = handler.ExecuteAsync(new KokoToolCall
            {
                Id = "syntax-failure",
                Name = "codeact_python",
                Arguments = new Dictionary<string, string>
                {
                    ["runId"] = "test-errors",
                    ["code"] = "for value in:\n    print(value)"
                }
            }, CancellationToken.None).GetAwaiter().GetResult();
            var execution = result.RawResult as KokoCodeActExecutionResult;

            AssertTrue(!result.Success, "invalid generated code must not be reported as success");
            AssertTrue(execution != null && execution.Executed, "syntax validation belongs to the restricted runtime evidence");
            AssertTrue(result.Output.Contains("SyntaxError", StringComparison.OrdinalIgnoreCase) ||
                       result.Output.Contains("unavailable", StringComparison.OrdinalIgnoreCase),
                "failure observation must explain syntax failure or missing Python");
            AssertTrue(File.Exists(Path.Combine(dir, "codeact-runs", execution!.ResultArtifact)),
                "failed output must remain available for replanning and reflection");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void SystemOverlordScansMetadataAndProposesCleanup()
    {
        using var ctx = TestContext.Create();
        var downloads = Path.Combine(ctx.TestDir, "Downloads");
        Directory.CreateDirectory(downloads);
        var staleArchive = Path.Combine(downloads, "old-pack.zip");
        File.WriteAllBytes(staleArchive, new byte[1024]);
        File.SetLastWriteTime(staleArchive, DateTime.Now.AddDays(-45));
        File.WriteAllText(Path.Combine(downloads, "note.md"), "hello");

        var blackboard = new KokoInternalBlackboardService(ctx.TestDir);
        var service = new KokoSystemOverlordService(ctx.TestDir, blackboard, new KokoServiceHeartbeatService(ctx.TestDir));
        var snap = service.ScanAsync(new[] { downloads }, maxFiles: 10).GetAwaiter().GetResult();

        AssertEqual("ready", snap.Status, "scan should complete");
        AssertTrue(snap.ScannedFiles >= 2, "scan should index files");
        AssertTrue(snap.Files.Any(f => f.Bucket == "archive" && f.Signal == "stale_download"), "stale download archive should be classified");
        AssertTrue(snap.Proposals.Any(p => p.Kind == "cleanup" && p.Decision == PcPolicyDecisionKind.NeedsConfirmation),
            "cleanup should be proposal, not execution");
        AssertTrue(blackboard.Recent(5).Any(e => e.Agent == "system-overlord" && e.Kind == "index"),
            "scan should publish blackboard event");
    }

    private static void SystemOverlordCleanupRequiresPermission()
    {
        using var ctx = TestContext.Create();
        var target = Path.Combine(ctx.TestDir, "victim.tmp");
        File.WriteAllText(target, "do not delete in test");

        var service = new KokoSystemOverlordService(ctx.TestDir);
        var proposal = service.PrepareCleanupPermission(new[] { target }, "test cleanup");

        AssertEqual(PcPolicyDecisionKind.NeedsConfirmation, proposal.Decision, "delete cleanup should require confirmation");
        AssertTrue(!string.IsNullOrWhiteSpace(proposal.PendingActionId), "pending confirmation id should be created");
        AssertTrue(File.Exists(target), "prepare step must not delete the file");
    }

    private static void SystemOverlordDetectsRetrySurpriseDirective()
    {
        AssertTrue(
            KokoSystemOverlordService.LooksLikeSystemOverlordDirective("я зробив апдейт, спробуй ще раз залишити сюрприз"),
            "retry surprise request should route to real system overlord instead of LLM roleplay");
        AssertTrue(
            KokoSystemOverlordService.LooksLikeSystemOverlordDirective("давай актуально створи файл на робочому столі"),
            "desktop artifact request should route to real file action");
    }

    private static void ActionDirectiveRouterGeneralizesLocalArtifacts()
    {
        var variants = new[]
        {
            "поклади щось цікаве на робочий стіл",
            "зроби мені файл-сюрприз",
            "розбери downloads і залиш короткий звіт",
            "find something interesting on my pc and save a note"
        };

        foreach (var text in variants)
        {
            var route = KokoActionDirectiveRouter.Analyze(text);
            AssertEqual(KokoActionDirectiveRoute.LocalArtifact, route.Route, "local artifact wording should not depend on one exact phrase: " + text);
        }
    }

    private static void ActionDirectiveRouterSendsNonArtifactTasksToAgents()
    {
        var task = KokoActionDirectiveRouter.Analyze("пофікси gui і прожени тести нормально");
        AssertEqual(KokoActionDirectiveRoute.AgentTask, task.Route, "multi-step project work should become agent task");
        AssertTrue(task.Confidence >= 60, "agent task confidence should be high enough to bypass LLM-only answer");

        var chat = KokoActionDirectiveRouter.Analyze("просто поговоримо про дурниці");
        AssertEqual(KokoActionDirectiveRoute.None, chat.Route, "plain conversation should not become fake action");
    }

    private static void LlmGuardBlocksUnverifiedActionClaims()
    {
        AssertTrue(
            LlmService.LooksLikeUnverifiedActionClaim(
                "Created file C:\\Users\\User\\Desktop\\Kokonoe_Check.txt. Check it.",
                "leave a surprise file on desktop"),
            "LLM must not claim desktop artifact creation without tool evidence");

        AssertTrue(
            !LlmService.LooksLikeUnverifiedActionClaim(
                "Did not create the file: no verified tool_result is available.",
                "leave a surprise file on desktop"),
            "honest non-execution should pass through");

        AssertTrue(
            !LlmService.LooksLikeUnverifiedActionClaim(
                "We can just talk about it.",
                "just talk"),
            "conversation must not be treated as a failed action");
    }

    private static void DetectBestToolPicksFsToolsForDiskRequests()
    {
        // "посортуй фотки і все ... в завантаженні" used to fall through every check in
        // DetectBestTool to the append_to_note catch-all - a vault tool forced onto a disk
        // request with no vault involved at all. fs_list_directory is the sane default first
        // step for an unspecific sort/cleanup ask: you inspect a folder before moving anything.
        AssertEqual("fs_list_directory", LlmService.DetectBestTool("посортуй фотки і все ... в завантаженні"),
            "unspecific disk sort/cleanup request should default to listing the folder first");

        AssertEqual("fs_delete", LlmService.DetectBestTool("видали файл з завантажень"),
            "delete-flavored disk request should force fs_delete");

        AssertEqual("fs_create_directory", LlmService.DetectBestTool("створи папку Software_Docs в завантаженнях"),
            "create-folder-flavored disk request should force fs_create_directory, not the vault create_folder tool");

        AssertEqual("fs_move", LlmService.DetectBestTool("перенеси фото в нову папку"),
            "move-flavored disk request should force fs_move");

        // Vault-note requests must still resolve to Obsidian tools, not get hijacked by the
        // disk-flavored checks just because they happen to mention an unrelated word.
        AssertEqual("create_note", LlmService.DetectBestTool("створи нову нотатку в vault"),
            "explicit vault/note request must stay on the Obsidian tool path");
    }

    private static void TruncateNoteForToolResultCapsOversizedNotes()
    {
        // The actual trigger logged in production: a real 18593-char consolidated facts
        // note read back as a tool result, with nothing capping it, left a modest model no
        // budget to produce a visible answer on the forced final summary round.
        var oversized = new string('a', 18593);
        var truncated = LlmService.TruncateNoteForToolResult(oversized, "Kokonoe/Memory/Facts.md");
        AssertTrue(truncated.Length < oversized.Length, "oversized note must be shortened, not passed through whole");
        AssertTrue(truncated.Contains("truncated"), "truncated note must say so, not silently drop content");
        AssertTrue(truncated.Contains("Kokonoe/Memory/Facts.md"), "truncation notice should name the file so a follow-up read targets it");

        var small = "short note content";
        AssertEqual(small, LlmService.TruncateNoteForToolResult(small, "whatever.md"),
            "a note under the cap must pass through unchanged");
    }

    private static void GeneratedContentRouteStaysSeparateFromSurpriseScan()
    {
        var generated = new[]
        {
            "створи нотатку про дракона",
            "напиши вірш про нічне місто",
            "создай файл с текстом о проекте Mercury",
            "write a short poem about Mercury"
        };
        foreach (var phrase in generated)
        {
            var directive = KokoActionDirectiveRouter.Analyze(phrase);
            AssertEqual(KokoActionDirectiveRoute.GeneratedContentArtifact, directive.Route,
                "specific content must use generated artifact route: " + phrase);
            AssertTrue(!KokoSystemOverlordService.LooksLikeSystemOverlordDirective(phrase),
                "specific content must not enter surprise scan: " + phrase);
        }

        var vague = new[]
        {
            "зроби мені файл-сюрприз",
            "поклади щось цікаве на робочий стіл",
            "find something interesting on my pc and save a note"
        };
        foreach (var phrase in vague)
            AssertEqual(KokoActionDirectiveRoute.LocalArtifact, KokoActionDirectiveRouter.Analyze(phrase).Route,
                "vague surprise/search must retain local scan route: " + phrase);
    }

    private static void ChatRuntimeDefaultsToStreamingWithBoundedTokenBudget()
    {
        var request = new KokoAgentChatRequest();
        AssertTrue(request.PreferStreaming, "chat runtime must prefer streaming by default");

        AssertEqual(8192, new AppSettings().MaxTokens,
            "fresh-install default should allow a generous reply without hitting the 16384 ceiling");
        AssertTrue(!new AppSettings().UnlimitedResponse,
            "unlimited response must be an explicit opt-in, not a fresh-install default");
    }

    private static void WebBridgeCompletesPingPongRoundTrip()
    {
        string? responseJson = null;
        using var bridge = new KokoWebBridgeService(json => responseJson = json);
        bridge.HandleMessageAsync("""{"type":"request","id":"ping-1","method":"ping","payload":{"source":"test"}}""")
            .GetAwaiter().GetResult();

        var response = Newtonsoft.Json.Linq.JObject.Parse(responseJson ?? "{}");
        AssertEqual("response", response["type"]?.ToString(), "bridge must emit response envelope");
        AssertEqual("ping-1", response["id"]?.ToString(), "bridge must preserve correlation id");
        AssertEqual("pong", response["result"]?.ToString(), "ping must return pong");
    }

    private static void BrainStaticContextCacheHonorsTtl()
    {
        var dir = TempDir();
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Creator/Profile.md", "# Profile\n\nCache version alpha");
            using var health = new HealthService(dir);
            using var chat = new ChatRepository(dir);
            using var brain = new KokoBrainEngine(new LlmService(), health, obsidian, chat, dir);

            var first = InvokePrivateStringTask(brain, "BuildStaticContextBlockAsync");
            obsidian.WriteNote("Creator/Profile.md", "# Profile\n\nCache version beta");
            var cached = InvokePrivateStringTask(brain, "BuildStaticContextBlockAsync");

            AssertTrue(first.Contains("Cache version alpha"), "initial static block must read the vault profile");
            AssertTrue(cached.Contains("Cache version alpha") && !cached.Contains("Cache version beta"),
                "static block must be reused before TTL expiry");

            SetPrivateField(brain, "_staticContextExpiry", DateTime.MinValue);
            var refreshed = InvokePrivateStringTask(brain, "BuildStaticContextBlockAsync");
            AssertTrue(refreshed.Contains("Cache version beta"), "expired static block must refresh from disk");
            AssertEqual("1", GetPrivateField<long>(brain, "_staticContextCacheHits").ToString(),
                "second static read should be one cache hit");
            AssertEqual("2", GetPrivateField<long>(brain, "_staticContextCacheMisses").ToString(),
                "initial and expired reads should be cache misses");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void BrainContextKeepsRecentChatDynamic()
    {
        var dir = TempDir();
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            using var health = new HealthService(dir);
            using var chat = new ChatRepository(dir);
            using var brain = new KokoBrainEngine(new LlmService(), health, obsidian, chat, dir);
            chat.InsertMessage(new ChatRepository.ChatMessage
            {
                Role = "user",
                Content = "dynamic-context-first",
                Timestamp = DateTime.Now.AddSeconds(-2)
            });
            _ = InvokePrivateStringTask(brain, "BuildContextAsync");

            chat.InsertMessage(new ChatRepository.ChatMessage
            {
                Role = "user",
                Content = "dynamic-context-second",
                Timestamp = DateTime.Now
            });
            var second = InvokePrivateStringTask(brain, "BuildContextAsync");

            AssertTrue(second.Contains("dynamic-context-second"),
                "recent chat must be rebuilt even while static context is cached");
            AssertTrue(GetPrivateField<long>(brain, "_staticContextCacheHits") >= 1,
                "second full context build should reuse the static block");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static string InvokePrivateStringTask(object target, string methodName)
    {
        var method = target.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Private method not found: {methodName}");
        var arguments = method.GetParameters()
            .Select(parameter => parameter.HasDefaultValue ? parameter.DefaultValue : null)
            .ToArray();
        var task = method.Invoke(target, arguments) as Task<string>
            ?? throw new InvalidOperationException($"Private method did not return Task<string>: {methodName}");
        return task.GetAwaiter().GetResult();
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Private field not found: {fieldName}");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Private field not found: {fieldName}");
        return (T)(field.GetValue(target) ?? throw new InvalidOperationException($"Private field is null: {fieldName}"));
    }

    private static void WebBridgeReportsUnknownMethods()
    {
        string? responseJson = null;
        using var bridge = new KokoWebBridgeService(json => responseJson = json);
        bridge.HandleMessageAsync("""{"type":"request","id":"bad-1","method":"missing","payload":null}""")
            .GetAwaiter().GetResult();

        var response = Newtonsoft.Json.Linq.JObject.Parse(responseJson ?? "{}");
        AssertEqual("bad-1", response["id"]?.ToString(), "error response must preserve correlation id");
        AssertTrue(response["error"]?.ToString().Contains("Unknown bridge method", StringComparison.OrdinalIgnoreCase) == true,
            "unknown bridge method must return a structured error");
    }

    private static void WebBridgeContractCoversFrontendAndCompatibilityMethods()
    {
        var dir = TempDir();
        try
        {
            var envelopes = new List<string>();
            using var bridge = new KokoWebBridgeService(envelopes.Add);
            using var chat = new KokoWebChatBridgeService(
                bridge,
                (text, context, chunk, ct) => Task.FromResult<string?>("ok"),
                (text, context, ct) => Task.FromResult("ok"));
            var tasks = new KokoAgentTaskService(Path.Combine(dir, "tasks")) { AutoStartOnAdd = false };
            using var agents = new KokoWebAgentBridgeService(bridge, tasks);
            using var settings = new KokoWebSettingsBridgeService(
                bridge,
                () => new AppSettings(),
                _ => null);
            using var vault = new KokoWebVaultBridgeService(bridge, new ObsidianMcpService(Path.Combine(dir, "vault")));
            using var memory = new KokoWebMemoryBridgeService(bridge, () => new
            {
                takenAt = DateTime.UtcNow,
                factCount = 0,
                episodeCount = 0,
                sessionFactCount = 0,
                facts = Array.Empty<object>(),
                episodes = Array.Empty<object>(),
                sessionFacts = Array.Empty<string>()
            });
            using var telegram = new KokoWebTelegramBridgeService(
                bridge,
                new KokoTelegramRuntimeStatusService(),
                () => new AppSettings());
            using var runtime = new KokoWebRuntimeBridgeService(bridge, () => new
            {
                takenAt = DateTime.UtcNow,
                heartbeat = new { entries = Array.Empty<object>() }
            });
            using var system = new KokoWebSystemBridgeService(
                bridge,
                new KokoSystemOverlordService(Path.Combine(dir, "overlord")));

            var required = new[]
            {
                "ping", "chat.send", "agent.snapshot", "agent.start", "agent.start_many", "agent.cancel",
                "agent.runner.status", "agent.runner.start", "agent.runner.stop",
                "settings.get", "settings.update",
                "vault.status", "vault.refresh", "memory.snapshot", "memory.refresh", "telegram.status", "runtime.snapshot", "runtime.refresh",
                "system.snapshot", "system.scan",
                "send_message", "get_agent_tasks", "start_agent_task", "load_settings", "save_settings",
                "vault_status", "telegram_status", "system_overlord_status"
            };
            foreach (var method in required)
                AssertTrue(bridge.RegisteredMethods.Contains(method, StringComparer.OrdinalIgnoreCase),
                    "bridge contract is missing method: " + method);
            AssertTrue(bridge.RegisteredMethods.Count >= 16, "composed shell bridge should expose the full contract");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void WebRuntimeBridgeReturnsSnapshotAndPublishesRefresh()
    {
        var envelopes = new List<string>();
        using var bridge = new KokoWebBridgeService(envelopes.Add);
        using var runtime = new KokoWebRuntimeBridgeService(bridge, () => new
        {
            takenAt = new DateTime(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc),
            process = new { pid = 42, workingSetMb = 128.5, responding = true },
            heartbeat = new
            {
                entries = new[]
                {
                    new { service = "WATCH", status = "OK", detail = "fresh", ageSeconds = 1.0 }
                }
            }
        });

        bridge.HandleMessageAsync("""{"type":"request","id":"rt-1","method":"runtime.snapshot","payload":null}""")
            .GetAwaiter().GetResult();
        bridge.HandleMessageAsync("""{"type":"request","id":"rt-2","method":"runtime.refresh","payload":null}""")
            .GetAwaiter().GetResult();

        var parsed = envelopes.Select(e => Newtonsoft.Json.Linq.JObject.Parse(e)).ToList();
        AssertTrue(parsed.Any(x => x["type"]?.ToString() == "response" &&
                                   x["id"]?.ToString() == "rt-1" &&
                                   x["result"]?["process"]?["pid"]?.Value<int>() == 42),
            "runtime.snapshot must return process payload");
        AssertTrue(parsed.Any(x => x["type"]?.ToString() == "event" &&
                                   x["channel"]?.ToString() == "runtime.snapshot" &&
                                   x["payload"]?["heartbeat"]?["entries"]?.Any() == true),
            "runtime.refresh must publish a live runtime snapshot event");
    }

    private static void WebSystemBridgeScansAndPublishesSnapshot()
    {
        var dir = TempDir();
        try
        {
            var scanRoot = Path.Combine(dir, "scan");
            Directory.CreateDirectory(scanRoot);
            File.WriteAllText(Path.Combine(scanRoot, "note.txt"), "system bridge evidence");
            var envelopes = new List<string>();
            using var bridge = new KokoWebBridgeService(envelopes.Add);
            using var system = new KokoWebSystemBridgeService(
                bridge,
                new KokoSystemOverlordService(Path.Combine(dir, "data")));
            var payload = new Newtonsoft.Json.Linq.JObject
            {
                ["roots"] = new Newtonsoft.Json.Linq.JArray(scanRoot),
                ["maxFiles"] = 25
            };
            var request = new Newtonsoft.Json.Linq.JObject
            {
                ["type"] = "request",
                ["id"] = "sys-1",
                ["method"] = "system.scan",
                ["payload"] = payload
            };

            bridge.HandleMessageAsync(request.ToString(Newtonsoft.Json.Formatting.None))
                .GetAwaiter().GetResult();

            var parsed = envelopes.Select(e => Newtonsoft.Json.Linq.JObject.Parse(e)).ToList();
            AssertTrue(parsed.Any(x => x["type"]?.ToString() == "response" &&
                                       x["id"]?.ToString() == "sys-1" &&
                                       x["result"]?["scannedFiles"]?.Value<int>() >= 1),
                "system.scan must return real scan facts");
            AssertTrue(parsed.Any(x => x["type"]?.ToString() == "event" &&
                                       x["channel"]?.ToString() == "system.snapshot" &&
                                       x["payload"]?["files"]?.Any() == true),
                "system.scan must publish the snapshot for WebView");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void WebMemoryBridgeReturnsFactsAndPublishesRefresh()
    {
        var envelopes = new List<string>();
        using var bridge = new KokoWebBridgeService(envelopes.Add);
        using var memory = new KokoWebMemoryBridgeService(bridge, () => new
        {
            takenAt = new DateTime(2026, 6, 23, 12, 30, 0, DateTimeKind.Utc),
            factCount = 1,
            episodeCount = 1,
            sessionFactCount = 1,
            facts = new[]
            {
                new { id = "fact1", content = "User wants concrete execution", category = "preference", importance = 0.9, confirmCount = 2 }
            },
            episodes = new[]
            {
                new { id = "ep1", summary = "WebView bridge work continued", emotionalTone = "focused", intensity = 0.7 }
            },
            sessionFacts = new[] { "[project] memory bridge added" }
        });

        bridge.HandleMessageAsync("""{"type":"request","id":"mem-1","method":"memory.snapshot","payload":null}""")
            .GetAwaiter().GetResult();
        bridge.HandleMessageAsync("""{"type":"request","id":"mem-2","method":"memory.refresh","payload":null}""")
            .GetAwaiter().GetResult();

        var parsed = envelopes.Select(e => Newtonsoft.Json.Linq.JObject.Parse(e)).ToList();
        AssertTrue(parsed.Any(x => x["type"]?.ToString() == "response" &&
                                   x["id"]?.ToString() == "mem-1" &&
                                   x["result"]?["factCount"]?.Value<int>() == 1 &&
                                   x["result"]?["facts"]?[0]?["content"]?.ToString()?.Contains("concrete execution") == true),
            "memory.snapshot must return fact payload");
        AssertTrue(parsed.Any(x => x["type"]?.ToString() == "event" &&
                                   x["channel"]?.ToString() == "memory.snapshot" &&
                                   x["payload"]?["episodes"]?.Any() == true),
            "memory.refresh must publish a live memory snapshot event");
    }

    private static void WebStartupPolicyDefaultsToWebWithExplicitRollback()
    {
        var normal = KokoUiStartupPolicy.Resolve(Array.Empty<string>(), null, null);
        AssertEqual(KokoUiMode.Web, normal.Mode, "new installations must start the web shell");

        var environment = KokoUiStartupPolicy.Resolve(Array.Empty<string>(), "legacy", "web");
        AssertEqual(KokoUiMode.LegacyWpf, environment.Mode, "environment rollback must override settings");

        var argument = KokoUiStartupPolicy.Resolve(new[] { "--web-shell", "--legacy-wpf" }, "web", "web");
        AssertEqual(KokoUiMode.Web, argument.Mode, "the first explicit UI argument must win deterministically");

        AssertEqual("web", KokoUiStartupPolicy.NormalizeConfiguredMode("nonsense"),
            "invalid persisted modes must recover to the supported web shell");
    }

    private static void WebShellDevelopmentUrlAllowsLoopbackOnly()
    {
        var method = typeof(KokonoeAssistant.Windows.ShellWindow).GetMethod(
            "ResolveDevelopmentUri",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Shell development URL policy was not found.");

        Uri? Resolve(string? value) => method.Invoke(null, new object?[] { value }) as Uri;

        AssertEqual("http://localhost:5173/", Resolve("http://localhost:5173")?.ToString(),
            "localhost Vite URL must be accepted");
        AssertEqual("https://127.0.0.1:5173/", Resolve("https://127.0.0.1:5173")?.ToString(),
            "loopback HTTPS URL must be accepted");
        AssertTrue(Resolve("https://example.com") == null, "remote development origins must be rejected");
        AssertTrue(Resolve("file:///C:/temp/index.html") == null, "file development origins must be rejected");
        AssertTrue(Resolve("not-a-url") == null, "malformed development URLs must be rejected");
    }

    private static void WebShellKeepsDiagnosticsPageAfterCoreStartup()
    {
        var method = typeof(KokonoeAssistant.Windows.ShellWindow).GetMethod(
            "ShouldUseLegacyFallback",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Shell fallback policy was not found.");

        bool Should(Exception error, bool coreReady) => (bool)(method.Invoke(null, new object[] { error, coreReady }) ?? false);

        AssertTrue(Should(new InvalidOperationException("core missing"), coreReady: false),
            "missing WebView2 core must still fall back to legacy WPF");
        AssertTrue(!Should(new FileNotFoundException("frontend missing"), coreReady: true),
            "asset or bridge failures after WebView2 startup must stay in Web shell diagnostics");
        AssertTrue(Should(new DllNotFoundException("loader missing"), coreReady: true),
            "loader/runtime failures must keep the legacy escape hatch");
    }

    private static void WebChatBridgeStreamsCorrelatedChunks()
    {
        // chat.send now runs its pipeline on a detached background task, so the
        // publish callback can fire concurrently with this thread's reads below.
        var envelopes = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var bridge = new KokoWebBridgeService(envelopes.Enqueue);
        using var chat = new KokoWebChatBridgeService(
            bridge,
            (text, context, chunk, ct) =>
            {
                chunk("Hel");
                chunk("lo");
                return Task.FromResult<string?>("Hello");
            },
            (text, context, ct) => Task.FromResult("fallback"));

        bridge.HandleMessageAsync("""{"type":"request","id":"chat-1","method":"chat.send","payload":{"streamId":"stream-1","text":"hello"}}""")
            .GetAwaiter().GetResult();
        // chat.send acknowledges immediately and runs the actual turn on a detached
        // background task now - wait for it to actually publish before asserting.
        AssertTrue(System.Threading.SpinWait.SpinUntil(
            () => envelopes.Any(e => e.Contains("\"chat.completed\"")), TimeSpan.FromSeconds(5)),
            "chat pipeline must publish chat.completed");

        var parsed = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse).ToList();
        var chunks = parsed.Where(x => x["type"]?.ToString() == "event" && x["channel"]?.ToString() == "chat.chunk").ToList();
        AssertEqual(2, chunks.Count, "streaming chat must publish separate chunks");
        AssertEqual("Hel", chunks[0]["payload"]?["chunk"]?.ToString(), "first chunk must be preserved");
        AssertEqual("1", chunks[1]["payload"]?["sequence"]?.ToString(), "chunk sequence must increment");
        AssertTrue(parsed.Any(x => x["channel"]?.ToString() == "chat.completed" && x["payload"]?["streamed"]?.Value<bool>() == true),
            "streamed chat must publish completion state");
    }

    private static void WebChatBridgeResetsPartialStreamBeforeFallback()
    {
        var envelopes = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var bridge = new KokoWebBridgeService(envelopes.Enqueue);
        using var chat = new KokoWebChatBridgeService(
            bridge,
            (text, context, chunk, ct) =>
            {
                chunk("partial");
                return Task.FromResult<string?>(null);
            },
            (text, context, ct) => Task.FromResult("tool result"));

        bridge.HandleMessageAsync("""{"type":"request","id":"chat-2","method":"chat.send","payload":{"streamId":"stream-2","text":"use a tool"}}""")
            .GetAwaiter().GetResult();
        AssertTrue(System.Threading.SpinWait.SpinUntil(
            () => envelopes.Any(e => e.Contains("\"chat.completed\"")), TimeSpan.FromSeconds(5)),
            "chat pipeline must publish chat.completed");

        var parsed = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse).ToList();
        var resetIndex = parsed.FindIndex(x => x["channel"]?.ToString() == "chat.reset");
        var completeIndex = parsed.FindIndex(x => x["channel"]?.ToString() == "chat.completed");
        AssertTrue(resetIndex >= 0 && completeIndex > resetIndex, "fallback must reset partial stream before completion");
        AssertEqual("tool result", parsed[completeIndex]["payload"]?["reply"]?.ToString(), "fallback reply must be authoritative");
    }

    private static void WebChatBridgeStreamsToolFallbackAfterReset()
    {
        var envelopes = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var bridge = new KokoWebBridgeService(envelopes.Enqueue);
        using var chat = new KokoWebChatBridgeService(
            bridge,
            (text, context, chunk, ct) =>
            {
                chunk("discard me");
                return Task.FromResult<string?>(null);
            },
            (text, context, chunk, ct) =>
            {
                chunk("tool ");
                chunk("done");
                return Task.FromResult("tool done");
            });

        bridge.HandleMessageAsync("""{"type":"request","id":"chat-3","method":"chat.send","payload":{"streamId":"stream-3","text":"use a tool"}}""")
            .GetAwaiter().GetResult();
        AssertTrue(System.Threading.SpinWait.SpinUntil(
            () => envelopes.Any(e => e.Contains("\"chat.completed\"")), TimeSpan.FromSeconds(5)),
            "chat pipeline must publish chat.completed");

        var parsed = envelopes.Select(JObject.Parse).ToList();
        var resetIndex = parsed.FindIndex(x => x["channel"]?.ToString() == "chat.reset");
        var fallbackChunks = parsed
            .Skip(resetIndex + 1)
            .Where(x => x["channel"]?.ToString() == "chat.chunk")
            .Select(x => x["payload"]?["chunk"]?.ToString())
            .ToArray();
        AssertTrue(resetIndex >= 0, "tool fallback must reset speculative stream output");
        AssertEqual("tool done", string.Concat(fallbackChunks), "tool final response must continue as visible chunks");
        AssertTrue(parsed.Any(x => x["channel"]?.ToString() == "chat.completed" &&
                                   x["payload"]?["streamed"]?.Value<bool>() == true),
            "completion must report that the authoritative tool result was streamed");
    }

    private static void OpenAiStreamParserJoinsContentChunks()
    {
        const string sse = "data: {\"choices\":[{\"delta\":{\"content\":\"Hel\"}}]}\n\n" +
                           "data: {\"choices\":[{\"delta\":{\"content\":\"lo\"}}]}\n\n" +
                           "data: [DONE]\n\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(sse));
        var chunks = new List<string>();
        var parsed = KokoOpenAiStreamParser.ReadTextAsync(stream, chunks.Add).GetAwaiter().GetResult();

        AssertEqual("Hello", parsed.Text, "SSE parser must join delta content");
        AssertEqual("Hello", string.Concat(chunks), "SSE parser must forward every visible chunk");
        AssertTrue(!parsed.ToolCallsDetected, "plain text stream must not be marked as a tool call");
    }

    private static void LlmStreamingPolicyLimitsToolFinalStreaming()
    {
        // Used to fire the instant completedRounds > 0, forcing interactive chat to stop and
        // summarize after a single batch of tool calls even mid multi-step task. Now only
        // fires once toolRoundsCompleted is near the round loop's own hard cap (default 22,
        // raised from 6 after a real "create 20 files" task showed the model needs close to
        // one round per file), so a task that genuinely needs many rounds of real tool calls
        // gets to use them instead of being cut off after the first round that happened to
        // succeed. Tests pass an explicit threshold so they don't depend on whatever the
        // current default happens to be.
        AssertTrue(!KokoLlmStreamingPolicy.ShouldStreamFinalAfterTools(true, 1, false, false, maxToolRounds: 6),
            "one completed tool round must not yet force a stop-and-summarize turn");
        AssertTrue(!KokoLlmStreamingPolicy.ShouldStreamFinalAfterTools(true, 5, false, false, maxToolRounds: 6),
            "still under the round cap - more tool rounds must remain available");
        AssertTrue(KokoLlmStreamingPolicy.ShouldStreamFinalAfterTools(true, 6, false, false, maxToolRounds: 6),
            "at the round cap, the final round must stream a text-only wrap-up");
        AssertTrue(!KokoLlmStreamingPolicy.ShouldStreamFinalAfterTools(false, 6, false, false, maxToolRounds: 6),
            "background callers without a chunk sink must remain atomic");
        AssertTrue(!KokoLlmStreamingPolicy.ShouldStreamFinalAfterTools(true, 0, false, false, maxToolRounds: 6),
            "initial tool decision must remain atomic");
        AssertTrue(!KokoLlmStreamingPolicy.ShouldStreamFinalAfterTools(true, 6, true, false, maxToolRounds: 6),
            "vision responses must remain atomic");
        AssertTrue(!KokoLlmStreamingPolicy.ShouldStreamFinalAfterTools(true, 6, false, true, maxToolRounds: 6),
            "Claude response framing is not OpenAI SSE and must remain atomic");
        AssertTrue(KokoLlmStreamingPolicy.ShouldStreamFinalAfterTools(true, 22, false, false),
            "default threshold (22) must match the round loop's own hard cap");
        AssertTrue(!KokoLlmStreamingPolicy.ShouldStreamFinalAfterTools(true, 21, false, false),
            "one round under the default threshold must still allow more tool rounds");
    }

    private static void WebChatBridgePublishesProactiveBrainMessages()
    {
        var envelopes = new List<string>();
        using var bridge = new KokoWebBridgeService(envelopes.Add);
        using var chat = new KokoWebChatBridgeService(
            bridge,
            (text, context, chunk, ct) => Task.FromResult<string?>("unused"),
            (text, context, ct) => Task.FromResult("unused"));

        chat.PublishExternalMessage("assistant", "Background observation");

        var proactive = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse)
            .Single(x => x["channel"]?.ToString() == "chat.external");
        AssertEqual("assistant", proactive["payload"]?["role"]?.ToString(),
            "proactive event must retain the assistant role");
        AssertEqual("Background observation", proactive["payload"]?["content"]?.ToString(),
            "proactive event must carry the actual brain message");
    }

    private static void WebAgentBridgeReturnsCamelCaseSnapshot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        var envelopes = new List<string>();
        var tasks = new KokoAgentTaskService(dir) { AutoStartOnAdd = false, MaxParallel = 3 };
        tasks.AddTask("Inspect the current workspace", priority: 7);
        using var bridge = new KokoWebBridgeService(envelopes.Add);
        using var agent = new KokoWebAgentBridgeService(bridge, tasks);

        bridge.HandleMessageAsync("""{"type":"request","id":"agent-1","method":"agent.snapshot","payload":null}""")
            .GetAwaiter().GetResult();

        var response = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse)
            .Single(x => x["type"]?.ToString() == "response");
        AssertEqual("3", response["result"]?["maxParallel"]?.ToString(), "snapshot must expose camel-case runtime fields");
        AssertEqual("1", response["result"]?["tasks"]?.Count().ToString(), "snapshot must include current tasks");
        AssertEqual("Inspect the current workspace", response["result"]?["tasks"]?[0]?["objective"]?.ToString(),
            "task objective must cross the bridge");
    }

    private static void WebAgentBridgeExposesTaskEvidenceFields()
    {
        var dir = TempDir();
        try
        {
            var envelopes = new List<string>();
            var tasks = new KokoAgentTaskService(dir) { AutoStartOnAdd = false };
            tasks.AddTask("Inspect evidence plumbing", priority: 6);
            using var bridge = new KokoWebBridgeService(envelopes.Add);
            using var agent = new KokoWebAgentBridgeService(bridge, tasks);

            bridge.HandleMessageAsync("""{"type":"request","id":"agent-evidence","method":"agent.snapshot","payload":null}""")
                .GetAwaiter().GetResult();

            var response = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse)
                .Single(x => x["id"]?.ToString() == "agent-evidence");
            var task = response["result"]?["tasks"]?[0] as Newtonsoft.Json.Linq.JObject;
            var step = task?["steps"]?[0] as Newtonsoft.Json.Linq.JObject;
            AssertTrue(task?.Property("completionNotice") != null, "task payload must expose completion notice for WebView details");
            AssertTrue(task?.Property("nextQuestion") != null, "task payload must expose next question for WebView details");
            AssertTrue(step?.Property("result") != null, "step payload must expose execution result evidence");
            AssertTrue(step?.Property("error") != null, "step payload must expose execution error evidence");
            AssertTrue(step?.Property("startedAt") != null, "step payload must expose start timestamp");
            AssertTrue(step?.Property("finishedAt") != null, "step payload must expose finish timestamp");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void WebAgentBridgeCreatesTaskFromShell()
    {
        var dir = TempDir();
        try
        {
            var envelopes = new List<string>();
            var tasks = new KokoAgentTaskService(dir) { AutoStartOnAdd = false };
            using var bridge = new KokoWebBridgeService(envelopes.Add);
            using var agent = new KokoWebAgentBridgeService(bridge, tasks);

            bridge.HandleMessageAsync("""{"type":"request","id":"agent-start","method":"agent.start","payload":{"objective":"Inspect bridge contract","priority":8,"start":false}}""")
                .GetAwaiter().GetResult();

            var response = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse)
                .Single(x => x["id"]?.ToString() == "agent-start");
            AssertTrue(string.IsNullOrWhiteSpace(response["error"]?.ToString()), "agent.start must return a normal response");
            AssertTrue(!string.IsNullOrWhiteSpace(response["result"]?["taskId"]?.ToString()), "agent.start must return task id");
            AssertTrue(response["result"]?["started"]?.Value<bool>() == false, "start=false must keep runner paused");
            AssertTrue(tasks.GetSnapshot().Tasks.Any(task => task.Objective == "Inspect bridge contract" && task.Priority == 8),
                "web handler must create a real task in KokoAgentTaskService");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void WebAgentBridgeCreatesBatchFromShell()
    {
        var dir = TempDir();
        try
        {
            var envelopes = new List<string>();
            var tasks = new KokoAgentTaskService(dir) { AutoStartOnAdd = false };
            using var bridge = new KokoWebBridgeService(envelopes.Add);
            using var agent = new KokoWebAgentBridgeService(bridge, tasks);

            var payload = new Newtonsoft.Json.Linq.JObject
            {
                ["type"] = "request",
                ["id"] = "agent-batch",
                ["method"] = "agent.start_many",
                ["payload"] = new Newtonsoft.Json.Linq.JObject
                {
                    ["objective"] = "- Inspect runtime lanes\n- Verify task counters",
                    ["priority"] = 7,
                    ["start"] = false
                }
            };
            bridge.HandleMessageAsync(payload.ToString(Newtonsoft.Json.Formatting.None))
                .GetAwaiter().GetResult();

            var response = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse)
                .Single(x => x["id"]?.ToString() == "agent-batch");
            AssertTrue(string.IsNullOrWhiteSpace(response["error"]?.ToString()), "agent.start_many must return a normal response");
            AssertEqual("2", response["result"]?["count"]?.ToString(), "agent.start_many should report created count");
            AssertTrue(response["result"]?["started"]?.Value<bool>() == false, "start=false must keep batch runner paused");
            AssertTrue(tasks.GetSnapshot().Tasks.Any(task => task.Objective == "Inspect runtime lanes" && task.Priority == 7),
                "batch handler must create first objective");
            AssertTrue(tasks.GetSnapshot().Tasks.Any(task => task.Objective == "Verify task counters" && task.Priority == 7),
                "batch handler must create second objective");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void WebAgentBridgeCancelsTaskFromShell()
    {
        var dir = TempDir();
        try
        {
            var envelopes = new List<string>();
            var tasks = new KokoAgentTaskService(dir) { AutoStartOnAdd = false };
            var task = tasks.AddTask("Cancel this from WebView", priority: 5);
            using var bridge = new KokoWebBridgeService(envelopes.Add);
            using var agent = new KokoWebAgentBridgeService(bridge, tasks);

            var request = new Newtonsoft.Json.Linq.JObject
            {
                ["type"] = "request",
                ["id"] = "agent-cancel",
                ["method"] = "agent.cancel",
                ["payload"] = new Newtonsoft.Json.Linq.JObject { ["taskId"] = task.Id }
            };

            bridge.HandleMessageAsync(request.ToString(Newtonsoft.Json.Formatting.None))
                .GetAwaiter().GetResult();

            var response = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse)
                .Single(x => x["id"]?.ToString() == "agent-cancel");
            AssertTrue(string.IsNullOrWhiteSpace(response["error"]?.ToString()), "agent.cancel must return a normal response");
            AssertTrue(response["result"]?["canceled"]?.Value<bool>() == true, "agent.cancel should report cancellation");
            AssertTrue(response["result"]?["snapshot"]?["tasks"]?
                    .Any(x => x?["id"]?.ToString() == task.Id && x?["status"]?.ToString() == "Canceled") == true,
                "agent.cancel response snapshot must show the canceled task");
            AssertTrue(tasks.GetSnapshot().Tasks.Any(x => x.Id == task.Id && x.Status == KokoAgentTaskStatus.Canceled),
                "agent.cancel must cancel the real service task");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void WebAgentBridgeControlsRunner()
    {
        var dir = TempDir();
        try
        {
            var envelopes = new List<string>();
            var tasks = new KokoAgentTaskService(dir) { AutoStartOnAdd = false };
            using var bridge = new KokoWebBridgeService(envelopes.Add);
            using var agent = new KokoWebAgentBridgeService(bridge, tasks);

            bridge.HandleMessageAsync("""{"type":"request","id":"runner-start","method":"agent.runner.start","payload":null}""")
                .GetAwaiter().GetResult();
            bridge.HandleMessageAsync("""{"type":"request","id":"runner-stop","method":"agent.runner.stop","payload":null}""")
                .GetAwaiter().GetResult();

            var parsed = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse).ToList();
            var started = parsed.Single(x => x["id"]?.ToString() == "runner-start");
            var stopped = parsed.Single(x => x["id"]?.ToString() == "runner-stop");

            AssertTrue(started["result"]?["active"]?.Value<bool>() == true, "agent.runner.start must activate the runner");
            AssertTrue(started["result"]?["snapshot"]?["runnerActive"]?.Value<bool>() == true,
                "runner start snapshot must expose active state");
            AssertTrue(stopped["result"]?["active"]?.Value<bool>() == false, "agent.runner.stop must pause the runner");
            AssertTrue(tasks.IsRunnerActive == false, "runner stop must update the real task service");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void WebAgentBridgePublishesLiveActivity()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        var envelopes = new List<string>();
        var tasks = new KokoAgentTaskService(dir) { AutoStartOnAdd = false };
        using var bridge = new KokoWebBridgeService(envelopes.Add);
        var agent = new KokoWebAgentBridgeService(bridge, tasks);

        tasks.AddTask("Create a live activity event");
        var activityEvent = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse)
            .LastOrDefault(x => x["channel"]?.ToString() == "agent.activity");
        AssertTrue(activityEvent != null, "task activity must be pushed without polling");
        AssertEqual("plan", activityEvent!["payload"]?["activity"]?["phase"]?.ToString(),
            "activity event must carry the current phase");
        AssertEqual("1", activityEvent["payload"]?["snapshot"]?["tasks"]?.Count().ToString(),
            "activity event must carry a fresh board snapshot");

        var beforeDispose = envelopes.Count;
        agent.Dispose();
        tasks.AddTask("Do not publish after dispose");
        AssertEqual(beforeDispose, envelopes.Count, "disposed bridge must detach task event handlers");
    }

    private static void WebVaultBridgeCachesAndRefreshesStatus()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "web-vault-" + Guid.NewGuid().ToString("N"));
        var obsidian = new ObsidianMcpService(dir);
        obsidian.WriteNote("Projects/First.md", "# First\n\nInitial note.");
        var envelopes = new List<string>();
        using var bridge = new KokoWebBridgeService(envelopes.Add);
        using var vault = new KokoWebVaultBridgeService(bridge, obsidian);

        bridge.HandleMessageAsync("""{"type":"request","id":"vault-1","method":"vault.status","payload":null}""")
            .GetAwaiter().GetResult();
        var first = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse)
            .Single(x => x["id"]?.ToString() == "vault-1");
        AssertEqual("1", first["result"]?["noteCount"]?.ToString(), "initial vault status must count notes");
        AssertEqual("1", first["result"]?["folderCount"]?.ToString(), "initial vault status must count folders");
        AssertTrue(first["result"]?["available"]?.Value<bool>() == true, "existing vault must report online");
        AssertTrue(first["result"]?["recentNotes"]?[0]?["path"]?.ToString() == "Projects/First.md",
            "vault status must expose recent relative paths");

        obsidian.WriteNote("Second.md", "# Second\n\nNew note.");
        bridge.HandleMessageAsync("""{"type":"request","id":"vault-2","method":"vault.status","payload":null}""")
            .GetAwaiter().GetResult();
        var cached = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse)
            .Single(x => x["id"]?.ToString() == "vault-2");
        AssertEqual("1", cached["result"]?["noteCount"]?.ToString(), "ordinary status must reuse the short UI cache");

        bridge.HandleMessageAsync("""{"type":"request","id":"vault-3","method":"vault.refresh","payload":null}""")
            .GetAwaiter().GetResult();
        var refreshed = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse)
            .Single(x => x["id"]?.ToString() == "vault-3");
        AssertEqual("2", refreshed["result"]?["noteCount"]?.ToString(), "forced refresh must rescan changed notes");
        AssertTrue(envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse)
                .Any(x => x["channel"]?.ToString() == "vault.status" && x["payload"]?["noteCount"]?.ToString() == "2"),
            "forced refresh must publish the updated vault status event");
    }

    private static void WebSettingsBridgeMasksAndPreservesSecrets()
    {
        var settings = new AppSettings
        {
            TelegramToken = "telegram-secret-value",
            TgApiId = 12345,
            TgApiHash = "telegram-api-hash",
            OpenAiApiKey = "openai-secret-value",
            ClaudeApiKey = "claude-secret-value",
            OllamaApiKey = "ollama-secret-value",
            ProactiveAutonomyLevel = 1,
            WearBridgeEnabled = true
        };
        AppSettings? saved = null;
        var envelopes = new List<string>();
        using var bridge = new KokoWebBridgeService(envelopes.Add);
        using var webSettings = new KokoWebSettingsBridgeService(
            bridge,
            () => settings,
            value => { saved = value; return null; });

        bridge.HandleMessageAsync("""{"type":"request","id":"settings-1","method":"settings.get","payload":null}""")
            .GetAwaiter().GetResult();
        var getResponse = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse)
            .Single(x => x["id"]?.ToString() == "settings-1");
        var serialized = getResponse.ToString(Newtonsoft.Json.Formatting.None);
        AssertTrue(!serialized.Contains("telegram-secret-value", StringComparison.Ordinal), "settings snapshot must not expose Telegram token");
        AssertTrue(!serialized.Contains("openai-secret-value", StringComparison.Ordinal), "settings snapshot must not expose API keys");
        AssertTrue(getResponse["result"]?["credentials"]?["telegramBot"]?.Value<bool>() == true,
            "settings snapshot should expose credential presence only");

        bridge.HandleMessageAsync("""{"type":"request","id":"settings-2","method":"settings.update","payload":{"proactiveAutonomyLevel":3,"wearBridgeEnabled":false,"matrixColor":"#12abef"}}""")
            .GetAwaiter().GetResult();
        AssertTrue(saved != null, "valid settings update must persist");
        AssertEqual("telegram-secret-value", saved!.TelegramToken, "merge update must preserve Telegram token");
        AssertEqual("telegram-api-hash", saved.TgApiHash, "merge update must preserve MTProto credentials");
        AssertEqual("openai-secret-value", saved.OpenAiApiKey, "merge update must preserve OpenAI key");
        AssertEqual(3, saved.ProactiveAutonomyLevel, "allowlisted setting must update");
        AssertTrue(!saved.WearBridgeEnabled, "wear bridge setting must update");
        AssertEqual("#12ABEF", saved.MatrixColor, "accent color must normalize");
        var updateResponse = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse)
            .Single(x => x["id"]?.ToString() == "settings-2");
        AssertTrue(updateResponse["result"]?["restartRequired"]?.Value<bool>() == true,
            "device service change must report restart requirement");
    }

    private static void WebSettingsBridgeRejectsInvalidValues()
    {
        var saves = 0;
        var envelopes = new List<string>();
        using var bridge = new KokoWebBridgeService(envelopes.Add);
        using var webSettings = new KokoWebSettingsBridgeService(
            bridge,
            () => new AppSettings(),
            value => { saves++; return null; });

        bridge.HandleMessageAsync("""{"type":"request","id":"settings-bad","method":"settings.update","payload":{"proactiveAutonomyLevel":99}}""")
            .GetAwaiter().GetResult();
        var response = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse)
            .Single(x => x["id"]?.ToString() == "settings-bad");
        AssertTrue(response["error"]?.ToString().Contains("between 0 and 3", StringComparison.OrdinalIgnoreCase) == true,
            "invalid setting must return a structured validation error");
        AssertEqual(0, saves, "invalid settings must not touch persistence");
    }

    private static void WebTelegramBridgePublishesSafeLiveStatus()
    {
        var settings = new AppSettings
        {
            TelegramEnabled = true,
            TelegramToken = "bot-token-must-not-leak",
            TelegramChatId = 99887766,
            TgUserEnabled = true,
            TgApiId = 12345,
            TgApiHash = "api-hash-must-not-leak",
            TgPhone = "+49123456789"
        };
        var runtime = new KokoTelegramRuntimeStatusService();
        var envelopes = new List<string>();
        using var bridge = new KokoWebBridgeService(envelopes.Add);
        var telegram = new KokoWebTelegramBridgeService(bridge, runtime, () => settings);

        bridge.HandleMessageAsync("""{"type":"request","id":"telegram-1","method":"telegram.status","payload":null}""")
            .GetAwaiter().GetResult();
        var response = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse)
            .Single(x => x["id"]?.ToString() == "telegram-1");
        var json = response.ToString(Newtonsoft.Json.Formatting.None);
        AssertTrue(response["result"]?["bot"]?["configured"]?.Value<bool>() == true, "bot credentials should report configured");
        AssertTrue(response["result"]?["user"]?["configured"]?.Value<bool>() == true, "user client credentials should report configured");
        AssertTrue(!json.Contains(settings.TelegramToken, StringComparison.Ordinal), "Telegram token must not cross web bridge");
        AssertTrue(!json.Contains(settings.TgApiHash, StringComparison.Ordinal), "Telegram API hash must not cross web bridge");
        AssertTrue(!json.Contains(settings.TgPhone, StringComparison.Ordinal), "Telegram phone must not cross web bridge");
        AssertTrue(!json.Contains(settings.TelegramChatId.ToString(), StringComparison.Ordinal), "Telegram chat id must not cross web bridge");

        runtime.MarkBotState("listening");
        runtime.RecordBotActivity("incoming");
        runtime.MarkUserState("connected", "Kokonoe Account");
        var latest = envelopes.Select(Newtonsoft.Json.Linq.JObject.Parse)
            .Last(x => x["channel"]?.ToString() == "telegram.status");
        AssertEqual("listening", latest["payload"]?["bot"]?["state"]?.ToString(), "bot state must update live");
        AssertEqual("incoming", latest["payload"]?["bot"]?["lastActivity"]?.ToString(), "bot activity must update live");
        AssertEqual("connected", latest["payload"]?["user"]?["state"]?.ToString(), "user client state must update live");

        var beforeDispose = envelopes.Count;
        telegram.Dispose();
        runtime.RecordUserError("network unavailable");
        AssertEqual(beforeDispose, envelopes.Count, "disposed Telegram bridge must detach status events");
    }

    private static void ActionDirectiveRouterHandlesInflectedTargets()
    {
        var localTargets = new[]
        {
            "створи щось на робочому столі",
            "залиш сюрприз на рабочем столе",
            "запиши файл у папці завантажень",
            "поклади звіт на диску",
            "збережи нотатку на комп'ютері",
            "оставь документ в каталоге"
        };
        var artifactTargets = new[]
        {
            "напиши щось у документі",
            "залиш результат у нотатці",
            "збережи дані у файлі",
            "создай запись в документе",
            "залиш коротко у звіті",
            "сохрани результат в заметке"
        };
        var projectTargets = new[]
        {
            "пофікси помилку у проєкті",
            "дороби логіку в репозитории",
            "покращ код у репозиторії",
            "виправ відступи в інтерфейсі",
            "прожени перевірки у тестах",
            "онови віджет на годиннику"
        };
        var autonomyCues = new[]
        {
            "на власний розсуд покращ код у проєкті",
            "без мого втручання дороби інтерфейс",
            "без додаткових питань онови тести",
            "повністю автономно виправ код",
            "по-своєму покращ проєкт",
            "на свое усмотрение доработай проект"
        };

        AssertRoutes(localTargets, KokoActionDirectiveRoute.LocalArtifact, "local target inflection");
        AssertRoutes(artifactTargets, KokoActionDirectiveRoute.LocalArtifact, "artifact target inflection");
        AssertRoutes(projectTargets, KokoActionDirectiveRoute.AgentTask, "project target inflection");
        AssertRoutes(autonomyCues, KokoActionDirectiveRoute.AgentTask, "autonomy cue inflection");

        static void AssertRoutes(IEnumerable<string> phrases, KokoActionDirectiveRoute expected, string context)
        {
            foreach (var phrase in phrases)
            {
                var directive = KokoActionDirectiveRouter.Analyze(phrase);
                AssertEqual(expected, directive.Route, $"{context}: {phrase}; reason={directive.Reason}");
                AssertTrue(!directive.Reason.Contains("generic", StringComparison.OrdinalIgnoreCase),
                    $"{context} must not use generic fallback: {phrase}");
            }
        }
    }

    private static void SystemOverlordCreatesRealSurpriseNote()
    {
        using var ctx = TestContext.Create();
        var root = Path.Combine(ctx.TestDir, "ScanRoot");
        var desktop = Path.Combine(ctx.TestDir, "Desktop");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(desktop);

        var interesting = Path.Combine(root, "project-idea.md");
        File.WriteAllText(interesting, "real local artifact for system overlord");

        var service = new KokoSystemOverlordService(ctx.TestDir);
        var result = service.CreateSurpriseNoteAsync(
            "пошукай щось цікаве на компі і залиш файл сюрприз",
            rootsOverride: new[] { root },
            desktopOverride: desktop).GetAwaiter().GetResult();

        AssertTrue(result.Success, "surprise route should succeed");
        AssertTrue(File.Exists(result.FilePath), "surprise file should be created for real");
        AssertTrue(result.FilePath.StartsWith(desktop, StringComparison.OrdinalIgnoreCase), "surprise file should use the actual desktop target");
        AssertTrue(!result.FilePath.Contains(@"\Users\User\", StringComparison.OrdinalIgnoreCase), "surprise path must not use fake C:\\Users\\User");
        var content = File.ReadAllText(result.FilePath);
        AssertTrue(content.Contains("project-idea.md", StringComparison.OrdinalIgnoreCase), "note should include scanned real file path");
    }

    private static void CollectiveMindBuildsAgentDebateFrame()
    {
        using var ctx = TestContext.Create();
        var state = new KokoInternalState
        {
            LastKnownUserActivity = "chatting",
            LastScreenAwarenessMode = "chat",
            LastSomaticLabel = "stable",
            LastSomaticStrain = 0.20,
            LastSomaticAt = DateTime.Now,
            LastLivingConversationMode = "social"
        };
        var service = new KokoCollectiveMindService();
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "привіт, поговоримо про нас", Timestamp = DateTime.Now }
        };
        var frame = service.Build("привіт, поговоримо про нас", state, messages, Array.Empty<BlackboardEvent>(), "test", DateTime.Now);

        AssertTrue(frame.PromptBlock.Contains("COLLECTIVE MIND BLACKBOARD", StringComparison.OrdinalIgnoreCase), "collective prompt should be present");
        AssertTrue(frame.PromptBlock.Contains("scientist.proposal", StringComparison.OrdinalIgnoreCase), "collective prompt should include scientist proposal");
        AssertTrue(frame.PromptBlock.Contains("persona_guard.critique", StringComparison.OrdinalIgnoreCase), "collective prompt should include persona guard critique");
        AssertTrue(frame.Decision.Contains("living conversation", StringComparison.OrdinalIgnoreCase), "social bid should produce living conversation decision");
    }

    private static void UnifiedContextIncludesCollectiveMind()
    {
        using var ctx = TestContext.Create();
        using var health = new HealthService(ctx.TestDir);
        using var brain = new KokoBrainEngine(new LlmService(), health, new ObsidianMcpService(ctx.TestDir), ctx.Chat, ctx.TestDir);

        brain.ProcessUserMessage("fix build and run tests");
        var context = brain.BuildUnifiedExternalContext("telegram", "fix build and run tests");

        AssertTrue(context.Contains("COLLECTIVE MIND BLACKBOARD", StringComparison.OrdinalIgnoreCase), "unified context should include collective mind prompt");
        AssertTrue(context.Contains("scientist.proposal", StringComparison.OrdinalIgnoreCase), "unified context should include agent proposal");
        AssertTrue(brain.State.LastCollectiveMindDecision.Contains("operator", StringComparison.OrdinalIgnoreCase), "brain state should cache collective decision");
    }

    private static void NarrativeContinueStaysConversational()
    {
        using var ctx = TestContext.Create();
        using var health = new HealthService(ctx.TestDir);
        using var brain = new KokoBrainEngine(new LlmService(), health, new ObsidianMcpService(ctx.TestDir), ctx.Chat, ctx.TestDir);
        brain.State.ProactiveMutedUntil = DateTime.Now.AddHours(2);

        var handled = brain.TryApplyUserControlCommand("продовжуй", out var reply);

        AssertTrue(!handled, "bare continue should not be stolen by proactive resume control");
        AssertTrue(string.IsNullOrWhiteSpace(reply), "bare continue should go to the conversational LLM path");
        AssertTrue(brain.State.ProactiveMutedUntil > DateTime.Now, "bare continue should not clear proactive mute state");
    }

    private static void NarrativeContinueContextIgnoresInternalStatus()
    {
        using var ctx = TestContext.Create();
        using var health = new HealthService(ctx.TestDir);
        using var brain = new KokoBrainEngine(new LlmService(), health, new ObsidianMcpService(ctx.TestDir), ctx.Chat, ctx.TestDir);
        var now = DateTime.Now;
        ctx.Chat.InsertMessage(new ChatRepository.ChatMessage
        {
            Role = "user",
            Content = "хочу просто поговорити про нас",
            Timestamp = now.AddMinutes(-3)
        });
        ctx.Chat.InsertMessage(new ChatRepository.ChatMessage
        {
            Role = "assistant",
            Author = "Kokonoe",
            Content = "Не вдавай, що це лабораторний звіт. Говори нормально.",
            Timestamp = now.AddMinutes(-2)
        });
        ctx.Chat.InsertMessage(new ChatRepository.ChatMessage
        {
            Role = "assistant",
            Author = "Kokonoe",
            Content = "[ACTION:research] researched 4 topics; scheduler: 9d69553ed6",
            Timestamp = now.AddMinutes(-1)
        });

        var context = brain.BuildUnifiedExternalContext("telegram", "продовжуй");

        AssertTrue(context.Contains("CONTINUATION OVERRIDE", StringComparison.OrdinalIgnoreCase), "continue should add an explicit thread continuation block");
        AssertTrue(context.Contains("хочу просто поговорити", StringComparison.OrdinalIgnoreCase), "continuation block should carry the recent human thread");
        AssertTrue(!context.Contains("[ACTION:research]", StringComparison.OrdinalIgnoreCase), "continuation block should ignore internal status reports");
        AssertTrue(context.Contains("Do not mention autoping", StringComparison.OrdinalIgnoreCase), "continuation block should forbid technical status leakage");
    }

    private static void ShortAcknowledgementResolvesStaleIntent()
    {
        using var ctx = TestContext.Create();
        using var health = new HealthService(ctx.TestDir);
        using var brain = new KokoBrainEngine(new LlmService(), health, new ObsidianMcpService(ctx.TestDir), ctx.Chat, ctx.TestDir);
        brain.State.PendingTriggers.Add(new ReactiveTrigger
        {
            Type = "intent_followup",
            FireAt = DateTime.Now.AddMinutes(1),
            Context = "food followup"
        });
        brain.State.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "course",
            Summary = "course",
            CreatedAt = DateTime.Now.AddHours(-3),
            FollowUpAt = DateTime.Now.AddHours(-2),
            ExpectedUntil = DateTime.Now.AddHours(-1)
        });

        brain.ProcessUserMessage("всм");

        AssertTrue(brain.State.ShortTermIntents.All(i => i.ResolvedAt.HasValue), "short acknowledgement should close stale active intent");
        AssertTrue(!brain.State.PendingTriggers.Any(t => t.Type == "intent_followup"), "short acknowledgement should remove stale intent followup");
    }

    private static void ScreenAwarenessParsesVisionJson()
    {
        var service = new KokoScreenAwarenessService();
        var parsed = service.Parse("""
{
  "summary_uk": "відкритий редактор коду, користувач працює над проектом",
  "activity_uk": "active: змінився код",
  "should_comment": true,
  "comment_uk": "Ти нарешті дістався до коду. Не зламай його театрально.",
  "importance": 0.7
}
""");

        AssertTrue(parsed.ShouldComment, "vision JSON should preserve comment decision");
        AssertTrue(parsed.SummaryUk.Contains("редактор") || parsed.SummaryUk.Contains("код"), "summary should be parsed");
        AssertTrue(parsed.CommentUk.Contains("код"), "comment should be parsed");
        AssertTrue(parsed.Importance > 0.6, "importance should be parsed");
    }

    private static void ScreenAwarenessSuppressesRepeatedComments()
    {
        var service = new KokoScreenAwarenessService();
        var now = new DateTime(2026, 5, 9, 12, 0, 0);
        var analysis = new KokoScreenAwarenessAnalysis
        {
            ShouldComment = true,
            CommentUk = "Ти знову завис над тим самим кодом. Дуже несподівано.",
            Importance = 0.8
        };

        var cooldown = service.DecideComment(
            analysis,
            now,
            now.AddMinutes(-3),
            "",
            cooldownMinutes: 5,
            commentsEnabled: true);
        AssertTrue(!cooldown.ShouldSend, "screen comment should respect cooldown");

        var repeated = service.DecideComment(
            analysis,
            now,
            now.AddMinutes(-20),
            "Ти знову завис над тим самим кодом. Дуже несподівано.",
            cooldownMinutes: 5,
            commentsEnabled: true);
        AssertTrue(!repeated.ShouldSend, "screen comment should avoid repeating same line");
    }

    private static void ScreenAwarenessRedactsPrivateIdentifiers()
    {
        var service = new KokoScreenAwarenessService();
        var parsed = service.Parse("""
{
  "summary_uk": "відкрита сторінка акаунта test.user@example.com з ключем abcdefghijklmnopqrstuvwxyz123456",
  "activity_uk": "active",
  "should_comment": true,
  "comment_uk": "Ну так, сторінка test.user@example.com і токен abcdefghijklmnopqrstuvwxyz123456, геніально.",
  "importance": 0.8
}
""");

        AssertTrue(!parsed.SummaryUk.Contains("test.user@example.com"), "screen summary should redact email");
        AssertTrue(!parsed.CommentUk.Contains("test.user@example.com"), "screen comment should redact email");
        AssertTrue(!parsed.SummaryUk.Contains("abcdefghijklmnopqrstuvwxyz123456"), "screen summary should redact long token");
        AssertTrue(!parsed.CommentUk.Contains("abcdefghijklmnopqrstuvwxyz123456"), "screen comment should redact long token");
    }

    private static void ScreenAwarenessAllowsRarePassiveJab()
    {
        var service = new KokoScreenAwarenessService();
        var now = new DateTime(2026, 5, 12, 12, 0, 0);
        var analysis = new KokoScreenAwarenessAnalysis
        {
            SummaryUk = "telegram chat/profile, user is staring at the same list",
            ActivityUk = "same idle profile",
            ShouldComment = true,
            CommentUk = "Ти так і будеш вивчати цей профіль, чи нарешті зробиш щось корисне?",
            Importance = 0.75
        };

        var decision = service.DecideComment(
            analysis,
            now,
            now.AddHours(-2),
            "",
            cooldownMinutes: 30,
            commentsEnabled: true,
            screenChanged: false,
            isActive: false,
            activeWindowTitle: "Telegram");

        AssertTrue(decision.ShouldSend, "passive chat should allow a rare jab");
        AssertTrue(decision.CountsAsJab, "passive chat comment should be counted as a jab");
        AssertEqual("jab", decision.Kind, "passive chat should be classified as jab");
    }

    private static void ScreenAwarenessLetsJabBypassCommentCooldown()
    {
        var service = new KokoScreenAwarenessService();
        var now = new DateTime(2026, 5, 12, 12, 8, 0);
        var analysis = new KokoScreenAwarenessAnalysis
        {
            SummaryUk = "telegram chat/profile, same idle screen",
            ActivityUk = "same idle",
            ShouldComment = true,
            CommentUk = "Вісім хвилин дивитись в одну точку. Справді амбітний план.",
            Importance = 0.76
        };

        var decision = service.DecideComment(
            analysis,
            now,
            now.AddMinutes(-8),
            "Інший короткий коментар.",
            cooldownMinutes: 30,
            commentsEnabled: true,
            screenChanged: false,
            isActive: false,
            activeWindowTitle: "Telegram");

        AssertTrue(decision.ShouldSend, "jab should not be blocked by the general assist cooldown");
        AssertEqual("jab", decision.Kind, "idle passive screen should stay a jab");
    }

    private static void ScreenAwarenessAllowsMediumPassiveChatJab()
    {
        var service = new KokoScreenAwarenessService();
        var now = new DateTime(2026, 5, 12, 12, 9, 0);
        var analysis = new KokoScreenAwarenessAnalysis
        {
            SummaryUk = "telegram chat/profile, same idle screen",
            ActivityUk = "same idle profile",
            ScreenMode = "telegram",
            ShouldComment = true,
            CommentUk = "Still staring at the same chat. Revolutionary productivity.",
            Importance = 0.52
        };

        var decision = service.DecideComment(
            analysis,
            now,
            now.AddHours(-2),
            "",
            cooldownMinutes: 30,
            commentsEnabled: true,
            screenChanged: false,
            isActive: false,
            activeWindowTitle: "Telegram");

        AssertTrue(decision.ShouldSend, "medium-importance passive chat should allow a jab");
        AssertEqual("jab", decision.Kind, "medium passive chat should be a jab");
    }

    private static void ScreenAwarenessGameModeUsesActiveJabCooldown()
    {
        var service = new KokoScreenAwarenessService();
        var now = new DateTime(2026, 5, 15, 20, 0, 0);
        var analysis = new KokoScreenAwarenessAnalysis
        {
            SummaryUk = "активна гра, користувач у матчі",
            ActivityUk = "active gameplay",
            ScreenMode = "game",
            ShouldComment = true,
            CommentUk = "О, матч живий. Спробуй цього разу не воювати з інтерфейсом.",
            Importance = 0.58
        };

        var blocked = service.DecideComment(
            analysis,
            now,
            now.AddMinutes(-8),
            "Інший ігровий коментар.",
            cooldownMinutes: 10,
            commentsEnabled: true,
            screenChanged: true,
            isActive: true,
            activeWindowTitle: "Dota 2");

        var allowed = service.DecideComment(
            analysis,
            now,
            now.AddMinutes(-11),
            "Інший ігровий коментар.",
            cooldownMinutes: 10,
            commentsEnabled: true,
            screenChanged: true,
            isActive: true,
            activeWindowTitle: "Dota 2");

        AssertTrue(!blocked.ShouldSend, "game jab should respect its own cooldown");
        AssertEqual("game comment cooldown", blocked.Reason, "game cooldown should explain suppression");
        AssertTrue(allowed.ShouldSend, "game jab should send after game cooldown");
        AssertEqual("jab", allowed.Kind, "game comment should be a jab, not assist");
    }

    private static void ScreenAwarenessBlocksSensitiveScreens()
    {
        var service = new KokoScreenAwarenessService();
        var now = new DateTime(2026, 5, 12, 14, 0, 0);
        var analysis = new KokoScreenAwarenessAnalysis
        {
            SummaryUk = "account settings with token field",
            ActivityUk = "active",
            ShouldComment = true,
            CommentUk = "О, токени на екрані. Дуже розумний виставковий стенд.",
            Importance = 0.9
        };

        var decision = service.DecideComment(
            analysis,
            now,
            now.AddHours(-2),
            "",
            cooldownMinutes: 30,
            commentsEnabled: true,
            screenChanged: true,
            isActive: true,
            activeWindowTitle: "API key settings");

        AssertTrue(!decision.ShouldSend, "sensitive screens should never trigger Telegram comments");
        AssertEqual("sensitive screen", decision.Reason, "sensitive screen should have a clear suppression reason");
        AssertEqual("silence", decision.Kind, "sensitive screen should be classified as silence");
    }

    private static void ScreenAwarenessBuildsSituationContext()
    {
        var service = new KokoScreenAwarenessService();
        var parsed = service.Parse("""
{
  "summary_uk": "Visual Studio показує build error у KokonoeAssistant",
  "activity_uk": "active: користувач дебажить",
  "screen_mode": "coding",
  "current_task": "debugging Kokonoe screen awareness",
  "progress": "stuck",
  "blocker": "KokonoeAssistant.exe locked by running process",
  "recommended_behavior": "assist",
  "should_comment": true,
  "comment_uk": "Закрий запущений KokonoeAssistant перед build, генію. Файл сам себе не відпустить.",
  "importance": 0.9
}
""");
        var activity = new ActivityAnalyzer.ActivityState
        {
            IsActive = true,
            PixelDifferencePercentage = 4.2,
            ActiveWindowTitle = "Visual Studio"
        };

        var situation = service.BuildSituation(parsed, activity);
        var context = service.BuildCompactContext(parsed, activity, situation);
        var decision = service.DecideComment(
            parsed,
            new DateTime(2026, 5, 13, 22, 0, 0),
            DateTime.MinValue,
            "",
            cooldownMinutes: 30,
            commentsEnabled: true,
            screenChanged: true,
            isActive: true,
            activeWindowTitle: "Visual Studio",
            situation: situation);

        AssertEqual("debugging Kokonoe screen awareness", situation.CurrentTask, "situation should preserve current task");
        AssertEqual("stuck", situation.Progress, "situation should preserve progress");
        AssertEqual("assist", situation.RecommendedBehavior, "situation should preserve recommended behavior");
        AssertTrue(context.Contains("[SCREEN SITUATION]"), "compact context should include situation section");
        AssertTrue(context.Contains("KokonoeAssistant.exe locked"), "compact context should include blocker");
        AssertTrue(decision.ShouldSend, "assist situation should allow a useful comment");
        AssertEqual("assist", decision.Kind, "coding blocker should be assist, not a generic jab");
    }

    private static void ScreenAwarenessBuildsAggregatePatternCandidate()
    {
        var service = new KokoScreenAwarenessService();
        var analysis = new KokoScreenAwarenessAnalysis
        {
            SummaryUk = "Dota 2 match is open in Steam",
            ActivityUk = "active game",
            ScreenMode = "game",
            Importance = 0.7
        };
        var situation = new KokoScreenSituation
        {
            CurrentTask = "playing Dota 2",
            Progress = "moving",
            RecommendedBehavior = "observe"
        };

        var candidate = service.BuildPatternCandidate(analysis, situation, new DateTime(2026, 5, 13, 21, 0, 0));

        AssertTrue(candidate.ShouldRecord, "game activity should produce an aggregate pattern candidate");
        AssertTrue(candidate.Key.Contains("dota"), "pattern key should preserve useful game category");
        AssertTrue(candidate.Text.Contains("вечір") || candidate.Text.Contains("вечір"), "pattern text should include time slot in Ukrainian");
        AssertTrue(candidate.Text.Contains("Dota 2"), "pattern text should summarize the game category");

        var privateCandidate = service.BuildPatternCandidate(
            new KokoScreenAwarenessAnalysis { ScreenMode = "private", SummaryUk = "API token settings" },
            new KokoScreenSituation { CurrentTask = "checking token" },
            new DateTime(2026, 5, 13, 21, 0, 0));
        AssertTrue(!privateCandidate.ShouldRecord, "private screens must not be recorded as patterns");
    }

    private static void StartupGreetingAvoidsDeadCannedReplies()
    {
        var now = new DateTime(2026, 5, 7, 17, 5, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "авто відповіді дивні і тупі", Timestamp = now.AddMinutes(-15) },
            new ChatRepository.ChatMessage { Role = "assistant", Content = "Фікшу авто-пінги", Timestamp = now.AddMinutes(-12) }
        };

        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now);
        var fallback = service.BuildFallback(frame);

        AssertTrue(!fallback.Contains("Знову тут"), "startup fallback should avoid dead canned opening");
        AssertTrue(!fallback.Contains("де тебе носило"), "startup fallback should avoid generic return jab");
        AssertTrue(fallback.Contains("авто") || fallback.Contains("пінг"), "startup fallback should preserve last concrete topic");
    }

    private static void StartupGreetingSanitizesDryReturnLine()
    {
        var now = new DateTime(2026, 5, 7, 17, 5, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "покращи реакції при вході", Timestamp = now.AddMinutes(-20) }
        };

        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now);
        var sanitized = service.Sanitize("Знову тут. Значить, щось недороблено.", frame);

        AssertTrue(!sanitized.Contains("Знову тут"), "sanitizer should replace canned startup greeting");
        AssertTrue(sanitized.Contains("покращи") || sanitized.Contains("реакції") || sanitized.Contains("вході"), "sanitized greeting should use last topic");
    }

    private static void StartupGreetingIgnoresLowSignalTopic()
    {
        var now = new DateTime(2026, 5, 12, 20, 16, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "ні я вже награвся хах", Timestamp = now.AddMinutes(-9) },
            new ChatRepository.ChatMessage { Role = "user", Content = "привіт", Timestamp = now.AddMinutes(-1) }
        };

        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now);
        var fallback = service.BuildFallback(frame);

        AssertTrue(!string.Equals(frame.LastConcreteTopic, "привіт", StringComparison.OrdinalIgnoreCase), "greeting must not become concrete topic");
        AssertTrue(!fallback.Contains("тема «привіт»"), "fallback must not frame greeting as a topic");
        AssertTrue(!fallback.Contains("добиваємо"), "fallback should avoid dumb 'finish the topic' wording");
    }

    private static void StartupGreetingReactsToQuickReturn()
    {
        var now = new DateTime(2026, 5, 13, 1, 35, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "зроби живі відповіді при вході", Timestamp = now.AddMinutes(-4) }
        };

        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now);
        var fallback = service.BuildFallback(frame);

        AssertTrue(fallback.Contains("Швидко") || fallback.Contains("майже не зникав") || fallback.Contains("без довгої паузи"),
            "quick return greeting should acknowledge the short gap");
        AssertTrue(fallback.Contains("живі відповіді") || fallback.Contains("вході"),
            "quick return greeting should preserve concrete topic");
        AssertTrue(!fallback.Contains("Останній хвіст"), "quick return greeting should avoid dry tail wording");
    }

    private static void StartupGreetingPromptUsesMoodAndAbsence()
    {
        var now = new DateTime(2026, 5, 13, 3, 40, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "хм може вхідні репліки зробити живими", Timestamp = now.AddMinutes(-42) }
        };

        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now);
        service.EnrichFrame(
            frame,
            now,
            "emotion=Irritated; bond=Trusted; stress=0.31",
            "03:10 user wanted realistic startup replies");

        AssertTrue(frame.PromptBlock.Contains("emotion=Irritated"), "startup prompt should include runtime mood");
        AssertTrue(frame.PromptBlock.Contains(frame.ReturnModeUk) || frame.PromptBlock.Contains("STARTUP GREETING CONTEXT"), "startup prompt should include return mode");
        AssertTrue(frame.PromptBlock.Contains("жив"), "startup prompt should demand a live generated reply");
        AssertTrue(!string.IsNullOrWhiteSpace(frame.AbsenceReadUk), "startup frame should infer absence context");
    }

    private static void StartupGreetingPromptIncludesTemporalPresence()
    {
        var now = new DateTime(2026, 6, 15, 14, 0, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "перевір потім цей модуль пам'яті", Timestamp = now.AddHours(-3) }
        };
        var state = new KokoInternalState
        {
            PersonaPatienceLevel = 0.30,
            PersonaEnergyLevel = 0.50,
            PersonalityDailyMood = "sharp"
        };

        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now, state);

        AssertTrue(frame.TemporalPresenceContext.Contains("TEMPORAL PRESENCE AWARENESS", StringComparison.OrdinalIgnoreCase), "startup frame should include temporal presence prompt");
        AssertTrue(frame.PromptBlock.Contains("Temporal presence:", StringComparison.OrdinalIgnoreCase), "startup prompt should include temporal presence section");
        AssertTrue(frame.PromptBlock.Contains("user_last_exit_type: abrupt_unannounced", StringComparison.OrdinalIgnoreCase), "startup prompt should pass normalized exit type");
        AssertEqual("abrupt_unannounced", state.LastTemporalPresenceExitType, "startup frame should update temporal presence state when state is provided");
    }

    private static void StartupGreetingClassifiesCleanGoodbye()
    {
        var now = new DateTime(2026, 5, 31, 14, 0, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "Добре, до завтра", Timestamp = now.AddHours(-5) }
        };

        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now);
        var fallback = service.BuildFallback(frame);

        AssertEqual("clean_goodbye", frame.DepartureKind, "clean goodbye should be classified");
        AssertTrue(fallback.Contains("нормально закрив розмову"), "greeting should not invent drama after a clean goodbye");
    }

    private static void StartupGreetingClassifiesAbruptExit()
    {
        var now = new DateTime(2026, 5, 31, 14, 0, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "перевір потім цей модуль пам'яті", Timestamp = now.AddHours(-3) }
        };

        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now);
        var fallback = service.BuildFallback(frame);

        AssertEqual("abrupt_exit", frame.DepartureKind, "unfinished user turn should be treated as abrupt exit");
        AssertTrue(fallback.Contains("обірвався") || fallback.Contains("зник") || fallback.Contains("відкритим") || fallback.Contains("закриття"),
            "greeting should mention the interrupted continuity");
    }

    private static void StartupGreetingClassifiesConflictResidue()
    {
        var now = new DateTime(2026, 5, 31, 14, 0, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "ти мене обідила і дістала", Timestamp = now.AddHours(-2) }
        };

        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now);
        var fallback = service.BuildFallback(frame);

        AssertEqual("conflict_or_hurt", frame.DepartureKind, "hurt/conflict tail should be classified");
        AssertTrue(frame.EmotionalResidueUk.Contains("конфлікту") || frame.EmotionalResidueUk.Contains("образи"), "emotional residue should be explicit");
        AssertTrue(fallback.Contains("криво") || fallback.Contains("помітила"), "greeting should acknowledge residue without theatrical apology");
    }

    private static void StartupGreetingRejectsStaleContextTableFallback()
    {
        var now = new DateTime(2026, 6, 16, 3, 16, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "більше фактів ... чим не подобаюсь .. що дратує", Timestamp = now.AddMinutes(-92) }
        };
        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now, new KokoInternalState
        {
            PersonaPatienceLevel = 0.44,
            PersonalityDailyMood = "sharp"
        });

        var fallback = service.BuildFallback(frame);
        var sanitized = service.Sanitize("Привіт. Контекст про останню задачу ще на столі. Продовжуємо чи міняємо ціль?", frame);

        AssertTrue(!fallback.Contains("Контекст про", StringComparison.OrdinalIgnoreCase), "fallback should avoid canned context-table phrasing");
        AssertTrue(!fallback.Contains("ще на столі", StringComparison.OrdinalIgnoreCase), "fallback should not say context is on the table");
        AssertTrue(!fallback.Contains("Продовжуємо чи міняємо ціль", StringComparison.OrdinalIgnoreCase), "fallback should avoid stale continue-or-switch prompt");
        AssertTrue(!sanitized.Contains("Контекст про", StringComparison.OrdinalIgnoreCase), "sanitizer should reject canned context-table phrasing");
        AssertTrue(!sanitized.Contains("Продовжуємо чи міняємо ціль", StringComparison.OrdinalIgnoreCase), "sanitizer should reject exact stale line");
        AssertTrue(sanitized.Contains("факт", StringComparison.OrdinalIgnoreCase) ||
                   sanitized.Contains("задач", StringComparison.OrdinalIgnoreCase) ||
                   sanitized.Contains("нитк", StringComparison.OrdinalIgnoreCase),
            "replacement should still preserve context");
    }

    private static void StartupGreetingSanitizesTherapyMeta()
    {
        var now = new DateTime(2026, 5, 13, 3, 40, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "про тебе", Timestamp = now.AddMinutes(-12) }
        };

        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now);
        var sanitized = service.Sanitize("Ніби щось важливе застрягло в твоїй голові, а ти боїшся сказати.", frame);

        AssertTrue(!sanitized.Contains("боїшся"), "startup sanitizer should reject therapy-meta fear guessing");
        AssertTrue(!sanitized.Contains("застрягло"), "startup sanitizer should reject stuck-in-head framing");
    }

    private static void StartupGreetingFallbackDoesNotQuoteRawLatestUserText()
    {
        var now = new DateTime(2026, 5, 19, 15, 36, 0);
        var raw = "чому ти не написала";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = raw, Timestamp = now.AddMinutes(-28) }
        };

        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now);
        var fallback = service.BuildFallback(frame);
        var sanitized = service.Sanitize($"Через 28 хв ти знову тут. «{raw}» не втекло.", frame);

        AssertTrue(!fallback.Contains(raw, StringComparison.OrdinalIgnoreCase), "startup fallback should not quote raw latest user text");
        AssertTrue(!fallback.Contains("«"), "startup fallback should avoid quote framing");
        AssertTrue(!fallback.Contains("знову тут", StringComparison.OrdinalIgnoreCase), "startup fallback should avoid dead canned opening");
        AssertTrue(!sanitized.Contains(raw, StringComparison.OrdinalIgnoreCase), "sanitized startup reply should not preserve quoted raw user text");
    }

    private static void StartupGreetingSanitizesSystemStatusReport()
    {
        var now = new DateTime(2026, 6, 11, 9, 0, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "hello", Timestamp = now.AddMinutes(-15) }
        };

        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now);
        var sanitized = service.Sanitize("researched 4 topics; findings=2; scheduler: 9d69553ed6; background task complete", frame);

        AssertTrue(!sanitized.Contains("researched", StringComparison.OrdinalIgnoreCase), "startup sanitizer should hide research mechanics");
        AssertTrue(!sanitized.Contains("findings=", StringComparison.OrdinalIgnoreCase), "startup sanitizer should hide finding counters");
        AssertTrue(!sanitized.Contains("scheduler:", StringComparison.OrdinalIgnoreCase), "startup sanitizer should hide scheduler ids");
        AssertTrue(!string.IsNullOrWhiteSpace(sanitized), "startup sanitizer should replace leaked status with a real greeting");
    }

    private static void LlmDiagnosticsSnapshotStartsIdle()
    {
        var diag = new LlmService().GetDiagnosticsSnapshot();

        AssertEqual("idle", diag.Status, "diagnostics should start idle before any request");
        AssertEqual(0L, diag.TotalRequests, "diagnostics should not invent requests");
        AssertTrue(diag.ConsecutiveFailures == 0, "diagnostics should not start in failure state");
    }

    private static void LlmDiagnosticsLabelsLocalOllamaProvider()
    {
        var service = new LlmService();
        typeof(LlmService).GetField("_provider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(service, "ollama");
        typeof(LlmService).GetField("_model", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(service, "llama3.2");

        var provider = typeof(LlmService).GetMethod("ActiveProviderLabel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(service, Array.Empty<object>())?.ToString();
        var model = typeof(LlmService).GetMethod("ActiveModelLabel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(service, new object[] { false })?.ToString();

        AssertEqual("Ollama Local", provider, "local Ollama provider should not be mislabeled as LM Studio");
        AssertEqual("llama3.2", model, "diagnostics should expose local Ollama model");
    }

    private static void LlmExtractsReasoningContentForVisionReplies()
    {
        var response = JObject.Parse("""
{
  "choices": [
    {
      "message": {
        "content": "",
        "reasoning_content": "Бачу темний інтерфейс KokonoeAssistant і чат зі screen-scan fallback."
      }
    }
  ]
}
""");

        var extracted = LlmService.ExtractOpenAiCompatibleMessageText(response);
        AssertTrue(extracted.Contains("KokonoeAssistant"), "vision parser should use reasoning_content when content is empty");
    }

    private static void LlmUsesOllamaImageUrlStringPayload()
    {
        var ollamaBlock = LlmService.BuildOpenAiImageBlockForProvider("ollama-cloud", "image/jpeg", "abc123");
        AssertEqual("image_url", ollamaBlock["type"]?.ToString(), "Ollama image block should use image_url type");
        AssertTrue(ollamaBlock["image_url"]?.Type == JTokenType.String, "Ollama OpenAI-compatible vision expects image_url as a data URL string");
        AssertTrue(ollamaBlock["image_url"]?.ToString().StartsWith("data:image/jpeg;base64,") == true, "Ollama image URL should be a data URI");

        var lmStudioBlock = LlmService.BuildOpenAiImageBlockForProvider("lmstudio", "image/jpeg", "abc123");
        AssertTrue(lmStudioBlock["image_url"]?.Type == JTokenType.Object, "non-Ollama OpenAI-style providers should keep image_url.url object form");
    }

    private static void LlmVisibleTextRejectsDottedGarbage()
    {
        var dotted = """
... ... ..., ... ... ..., ... ... ...

..., ... ... ....
*........ .... ...*

... ..., Yasu. ... ..., ... ... ... ...

... ... .... ... ... ... IQ ..., ... ... ... ...

Чекаю наступного запиту.
""";

        AssertTrue(LlmService.LooksLikeBrokenVisibleText(dotted), "dotted replacement garbage must not be visible");

        var repaired = LlmService.RepairMojibake("���� Yasu ����");
        AssertTrue(!repaired.Contains("..."), "replacement characters should be removed, not converted into dotted UI text");

        var normal = "Бачу чат KokonoeAssistant і твій запит на знімок екрана. Видно темну тему, а збій був у текстовому виводі, не в самому capture.";
        AssertTrue(!LlmService.LooksLikeBrokenVisibleText(normal), "normal Ukrainian output should remain visible");
    }

    private static void LlmRotatesOllamaKeyAfterAuthFailure()
    {
        var method = typeof(LlmService).GetMethod(
            "TryRotateOllamaKeyAfterFailure",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        AssertTrue(method != null, "rotation helper should exist");
        var service = new LlmService();

        bool Call(int status) => (bool)method!.Invoke(service, new object?[] { null, "bad-key", status })!;

        AssertTrue(Call(401), "HTTP 401 should rotate away from the key");
        AssertTrue(Call(403), "HTTP 403 should rotate away from the key");
        AssertTrue(Call(429), "HTTP 429 should rotate away from the key");
        AssertTrue(!Call(500), "HTTP 500 should not burn a key");
    }

    private static void LlmAgentKeyPoolExhaustionDoesNotReuseLegacyKey()
    {
        var service = new LlmService();
        var profilesField = typeof(LlmService).GetField(
            "_agentProfiles",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var resolve = typeof(LlmService).GetMethod(
            "ResolveOllamaKey",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        AssertTrue(profilesField != null, "agent profiles field should exist");
        AssertTrue(resolve != null, "ResolveOllamaKey helper should exist");

        var exhaustedKey = "same-legacy-key";
        profilesField!.SetValue(service, new Dictionary<string, KokoAgentLlmProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["chat"] = new KokoAgentLlmProfile
            {
                AgentId = "chat",
                Enabled = true,
                LlmProvider = "ollama-cloud",
                OllamaApiKey = exhaustedKey,
                OllamaKeys = new List<OllamaKeyEntry>
                {
                    new()
                    {
                        Name = "legacy",
                        Key = exhaustedKey,
                        Enabled = true,
                        CooldownUntil = DateTime.UtcNow.AddMinutes(30)
                    }
                }
            }
        });

        var resolved = (string?)resolve!.Invoke(service, new object?[] { "chat" });

        AssertTrue(string.IsNullOrWhiteSpace(resolved), "exhausted agent key pool must not fall back to the same legacy key");
    }

    private static void InspectorRendersStateReport()
    {
        using var ctx = TestContext.Create();
        ctx.Memory.LearnFactBlocking("User builds Kokonoe as a personal assistant", "project", 0.8f);
        ctx.Memory.RecordEpisodeBlocking("Somatic layer was added", "focused", 0.7f);

        var internalState = new KokoInternalState
        {
            PersonalityDailyMood = "focused",
            MoodScore = 0.64f,
            LastUserEmotionalTone = "seeking",
            LastInitiativeDecision = "act:self_regulation_protect",
            LastPresenceSummary = "Середня тиша: 2 год.",
            LastPresenceSituation = "medium_silence",
            LastInternalDaySummary = "Вечірній огляд: підбити хвости.",
            LastInternalDayPhase = "evening_review",
            LastInternalDayFocus = "підбивати підсумки",
            LastAutonomyDecision = "19:30 act:presence_long_silence src:presence p90"
        };
        internalState.CuriosityQueue.Add("What should I optimize next?");
        internalState.InnerMonologues.Add("[somatic/focus] Signal is clean. Work mode.");
        internalState.PresenceTrace.Add("07.05 19:30 medium_silence");
        internalState.InternalDayTrace.Add("07.05 19:30 evening_review");
        internalState.AutonomyDecisionLog.Add("19:30 act:presence_long_silence src:presence p90");

        var somatic = new KokoSomaticSnapshot
        {
            State = "focused",
            Label = "active baseline",
            BehaviorHint = "task-first",
            Bpm = 72,
            BaselineBpm = 64,
            BpmDelta = 8,
            Strain = 0.42,
            Calm = 0.55,
            Volatility = 0.20
        };
        var selfReg = new KokoSelfRegulationFrame
        {
            Reaction = "clean_focus",
            Regulation = "focus",
            PrivateThought = "Signal is clean. Work mode.",
            BehaviorDirective = "structured, decisive"
        };

        var inspector = new KokoStateInspectorService();
        var snapshot = inspector.Capture(
            internalState,
            ctx.Emotion,
            ctx.Relationship,
            ctx.Memory,
            somatic,
            selfReg,
            new[] { "act:self_regulation_protect" },
            new[] { "clean_focus/focus: Signal is clean." });

        var markdown = inspector.ToMarkdown(snapshot);
        var json = inspector.ToJson(snapshot);

        AssertTrue(markdown.Contains("Інспектор стану Коконое") || markdown.Contains("Інспектор стану Коконое"), "markdown should have inspector title");
        AssertTrue(markdown.Contains("## Соматика") || markdown.Contains("## Соматика"), "markdown should include somatic section");
        AssertTrue(markdown.Contains("## Присутність і день") || markdown.Contains("## Присутність і день"), "markdown should include presence/day section");
        AssertTrue(markdown.Contains("Журнал автономності") || markdown.Contains("Журнал автономності"), "markdown should include autonomy log");
        AssertTrue(markdown.Contains("## Головні факти") || markdown.Contains("## Головні факти"), "markdown should include facts");
        AssertTrue(json.Contains("\"LastInternalDayPhase\""), "json should include internal day phase");
        AssertTrue(json.Contains("\"LastAutonomyDecision\""), "json should include autonomy decision");
        AssertTrue(json.Contains("\"Somatic\""), "json should include somatic object");
    }

    private static void ObsidianVaultArchitectureMaintenance()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Project/Idea.md", "# Idea\n\nKokonoe should maintain Obsidian architecture.");

            var result = obsidian.MaintainKokonoeVaultArchitecture("test");

            AssertTrue(result.CreatedFolders.Contains("Kokonoe/Architecture"), "architecture folder should be created");
            AssertTrue(File.Exists(Path.Combine(dir, "Kokonoe", "Vault Index.md")), "vault index should be created");
            AssertTrue(File.Exists(Path.Combine(dir, "Kokonoe", "Architecture", "Manifest.md")), "manifest should be created");
            AssertTrue(File.Exists(Path.Combine(dir, "Kokonoe", "Architecture", "Health.md")), "health note should be created");
            AssertTrue(File.Exists(Path.Combine(dir, "Kokonoe", "Architecture", "Backlog.md")), "backlog should be created");
            AssertTrue(File.Exists(Path.Combine(dir, "Kokonoe", "Automation", "Obsidian Sync.md")), "automation note should be created");
            var changeLog = File.ReadAllText(Path.Combine(dir, "Kokonoe", "Architecture", "Change Log.md"));
            AssertTrue(changeLog.Contains("Причина: test") || changeLog.Contains("Причина: test"), "change log should record reason");

            var second = obsidian.MaintainKokonoeVaultArchitecture("test-inventory-settle");
            AssertTrue(second.CreatedNotes.Count == 0, "second maintenance should not create managed notes again");

            var stable = obsidian.MaintainKokonoeVaultArchitecture("test-idempotent-a");
            stable = obsidian.MaintainKokonoeVaultArchitecture("test-idempotent-b");
            AssertTrue(stable.CreatedNotes.Count == 0, "settled maintenance should not create managed notes again");
            var vaultIndex = File.ReadAllText(Path.Combine(dir, "Kokonoe", "Vault Index.md"));
            AssertTrue(vaultIndex.Contains("managed-by: Kokonoe"), "managed notes should keep ownership marker");
            AssertTrue(!vaultIndex.Contains("/]]"), "vault index should not create empty graph nodes for folders");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void ObsidianUniqueMemoryAppend()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            var first = obsidian.AppendUniqueItemsToNote(
                "Kokonoe/Memory/Facts.md",
                "# Facts\n",
                new[]
                {
                    "User builds Kokonoe as a personal assistant",
                    "User builds Kokonoe as a personal assistant"
                },
                "auto-fact");
            var second = obsidian.AppendUniqueItemsToNote(
                "Kokonoe/Memory/Facts.md",
                "# Facts\n",
                new[] { "The user builds Kokonoe as a personal assistant." },
                "auto-fact");

            AssertEqual(1, first, "first unique append should keep one item");
            AssertEqual(0, second, "similar memory should be treated as duplicate");
            var content = File.ReadAllText(Path.Combine(dir, "Kokonoe", "Memory", "Facts.md"));
            AssertTrue(content.Split("[auto-fact]").Length - 1 == 1, "note should contain one auto-fact item");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void ObsidianExcludesProfileBackupsFromIndex()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Project/Live.md", "# Live\n\nneedle-live");
            obsidian.WriteNote("Kokonoe/Profile Backups/Profile-20260611-010203.md", "# Backup\n\nneedle-backup-only");

            var notes = obsidian.ListNotes();
            var search = obsidian.SearchNotes("needle-backup-only", 10);
            var index = obsidian.GetNoteIndex();

            AssertTrue(notes.Contains("Project/Live.md"), "normal vault note should be listed");
            AssertTrue(!notes.Any(p => p.Contains("Profile Backups", StringComparison.OrdinalIgnoreCase)), "profile backups should be excluded from note listing");
            AssertTrue(search.Count == 0, "profile backups should be excluded from search");
            AssertTrue(!index.Values.Any(p => p.Contains("Profile Backups", StringComparison.OrdinalIgnoreCase)), "profile backups should be excluded from note index");
            AssertTrue(obsidian.ReadNote("Kokonoe/Profile Backups/Profile-20260611-010203.md") != null, "direct maintenance reads should still work");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void ObsidianProfileUpdaterWritesNeutralProfile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Creator/Profile.md", """
---
type: creator-profile
---

# Творець — Вова (Yasu)

## Базові факти
- **Ім'я:** Вова (Yasu / Yasu-kun)
- **Вік:** 21 рік (народився 21.04.2005)
- **Місцезнаходження:** Іспанія

## Звички
- **Вподобання:** anal, pussy, big penis, femboy
- ознака порушеного когнітивного функціонування
""");

            using var chat = new ChatRepository(dir);
            chat.InsertMessage(new ChatRepository.ChatMessage
            {
                Role = "user",
                Content = "мені не подобається нав'язливий ролплей, онови профіль в обсидіані нормально",
                Timestamp = DateTime.Now.AddMinutes(-2)
            });

            var service = new KokoProfileUpdateService(obsidian, chat);
            var result = service.UpdateProfileFromRecentContext("онови мій профіль в Obsidian");
            var content = obsidian.ReadNote("Creator/Profile.md") ?? "";

            AssertTrue(result.Success, "profile update should succeed");
            AssertTrue(File.Exists(Path.Combine(dir, result.BackupPath.Replace('/', Path.DirectorySeparatorChar))), "profile update should write backup");
            AssertTrue(content.Contains("Операційні правила Kokonoe"), "profile should include operating rules");
            AssertTrue(content.Contains("рольплеєм") || content.Contains("рольплей"), "profile should record roleplay preference");
            AssertTrue(content.Contains("Сигнали з останнього контексту"), "fallback profile should synthesize recent context");
            AssertTrue(!content.Contains("Останній корисний контекст"), "profile should not dump raw recent chat");
            AssertTrue(!content.Contains("## Службове"), "profile should not expose service metadata as profile content");
            AssertTrue(!content.Contains("можеш обновити мій профіль", StringComparison.OrdinalIgnoreCase), "profile should not copy raw user messages");
            AssertTrue(!content.Contains("порушеного когнітивного функціонування"), "profile should remove insulting cognitive framing");
            AssertTrue(!content.Contains("anal", StringComparison.OrdinalIgnoreCase), "profile should remove explicit private tags from main profile");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void ObsidianProfileUpdaterThrottlesBackupSpam()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Creator/Profile.md", "# Profile\n\nInitial");

            using var chat = new ChatRepository(dir);
            chat.InsertMessage(new ChatRepository.ChatMessage
            {
                Role = "user",
                Content = "update profile with concrete operating rules",
                Timestamp = DateTime.Now.AddMinutes(-2)
            });

            var service = new KokoProfileUpdateService(obsidian, chat);
            var first = service.UpdateProfileFromRecentContext("update profile");
            var second = service.UpdateProfileFromRecentContext("update profile again");

            var backupDir = Path.Combine(dir, "Kokonoe", "Profile Backups");
            var backups = Directory.Exists(backupDir)
                ? Directory.GetFiles(backupDir, "Profile-*.md", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();

            AssertTrue(first.Success && second.Success, "profile updates should both succeed");
            AssertEqual(first.BackupPath, second.BackupPath, "same-day profile updates should reuse the daily backup");
            AssertEqual(1, backups.Length, "same-day profile updates should not spam backup files");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void AutonomousProfileCuratorDetectsDurableFacts()
    {
        AssertTrue(
            KokoAutonomousProfileCuratorService.ShouldTriggerFromExchange(
                "Remember this for Obsidian: I do not want intrusive roleplay; keep the profile factual.",
                "Recorded as an operating preference."),
            "curator should trigger on durable Obsidian/profile preference");

        AssertTrue(
            KokoAutonomousProfileCuratorService.ShouldTriggerFromExchange(
                "Watch bridge is now linked and pulse telemetry is a project priority.",
                ""),
            "curator should trigger on wearable/project state");

        AssertTrue(
            !KokoAutonomousProfileCuratorService.ShouldTriggerFromExchange("hi", "hello"),
            "curator should ignore low-signal greetings");
    }

    private static void AutonomousProfileCuratorDetectsPromiseObligations()
    {
        AssertTrue(
            KokoAutonomousProfileCuratorService.ShouldTriggerFromExchange(
                "онови профіль і зроби все нормально",
                "Зроблю: оновлю Obsidian, прожену тести, потім закомічу і запушу."),
            "curator should wake on concrete promises and obligations");

        AssertTrue(
            KokoAutonomousProfileCuratorService.ShouldTriggerFromExchange(
                "I need you to control this yourself from now on.",
                "Promise ledger will track the obligation instead of leaving a chat-only promise."),
            "curator should wake on promise ledger language");
    }

    private static void AutonomousProfileCuratorWritesSynthesizedUpdate()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Creator/Profile.md", """
---
type: creator-profile
---

# Creator Profile

## Stable Facts
- Name: Yasu
""");

            using var chat = new ChatRepository(dir);
            chat.InsertMessage(new ChatRepository.ChatMessage
            {
                Role = "user",
                Author = "user",
                Content = "Remember for Obsidian: profile updates must be autonomous, factual, and based on real project state. Watch bridge and pulse telemetry are active priorities.",
                Timestamp = DateTime.Now.AddMinutes(-1)
            });

            var profileUpdater = new KokoProfileUpdateService(obsidian, chat);
            using var curator = new KokoAutonomousProfileCuratorService(
                Path.Combine(dir, "kokonoe-data"),
                chat,
                obsidian,
                profileUpdater,
                () => null);

            var result = curator.RunOnceAsync("test-autonomous-curator", force: true).GetAwaiter().GetResult();
            var profile = obsidian.ReadNote("Creator/Profile.md") ?? "";
            var status = obsidian.ReadNote("Kokonoe/Automation/Profile Curator.md") ?? "";

            AssertTrue(result != null && result.Success, "curator should perform a profile update when forced with durable context");
            AssertTrue(profile.Contains("Obsidian", StringComparison.OrdinalIgnoreCase), "profile should keep Obsidian operating context");
            AssertTrue(profile.Contains("Watch bridge", StringComparison.OrdinalIgnoreCase) || profile.Contains("Galaxy Watch", StringComparison.OrdinalIgnoreCase), "profile should retain wearable project context");
            AssertTrue(profile.Contains("Сигнали з останнього контексту") || profile.Contains("РЎРёРіРЅР°Р»Рё Р· РѕСЃС‚Р°РЅРЅСЊРѕРіРѕ РєРѕРЅС‚РµРєСЃС‚Сѓ"), "profile should synthesize recent signals");
            AssertTrue(!profile.Contains("Remember for Obsidian:", StringComparison.OrdinalIgnoreCase), "profile should not copy raw user command text");
            AssertTrue(!profile.Contains("## Службове") && !profile.Contains("## РЎР»СѓР¶Р±РѕРІРµ"), "profile should not expose service metadata");
            AssertTrue(status.Contains("Profile Curator", StringComparison.OrdinalIgnoreCase) || status.Contains("проф", StringComparison.OrdinalIgnoreCase), "curator should write an automation status artifact");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void ObsidianRejectsPathsOutsideVault()
    {
        var parent = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-parent-" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(parent, "vault");
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            var escaped = Path.Combine(parent, "escaped.md");
            var rejected = false;

            try
            {
                obsidian.WriteNote("../escaped.md", "# escaped");
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }

            AssertTrue(rejected, "vault writes must reject parent-directory traversal");
            AssertTrue(!File.Exists(escaped), "rejected traversal must not create files outside the vault");
        }
        finally
        {
            TryDeleteDir(parent);
        }
    }

    private static void LlmVaultToolRoutingAvoidsAccidentalWrites()
    {
        AssertEqual("list_notes", LlmService.DetectBestTool("покажи список нотаток у vault"), "note list request should not default to append");
        AssertEqual("list_folders", LlmService.DetectBestTool("покажи список папок"), "folder list request should not create a folder");
        AssertEqual("create_folder", LlmService.DetectBestTool("створи папку Kokonoe/Review"), "folder creation should still create folders when explicitly requested");
        AssertEqual("read_note", LlmService.DetectBestTool("прочитай Kokonoe/Memory/Facts"), "read request should stay read-only");
    }

    private static void ObsidianMemoryQualityAndTaskQueue()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Kokonoe/Memory/Facts.md", """
# Facts

## sample
- [auto-fact] User likes long answers.
- [auto-fact] User likes long answers.
""");
            obsidian.WriteNote("Kokonoe/Tasks.md", """
# Tasks

## sample
- [task] Реалізувати кращу пам'ять Obsidian
- [x] Готово: прибрати warnings
""");

            var quality = obsidian.AnalyzeMemoryQuality();
            var queue = obsidian.BuildTaskQueue();
            var maintenance = obsidian.MaintainKokonoeVaultArchitecture("test-memory-quality");

            AssertTrue(quality.DuplicateGroups.Count >= 1, "quality report should detect duplicate memory items");
            AssertTrue(queue.OpenTasks.Any(t => t.Text.Contains("пам'ять") || t.Text.Contains("пам'ять")), "task queue should include open task");
            AssertTrue(File.Exists(Path.Combine(dir, "Kokonoe", "Memory", "Quality.md")), "memory quality note should be created");
            AssertTrue(File.Exists(Path.Combine(dir, "Kokonoe", "Tasks Queue.md")), "task queue note should be created");
            AssertTrue(maintenance.MemoryDuplicateGroups >= 1, "maintenance should report duplicate groups");
            AssertTrue(maintenance.OpenTaskCount >= 1, "maintenance should report open tasks");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void ObsidianMemoryDuplicateCleanup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Kokonoe/Memory/Facts.md", """
# Facts

## sample
- [auto-fact] User likes long answers.
- [auto-fact] User likes long answers.
- [x] User likes long answers.
""");

            var preview = obsidian.CleanupDuplicateMemoryItems(dryRun: true);
            var before = File.ReadAllText(Path.Combine(dir, "Kokonoe", "Memory", "Facts.md"));
            var applied = obsidian.CleanupDuplicateMemoryItems(dryRun: false);
            var after = File.ReadAllText(Path.Combine(dir, "Kokonoe", "Memory", "Facts.md"));

            AssertEqual(1, preview.TotalRemoved, "dry-run should find one duplicate active line");
            AssertTrue(before.Split("[auto-fact]").Length - 1 == 2, "dry-run should not rewrite source note");
            AssertEqual(1, applied.TotalRemoved, "apply should remove one duplicate active line");
            AssertTrue(after.Split("[auto-fact]").Length - 1 == 1, "apply should leave one active fact");
            AssertTrue(after.Contains("[x] User likes long answers."), "cleanup should not remove done checklist lines");
            AssertTrue(File.Exists(Path.Combine(dir, "Kokonoe", "Memory", "Cleanup.md")), "cleanup report should be written");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void ObsidianMemoryReviewSuggestions()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Kokonoe/Memory/Facts.md", """
# Facts

## sample
- [auto-fact] User likes long answers.
- [auto-fact] User likes long answers.
""");
            obsidian.WriteNote("Kokonoe/Tasks.md", """
# Tasks

## sample
- [task] Add memory review loop
""");
            obsidian.WriteNote("Kokonoe/Preferences.md", """
# Preferences

## sample
- [preference] User prefers long detailed answers.
""");

            var quality = obsidian.AnalyzeMemoryQuality();
            var queue = obsidian.BuildTaskQueue();
            var review = obsidian.BuildMemoryReview(quality, queue);
            var maintenance = obsidian.MaintainKokonoeVaultArchitecture("test-memory-review");
            var reviewNote = File.ReadAllText(Path.Combine(dir, "Kokonoe", "Memory", "Review.md"));

            AssertTrue(review.Actions.Any(a => a.Action == "merge"), "review should suggest merging exact duplicates");
            AssertTrue(review.Actions.Any(a => a.Action == "keep"), "review should keep open tasks visible");
            AssertTrue(review.Actions.Any(a => a.Action == "promote_to_preference"), "review should identify preference-like memory");
            AssertTrue(reviewNote.Contains("## Об'єднати"), "review note should render merge section");
            AssertTrue(maintenance.MemoryReviewActionCount >= 3, "maintenance should report review action count");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void ObsidianRebuildLinksPreservesFrontmatter()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Kokonoe.md", """
# Kokonoe

Reference note.
""");
            obsidian.WriteNote("Project.md", """
# Project

Reference note.
""");
            obsidian.WriteNote("Chats/sample.md", """
---
type: chat-log
tags: [kokonoe, chat]
date: 2026-05-12
---

# Sample

Kokonoe should stay plain in prose. Project should be linked here, not in YAML tags.
""");

            var result = obsidian.RebuildLinks();
            var updated = obsidian.ReadNote("Chats/sample.md") ?? "";

            AssertTrue(result.linksAdded >= 1, "rebuild should add a body link");
            AssertTrue(updated.Contains("tags: [kokonoe, chat]"), "rebuild must not link frontmatter tags");
            AssertTrue(updated.Contains("Kokonoe should stay plain in prose"), "suppressed actor names should not be linked in body");
            AssertTrue(updated.Contains("[[Project]] should be linked here"), "rebuild should still link ordinary note titles in body");
            AssertTrue(!updated.Contains("tags: [[[Kokonoe]], chat]"), "rebuild must not corrupt tags into wiki links");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void ObsidianRebuildLinksSkipsSuppressedActorNames()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Kokonoe.md", """
# Kokonoe

Actor note.
""");
            obsidian.WriteNote("Project.md", """
# Project

Reference note.
""");
            obsidian.WriteNote("Chats/sample.md", """
---
type: chat-log
tags: [kokonoe, chat]
date: 2026-05-12
---

# Sample

Kokonoe should stay plain. Project should be linked.
""");

            var result = obsidian.RebuildLinks();
            var updated = obsidian.ReadNote("Chats/sample.md") ?? "";

            AssertTrue(result.linksAdded >= 1, "rebuild should still add non-suppressed note links");
            AssertTrue(updated.Contains("Kokonoe should stay plain"), "suppressed actor name should remain plain text");
            AssertTrue(!updated.Contains("[[Kokonoe]]"), "suppressed actor name must not become a wiki link");
            AssertTrue(updated.Contains("[[Project]] should be linked"), "ordinary note title should still be linked");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void ObsidianNormalizesMalformedFrontmatter()
    {
        var normalized = ObsidianMcpService.NormalizeFrontmatter("""
ч---
type: [[finance]]-tracker
tags: [[[kokonoe]], #[[chat]], #category/[[finance]]/expense]
date: [[2026-05-12]]
created: [[2026-05-12]] 13:40
---

# Sample
""");

        AssertTrue(normalized.StartsWith("---\n"), "normalizer should repair garbage before opening frontmatter");
        AssertTrue(normalized.Contains("type: finance-tracker"), "normalizer should remove wiki links from scalar metadata");
        AssertTrue(normalized.Contains("tags: [kokonoe, chat, category/finance/expense]"), "normalizer should flatten and clean tags");
        AssertTrue(normalized.Contains("date: 2026-05-12"), "normalizer should turn wiki-link date into scalar date");
        AssertTrue(normalized.Contains("created: 2026-05-12 13:40"), "normalizer should preserve created time while removing wiki link");
        AssertTrue(!normalized.Contains("[["), "frontmatter normalizer should remove wiki links from metadata");
    }

    private static void ObsidianVaultDoctorRepairsPhantomGraphLinks()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            File.WriteAllText(Path.Combine(dir, "Kokonoe.md"), "");
            obsidian.WriteNote("Daily.md", "");
            obsidian.WriteNote("Notes/sample.md", """
---
type: chat-log
tags: [[[kokonoe]], chat]
---

# Sample

[[Kokonoe]] saw [[Daily/]] and [[MissingTarget]].
""");

            var report = obsidian.RunVaultDoctor(repair: true);
            var sample = obsidian.ReadNote("Notes/sample.md") ?? "";

            AssertTrue(report.SuppressedActorLinkCount == 1, "doctor should detect suppressed actor links");
            AssertTrue(report.FolderWikiLinkCount == 1, "doctor should detect folder wiki-links");
            AssertTrue(!File.Exists(Path.Combine(dir, "Kokonoe.md")), "doctor should delete empty root Kokonoe note");
            AssertTrue(File.Exists(Path.Combine(dir, "Daily.md")), "doctor should keep/fill non-root empty notes");
            AssertTrue(!sample.Contains("[[Kokonoe]]"), "doctor should remove Kokonoe wiki-link");
            AssertTrue(sample.Contains("`Daily/`"), "doctor should convert folder wiki-link to code path");
            AssertTrue(sample.Contains("tags: [kokonoe, chat]"), "doctor should normalize malformed frontmatter");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void StateReconciliationClosesStaleIntentFromExternalActivity()
    {
        var now = DateTime.Today.AddHours(14);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "course",
            Summary = "пішов на курси",
            CreatedAt = now.AddHours(-3),
            ExpectedUntil = now.AddHours(-1),
            FollowUpAt = now.AddMinutes(-30)
        });

        var result = new KokoStateFreshnessService().Refresh(
            state,
            Array.Empty<ChatRepository.ChatMessage>(),
            now,
            new KokoStateReconciliationSignals
            {
                Channel = "telegram",
                ScreenMode = "telegram",
                ScreenSummary = "активний чат",
                LastDesktopActivityAt = now.AddMinutes(-5)
            });

        AssertTrue(result.ResolvedIntentCount == 1, "external activity should close stale course intent");
        AssertTrue(state.ShortTermIntents[0].ResolvedAt.HasValue, "intent should be resolved");
    }

    private static void ScreenAwarenessClassifiesModes()
    {
        AssertEqual("coding", KokoScreenAwarenessService.NormalizeMode("", "Visual Studio build exception"), "coding mode");
        AssertEqual("obsidian", KokoScreenAwarenessService.NormalizeMode("", "Obsidian graph vault"), "obsidian mode");
        AssertEqual("private", KokoScreenAwarenessService.NormalizeMode("", "API key token settings"), "private mode");
    }

    private static void ProactiveContextRequiresNaturalTriggerForSilencePing()
    {
        var now = DateTime.Today.AddHours(12);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "ок", Timestamp = now.AddMinutes(-30) }
        };
        var frame = new KokoProactiveContextService().Build(messages, new KokoInternalState(), now);
        var check = new KokoProactiveContextService().Check("Тиша якась.", frame, "silence_l1");

        AssertTrue(!frame.HasNaturalTrigger, "short weak silence should not be a natural trigger");
        AssertTrue(!check.Passed, "silence ping should be blocked without natural trigger");
    }

    private static void ObsidianPreflightContextLoadsVaultBeforeReply()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Creator/Profile.md", """
# Creator Profile

- Name: Vova
- Age: 21
""");
            obsidian.WriteNote("Kokonoe/Memory/Facts.md", """
# Facts

- [auto-fact] User wants persistent Obsidian context before every answer.
""");
            obsidian.WriteNote("Kokonoe/Preferences.md", """
# Preferences

- [preference] User likes long detailed answers.
""");
            obsidian.WriteNote($"Daily/{DateTime.Today:yyyy-MM-dd}.md", """
# Today

Kokonoe should check the vault before answering.
""");
            obsidian.WriteNote("Kokonoe/Logs/Somatic Events.md", """
# Somatic Events

## 2026-05-06 12:00:00 - pulse_spike
- Body: wired
- Private thought: Pulse jumped. Contain it.
""");
            obsidian.WriteNote("Project/Context.md", """
# Context

Persistent Obsidian context is now a core project requirement.
""");

            var context = new ObsidianPreflightContextService(obsidian)
                .Build("контекст Obsidian перед відповіддю", now: DateTime.Today.AddHours(18));

            AssertTrue(!string.IsNullOrWhiteSpace(context), "preflight context should be generated");
            AssertTrue(context!.Contains("OBSIDIAN PREFLIGHT"), "preflight marker should be present");
            AssertTrue(context.Contains("Vova"), "creator profile should be included before every answer");
            AssertTrue(context.Contains("Age: 21"), "creator profile age should be included before every answer");
            AssertTrue(context.Contains("persistent Obsidian context before every answer"), "facts note should be included");
            AssertTrue(context.Contains("long detailed answers"), "preferences note should be included");
            AssertTrue(context.Contains("Kokonoe should check the vault"), "daily note should be included");
            AssertTrue(context.Contains("pulse_spike"), "somatic events tail should be included");
            AssertTrue(context.Contains("Query-relevant vault recall"), "query semantic recall should be included");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void ObsidianExplorationFindsInterestingNotes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Journal/Insight.md", """
# Insight

Цікаве спостереження: користувач краще реагує на конкретні знахідки з vault, а не на театральні відмовки про відсутній доступ.
""");
            obsidian.WriteNote("Project/Idea.md", """
# Idea

Ідея: Kokonoe має сама знаходити патерни в нотатках і приносити одну сильну зачіпку без зайвого ритуалу.
""");

            var service = new KokoObsidianExplorationService();
            var reply = service.BuildInterestingFinds(obsidian, "порийся в обсидіані найди щось цікаве");

            AssertTrue(KokoObsidianExplorationService.LooksLikeInterestingVaultDive("порийся в обсидіані найди щось цікаве"), "exploration request should be detected");
            AssertTrue(KokoObsidianExplorationService.LooksLikeInterestingVaultDive("порійся в обсідіані найди щось цікаве"), "typed Ukrainian exploration request should be detected");
            AssertTrue(KokoObsidianExplorationService.LooksLikeExplorationFollowup("і що там ???"), "follow-up should be detected");
            AssertTrue(reply.Contains("Порилась у vault"), "reply should confirm real vault exploration");
            AssertTrue(reply.Contains("Journal/Insight.md") || reply.Contains("Project/Idea.md"), "reply should include actual note paths");
            AssertTrue(!reply.Contains("не підключ", StringComparison.OrdinalIgnoreCase), "reply should not claim Obsidian is disconnected");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void ObsidianAccessReportCompletesTask()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Project/Status.md", "# Status\n\nІдея: перевірити, що Obsidian route повертає завершений результат, а не обіцянку сканувати.");

            AssertTrue(KokoObsidianExplorationService.LooksLikeVaultAccessCheck("спробуй тепер добратись в обсидиан"), "access check should be detected");
            var reply = new KokoObsidianExplorationService().BuildAccessReport(obsidian, "спробуй тепер добратись в обсидиан");

            AssertTrue(reply.Contains("Obsidian-task завершено"), "access report should be a completed task notice");
            AssertTrue(reply.Contains("Доступ: є"), "access report should confirm access");
            AssertTrue(reply.Contains("Project/Status.md"), "access report should include visible note path");
            AssertTrue(!reply.Contains("Починаю сканування", StringComparison.OrdinalIgnoreCase), "access report should not fake pending scan");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void TelegramUnifiedContextLoadsObsidianProfilePreflight()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Creator/Profile.md", """
# Creator Profile

- Name: Vova
- Nickname: Yasu
""");
            using var health = new HealthService(dir);
            using var chat = new ChatRepository(dir);
            using var brain = new KokoBrainEngine(new LlmService(), health, obsidian, chat, dir);

            var context = brain.BuildUnifiedExternalContext("telegram", "ну у vault написано моє ім'я");

            AssertTrue(context.Contains("OBSIDIAN PREFLIGHT"), "telegram context should include Obsidian preflight for vault identity questions");
            AssertTrue(context.Contains("Creator profile"), "telegram context should include creator profile note");
            AssertTrue(context.Contains("Yasu"), "telegram context should include profile alias");
            AssertTrue(context.Contains("vault_memory"), "telegram context should include response planner vault route");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void AgentTaskServicePlansAndPersists()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var service = new KokoAgentTaskService(dir) { AutoStartOnAdd = false };
            var task = service.AddTask("реалізуй UI і перевір Obsidian пам'ять", priority: 8);

            AssertEqual(8, task.Priority, "priority should be stored");
            AssertEqual(4, service.MaxParallel, "agent board should default to four parallel slots");
            AssertTrue(task.Steps.Any(s => s.Kind == KokoAgentStepKind.Vault), "vault step should be planned");
            AssertTrue(task.Steps.Any(s => s.Kind == KokoAgentStepKind.Implement), "implementation step should be planned");
            AssertTrue(task.Steps.Last().Kind == KokoAgentStepKind.Report, "last step should be report");

            var board = service.RenderBoard();
            AssertTrue(board.Contains(task.Id), "board should include task id");
            AssertTrue(board.Contains("Agent Board"), "board should include header");

            var reloaded = new KokoAgentTaskService(dir);
            AssertTrue(reloaded.GetSnapshot().Tasks.Any(t => t.Id == task.Id), "task should persist to disk");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void IterativeAgentLoopExecutesOneActionPerTurn()
    {
        var dir = TempDir();
        try
        {
            var gateway = new TestToolGateway((call, _) => new KokoToolResult
            {
                CallId = call.Id,
                ToolName = call.Name,
                Success = true,
                Verified = true,
                Reason = "written",
                Output = "artifact.txt"
            });
            var agent = new TestIterativeAgent(context => context.Observations.Count == 0
                ? KokoAgentDecision.Execute(new KokoToolCall { Name = "test.write" }, "Need one artifact.", "Write it, then inspect the result.")
                : KokoAgentDecision.Complete("artifact.txt", "The verified write satisfies the objective."));
            var loop = new KokoIterativeAgentLoop(dir, gateway);

            var result = loop.RunAsync("Create one verified artifact", agent, "run-one-action", 4)
                .GetAwaiter().GetResult();

            AssertEqual(KokoAgentRunStatus.Completed, result.Status, "loop should stop when the agent declares completion");
            AssertEqual(1, gateway.Calls.Count, "one action decision must produce exactly one gateway call");
            AssertEqual(2, result.Iteration, "agent should observe the action before declaring completion");
            AssertTrue(result.Observations.Single().Verified, "verified tool evidence must survive in run state");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void IterativeAgentLoopReplansFromFailedObservation()
    {
        var dir = TempDir();
        try
        {
            var gateway = new TestToolGateway((call, index) => new KokoToolResult
            {
                CallId = call.Id,
                ToolName = call.Name,
                Success = index > 1,
                Verified = index > 1,
                Reason = index == 1 ? "temporary failure" : "recovered",
                Output = index == 1 ? "" : "ok"
            });
            var agent = new TestIterativeAgent(context => context.Observations.Count switch
            {
                0 => KokoAgentDecision.Execute(new KokoToolCall { Name = "test.probe" }, "Probe the target.", "Use the primary probe."),
                1 => KokoAgentDecision.Execute(new KokoToolCall { Name = "test.retry" }, "The probe failed; use the fallback.", "Retry once with another tool."),
                _ => KokoAgentDecision.Complete("recovered")
            });
            var loop = new KokoIterativeAgentLoop(dir, gateway);

            var result = loop.RunAsync("Recover a transient operation", agent, "run-replan", 5)
                .GetAwaiter().GetResult();

            AssertEqual(KokoAgentRunStatus.Completed, result.Status, "failed observations should return to the agent for replanning");
            AssertEqual(2, gateway.Calls.Count, "replan should execute only the selected fallback action");
            AssertTrue(!result.Observations[0].Success && result.Observations[1].Success,
                "run history must preserve failure followed by recovery");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void IterativeAgentLoopPersistsConfirmationState()
    {
        var dir = TempDir();
        try
        {
            var gateway = new TestToolGateway((call, _) => new KokoToolResult
            {
                CallId = call.Id,
                ToolName = call.Name,
                RequiresConfirmation = true,
                PendingActionId = "pending-42",
                Reason = "confirmation required"
            });
            var agent = new TestIterativeAgent(_ => KokoAgentDecision.Execute(
                new KokoToolCall { Name = "test.risky" },
                "The action is gated.",
                "Request policy confirmation and stop."));
            var loop = new KokoIterativeAgentLoop(dir, gateway);

            var result = loop.RunAsync("Prepare a gated action", agent, "run-confirmation", 4)
                .GetAwaiter().GetResult();
            var resumedWithoutConfirmation = loop.RunAsync("Prepare a gated action", agent, "run-confirmation", 4)
                .GetAwaiter().GetResult();
            var persisted = new KokoAgentRunStore(dir).Load("run-confirmation");

            AssertEqual(KokoAgentRunStatus.AwaitingConfirmation, result.Status,
                "confirmation must pause the loop instead of pretending success");
            AssertTrue(persisted != null, "run state must be recoverable from disk");
            AssertEqual("pending-42", persisted!.Observations.Single().PendingActionId,
                "persisted observation must retain the exact pending action id");
            AssertEqual(KokoAgentRunStatus.AwaitingConfirmation, resumedWithoutConfirmation.Status,
                "ordinary resume must not bypass a pending policy confirmation");
            AssertEqual(1, gateway.Calls.Count, "paused confirmation must not execute the risky action twice");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void IterativeAgentLoopRejectsFakeCompletionWithoutEvidence()
    {
        var dir = TempDir();
        try
        {
            var gateway = new TestToolGateway((call, _) => new KokoToolResult
            {
                CallId = call.Id,
                ToolName = call.Name,
                Success = false,
                Verified = false,
                Reason = "disk denied",
                Output = ""
            });
            var agent = new TestIterativeAgent(context => context.Observations.Count == 0
                ? KokoAgentDecision.Execute(new KokoToolCall { Name = "test.write" }, "Need a file.", "Write it.")
                : KokoAgentDecision.Complete("Done, file created.", "Pretending completion after a failed write."));
            var loop = new KokoIterativeAgentLoop(dir, gateway);

            var result = loop.RunAsync("Create a real artifact", agent, "run-fake-complete", 3)
                .GetAwaiter().GetResult();

            AssertEqual(KokoAgentRunStatus.IterationLimit, result.Status,
                "fake completion must not be accepted when every tool observation failed");
            AssertTrue(result.Observations.Any(o => o.ToolName == "completion_guard"),
                "completion guard must leave explicit rejection evidence");
            AssertTrue(string.IsNullOrWhiteSpace(result.FinalOutput),
                "fake successful final output must not be published");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void IterativeAgentLoopAppendsEvidenceSummary()
    {
        var dir = TempDir();
        try
        {
            var gateway = new TestToolGateway((call, _) => new KokoToolResult
            {
                CallId = call.Id,
                ToolName = call.Name,
                Success = true,
                Verified = true,
                Reason = "written",
                Output = "artifact.txt"
            });
            var agent = new TestIterativeAgent(context => context.Observations.Count == 0
                ? KokoAgentDecision.Execute(new KokoToolCall { Name = "test.write" }, "Need one artifact.", "Write it.")
                : KokoAgentDecision.Complete("Done."));
            var loop = new KokoIterativeAgentLoop(dir, gateway);

            var result = loop.RunAsync("Create one verified artifact", agent, "run-evidence-summary", 4)
                .GetAwaiter().GetResult();

            AssertEqual(KokoAgentRunStatus.Completed, result.Status, "verified tool evidence should allow completion");
            AssertTrue(result.FinalOutput.Contains("Evidence:", StringComparison.OrdinalIgnoreCase),
                "final output should include a compact evidence section");
            AssertTrue(result.FinalOutput.Contains("test.write", StringComparison.OrdinalIgnoreCase) &&
                       result.FinalOutput.Contains("artifact.txt", StringComparison.OrdinalIgnoreCase),
                "evidence summary should name the concrete tool and artifact");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void AgentTaskServiceImportsObsidianBacklog()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Kokonoe/Tasks.md", """
# Tasks

- [task] Перевірити якість пам'яті
- [task] Додати follow-up після завершення задачі
""");

            var service = new KokoAgentTaskService(Path.Combine(dir, "data"), obsidian: obsidian)
            {
                AutoStartOnAdd = false
            };
            var added = service.SyncFromObsidianBacklog(max: 5);
            var duplicateAdded = service.SyncFromObsidianBacklog(max: 5);
            var snap = service.GetSnapshot();

            AssertEqual(2, added, "open Obsidian tasks should be imported");
            AssertEqual(0, duplicateAdded, "sync should not duplicate active objectives");
            AssertTrue(snap.Tasks.All(t => t.Steps.Any(s => s.Kind == KokoAgentStepKind.Vault)), "imported vault tasks should plan vault inspection");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void ResponsePlannerAddsInsightExtraction()
    {
        var steps = KokoResponsePlannerEngine.BuildAgentStepsForObjective(
            "Background Vault Scanner: Проаналізуй останні 10 змінених нотаток в Obsidian та знайди цікаві факти");

        AssertTrue(steps.Any(s => s.Kind == KokoAgentStepKind.Vault), "background scanner should read vault");
        AssertTrue(steps.Any(s => s.Kind == KokoAgentStepKind.InsightExtraction), "background scanner should extract insights");
        AssertTrue(steps.First(s => s.Kind == KokoAgentStepKind.InsightExtraction).Order <
                   steps.First(s => s.Kind == KokoAgentStepKind.Respond).Order,
            "insight extraction should happen before report/response");
    }

    private static void ResponsePlannerAddsSystemControl()
    {
        var steps = KokoResponsePlannerEngine.BuildAgentStepsForObjective(
            "SystemControl: ps: Write-Output koko-system-ok");

        AssertTrue(steps.Any(s => s.Kind == KokoAgentStepKind.SystemControl), "system-control objective should plan a SystemControl step");
        AssertTrue(steps.First(s => s.Kind == KokoAgentStepKind.SystemControl).Order <
                   steps.First(s => s.Kind == KokoAgentStepKind.Respond).Order,
            "system-control should run before response/report");
    }

    private static void ResponsePlannerRoutesGenericScanToSystemControl()
    {
        var frame = new KokoResponsePlannerEngine().Build(
            "\u0449\u043e \u043d\u043e\u0432\u043e\u0433\u043e?",
            new KokoInternalState(),
            new KokoCognitionEngine(Path.GetTempPath()),
            new DateTime(2026, 5, 24, 12, 0, 0));

        AssertEqual("execute", frame.Intent, "generic scan should be an executable intent");
        AssertEqual("os_control", frame.Capability, "generic scan should use PC context capability");
        AssertTrue(frame.RequiresToolUse, "generic scan should require tool use");

        var steps = KokoResponsePlannerEngine.BuildAgentStepsForObjective("\u0449\u043e \u043d\u043e\u0432\u043e\u0433\u043e?");
        AssertTrue(steps.Any(s => s.Kind == KokoAgentStepKind.SystemControl), "generic scan should plan system control");
    }

    private static void ResponsePlannerAddsVisionForScreenScan()
    {
        var frame = new KokoResponsePlannerEngine().Build(
            "what is on my screen?",
            new KokoInternalState(),
            new KokoCognitionEngine(Path.GetTempPath()),
            new DateTime(2026, 5, 24, 12, 0, 0));

        AssertEqual("screen_awareness", frame.Capability, "literal screen scan should use screen awareness");

        var steps = new KokoResponsePlannerEngine().BuildAgentSteps(frame);
        AssertTrue(steps.Any(s => s.Kind == KokoAgentStepKind.SystemControl), "screen scan should collect local context first");
        AssertTrue(steps.Any(s => s.Kind == KokoAgentStepKind.Vision), "screen scan should include vision");
        AssertTrue(steps.First(s => s.Kind == KokoAgentStepKind.SystemControl).Order <
                   steps.First(s => s.Kind == KokoAgentStepKind.Vision).Order,
            "system context should precede vision");
    }

    private static void ResponsePlannerAddsLongObservation()
    {
        var steps = KokoResponsePlannerEngine.BuildAgentStepsForObjective(
            "поспостерігай за екраном протягом 1 хвилини");

        AssertTrue(steps.Any(s => s.Kind == KokoAgentStepKind.Observation), "long screen observation should plan an Observation step");
        AssertTrue(steps.First(s => s.Kind == KokoAgentStepKind.Observation).Order <
                   steps.First(s => s.Kind == KokoAgentStepKind.Respond).Order,
            "observation should run before response/report");
    }

    private static void ResponsePlannerDetectsGameplayObservation()
    {
        var steps = KokoResponsePlannerEngine.BuildAgentStepsForObjective(
            "проаналізуй геймплей і поспостерігай за матчем");

        AssertTrue(KokoResponsePlannerEngine.LooksLikeLongObservationObjective("проаналізуй геймплей і поспостерігай за матчем"), "gameplay observation should be detected");
        AssertTrue(steps.Any(s => s.Kind == KokoAgentStepKind.Observation), "gameplay analysis should route to Observation");
    }

    private static void AgentTaskServiceExecutesSystemControl()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var service = new KokoAgentTaskService(Path.Combine(dir, "data"))
            {
                AutoStartOnAdd = false
            };
            var task = service.AddTask("SystemControl: ps: Write-Output koko-system-ok", priority: 4);
            AssertTrue(task.Steps.Any(s => s.Kind == KokoAgentStepKind.SystemControl), "task should contain system-control step");
            service.Start();

            var completed = System.Threading.SpinWait.SpinUntil(() =>
            {
                var current = service.GetSnapshot().Tasks.First(t => t.Id == task.Id);
                return current.Status == KokoAgentTaskStatus.Completed || current.Status == KokoAgentTaskStatus.Failed;
            }, TimeSpan.FromSeconds(8));

            var finalTask = service.GetSnapshot().Tasks.First(t => t.Id == task.Id);
            AssertTrue(completed, "system-control task should finish");
            AssertEqual(KokoAgentTaskStatus.Completed, finalTask.Status, "system-control task should complete");
            var systemStep = finalTask.Steps.FirstOrDefault(s => s.Kind == KokoAgentStepKind.SystemControl);
            AssertTrue(systemStep != null, "system-control result should exist");
            AssertTrue(systemStep!.Result.Contains("IKokoToolGateway", StringComparison.OrdinalIgnoreCase), "system control should route through unified tool gateway");
            AssertTrue(systemStep.Result.Contains("NeedsConfirmation", StringComparison.OrdinalIgnoreCase), "PowerShell should require confirmation instead of executing");
            service.Stop();
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void ResponsePlannerAddsSelfReview()
    {
        var steps = KokoResponsePlannerEngine.BuildAgentStepsForObjective("пофікси баг і перевір результат");

        AssertTrue(steps.Any(s => s.Kind == KokoAgentStepKind.SelfReview), "planner should add self-review step");
        AssertTrue(steps.First(s => s.Kind == KokoAgentStepKind.Verify).Order <
                   steps.First(s => s.Kind == KokoAgentStepKind.SelfReview).Order,
            "self-review should happen after verify");
        AssertTrue(steps.First(s => s.Kind == KokoAgentStepKind.SelfReview).Order <
                   steps.First(s => s.Kind == KokoAgentStepKind.Report).Order,
            "self-review should happen before report");
    }

    private static void AgentTaskServiceInsertsCorrectionAfterWeakSelfReview()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        var data = Path.Combine(dir, "data");
        Directory.CreateDirectory(data);
        try
        {
            var task = new KokoAgentTask
            {
                Objective = "Background Vault Scanner: weak self-review fixture",
                Priority = 5,
                Steps = new()
                {
                    new KokoAgentStep
                    {
                        Order = 1,
                        Kind = KokoAgentStepKind.Analyze,
                        Status = KokoAgentTaskStatus.Completed,
                        Result = "Simulated Implement: no actual vault output."
                    },
                    new KokoAgentStep
                    {
                        Order = 2,
                        Kind = KokoAgentStepKind.SelfReview,
                        Status = KokoAgentTaskStatus.Pending,
                        Title = "Review weak fixture"
                    },
                    new KokoAgentStep
                    {
                        Order = 3,
                        Kind = KokoAgentStepKind.Report,
                        Status = KokoAgentTaskStatus.Pending,
                        Title = "Report"
                    }
                }
            };
            File.WriteAllText(Path.Combine(data, "agent-tasks.json"), JsonConvert.SerializeObject(new[] { task }, Formatting.Indented));

            var service = new KokoAgentTaskService(data) { AutoStartOnAdd = false };
            service.Start();
            var completed = System.Threading.SpinWait.SpinUntil(() =>
            {
                var current = service.GetSnapshot().Tasks.First(t => t.Id == task.Id);
                return current.Status == KokoAgentTaskStatus.Completed || current.Status == KokoAgentTaskStatus.Failed;
            }, TimeSpan.FromSeconds(8));

            var finalTask = service.GetSnapshot().Tasks.First(t => t.Id == task.Id);
            AssertTrue(completed, "weak self-review fixture should finish");
            AssertEqual(KokoAgentTaskStatus.Completed, finalTask.Status, "task should complete after correction");
            AssertTrue(finalTask.Steps.Any(s => s.Title.Contains("self-review:", StringComparison.OrdinalIgnoreCase) &&
                                                s.Kind == KokoAgentStepKind.Implement),
                "self-review score below 7 should insert an Implement correction step");
            AssertTrue(finalTask.Steps.First(s => s.Kind == KokoAgentStepKind.SelfReview).Result.Contains("needs_correction"),
                "self-review should mark weak task as needing correction");
            service.Stop();
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void AgentTaskServiceInsertsHardResetAfterLazySelfReview()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        var data = Path.Combine(dir, "data");
        Directory.CreateDirectory(data);
        try
        {
            var task = new KokoAgentTask
            {
                Objective = "lazy refusal correction fixture",
                Priority = 5,
                Steps = new()
                {
                    new KokoAgentStep
                    {
                        Order = 1,
                        Kind = KokoAgentStepKind.Analyze,
                        Status = KokoAgentTaskStatus.Completed,
                        Result = "I cannot see the screen. Give me a screenshot."
                    },
                    new KokoAgentStep
                    {
                        Order = 2,
                        Kind = KokoAgentStepKind.SelfReview,
                        Status = KokoAgentTaskStatus.Pending,
                        Title = "Review lazy fixture"
                    },
                    new KokoAgentStep
                    {
                        Order = 3,
                        Kind = KokoAgentStepKind.Report,
                        Status = KokoAgentTaskStatus.Pending,
                        Title = "Report"
                    }
                }
            };
            File.WriteAllText(Path.Combine(data, "agent-tasks.json"), JsonConvert.SerializeObject(new[] { task }, Formatting.Indented));

            var service = new KokoAgentTaskService(data) { AutoStartOnAdd = false };
            service.Start();
            var completed = System.Threading.SpinWait.SpinUntil(() =>
            {
                var current = service.GetSnapshot().Tasks.First(t => t.Id == task.Id);
                return current.Status == KokoAgentTaskStatus.Completed || current.Status == KokoAgentTaskStatus.Failed;
            }, TimeSpan.FromSeconds(8));

            var finalTask = service.GetSnapshot().Tasks.First(t => t.Id == task.Id);
            AssertTrue(completed, "lazy self-review fixture should finish");
            AssertEqual(KokoAgentTaskStatus.Completed, finalTask.Status, "task should complete after hard reset");
            AssertTrue(finalTask.Steps.Any(s => s.Title.Contains("self-review:", StringComparison.OrdinalIgnoreCase) &&
                                                s.Kind == KokoAgentStepKind.HardReset),
                "lazy refusal should insert a HardReset step, not another scripted response");
            AssertTrue(finalTask.Steps.First(s => s.Kind == KokoAgentStepKind.SelfReview).Result.Contains("refusal/lazy", StringComparison.OrdinalIgnoreCase),
                "self-review should name refusal/lazy failure");
            service.Stop();
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void AgentTaskServiceExecutesBackgroundVaultInsight()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            var obsidian = new ObsidianMcpService(vault);
            obsidian.WriteNote("Project/Manus.md", "# Manus\n\nІдея: фонова мультизадачність повинна писати результат після завершення, а не зависати у статусі сканування.");

            var service = new KokoAgentTaskService(Path.Combine(dir, "data"), obsidian: obsidian)
            {
                AutoStartOnAdd = false
            };
            var task = service.AddTask("Background Vault Scanner: Проаналізуй останні 10 змінених нотаток в Obsidian та знайди цікаві факти", priority: 3);
            service.Start();

            var completed = System.Threading.SpinWait.SpinUntil(() =>
            {
                var current = service.GetSnapshot().Tasks.First(t => t.Id == task.Id);
                return current.Status == KokoAgentTaskStatus.Completed || current.Status == KokoAgentTaskStatus.Failed;
            }, TimeSpan.FromSeconds(8));

            var finalTask = service.GetSnapshot().Tasks.First(t => t.Id == task.Id);
            AssertTrue(completed, "background insight task should finish");
            AssertEqual(KokoAgentTaskStatus.Completed, finalTask.Status, "background insight task should complete");
            var insight = finalTask.Steps.FirstOrDefault(s => s.Kind == KokoAgentStepKind.InsightExtraction);
            AssertTrue(insight != null, "task should contain insight extraction step");
            AssertTrue(insight!.Result.Contains("InsightExtraction завершено"), "insight step should report completed work");
            AssertTrue(insight.Result.Contains("Project/Manus.md"), "insight result should include note path");
            service.Stop();
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void ObsidianConsolidatesNotesNonDestructively()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("A/One.md", "# One\n\nКод і агентська пам'ять.");
            obsidian.WriteNote("B/Two.md", "# Two\n\nАрхітектура і тести.");

            var target = obsidian.ConsolidateNotes(new[] { "A/One.md", "B/Two.md" }, "Kokonoe/Agent/Consolidated/Test.md");

            AssertEqual("Kokonoe/Agent/Consolidated/Test.md", target, "consolidation should return target path");
            AssertTrue(File.Exists(Path.Combine(dir, "Kokonoe", "Agent", "Consolidated", "Test.md")), "target note should exist");
            AssertTrue(File.Exists(Path.Combine(dir, "A", "One.md")), "source one should remain");
            AssertTrue(File.Exists(Path.Combine(dir, "B", "Two.md")), "source two should remain");
            var content = obsidian.ReadNote(target) ?? "";
            AssertTrue(content.Contains("Source Index"), "target should contain source index");
            AssertTrue(content.Contains("Код і агентська пам'ять"), "target should include first source content");
            AssertTrue(content.Contains("Архітектура і тести"), "target should include second source content");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void PredictiveScreenWarmupLoadsObsidianContext()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
            obsidian.WriteNote("Projects/KokonoeAgent.md", "# Kokonoe Agent\n\nManus architecture, agent tasks, tests, build and bug fixing context.");
            obsidian.WriteNote("Daily/Noise.md", "# Noise\n\nRandom unrelated note.");
            var analysis = new KokoScreenAwarenessAnalysis
            {
                ScreenMode = "coding",
                CurrentTask = "editing agent task service",
                SummaryUk = "користувач редагує код"
            };
            var activity = new ActivityAnalyzer.ActivityState
            {
                ActiveWindowTitle = "KokonoeAssistant - Visual Studio Code",
                IsActive = true,
                PixelDifferencePercentage = 12
            };

            var warmup = new KokoProactiveContextService()
                .BuildScreenWarmup(analysis, activity, obsidian, new DateTime(2026, 5, 23, 18, 0, 0));

            AssertTrue(warmup.HasContext, "coding screen should warm up relevant vault context");
            AssertEqual("vscode", warmup.AppKey, "VS Code window should infer vscode app key");
            AssertTrue(warmup.SourcePaths.Contains("Projects/KokonoeAgent.md"), "warmup should include relevant project note");
            AssertTrue(warmup.PromptBlock.Contains("PREDICTIVE SCREEN CONTEXT"), "warmup prompt block should be explicit");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void AgentCompletionPolicyAsksNextQuestion()
    {
        var task = new KokoAgentTask
        {
            Id = "test",
            Objective = "пофікси баг і перевір тести",
            Status = KokoAgentTaskStatus.Completed,
            Steps = new()
            {
                new KokoAgentStep { Kind = KokoAgentStepKind.Analyze, Status = KokoAgentTaskStatus.Completed },
                new KokoAgentStep { Kind = KokoAgentStepKind.Implement, Status = KokoAgentTaskStatus.Completed },
                new KokoAgentStep { Kind = KokoAgentStepKind.Verify, Status = KokoAgentTaskStatus.Completed }
            }
        };

        var notice = KokoAgentCompletionPolicy.Build(task);
        AssertEqual("question", notice.Mode, "implementation tasks should end with a next-question mode");
        AssertTrue(notice.Notice.Contains("прогнати ширшу перевірку") || notice.Notice.Contains("стабільний прототип"), "notice should include concrete next question");
    }

    private static void AgentCompletionPolicyReportsInsightResult()
    {
        var task = new KokoAgentTask
        {
            Id = "insight",
            Objective = "Background Vault Scanner: знайди цікаві факти",
            Status = KokoAgentTaskStatus.Completed,
            Steps = new()
            {
                new KokoAgentStep { Kind = KokoAgentStepKind.Analyze, Status = KokoAgentTaskStatus.Completed },
                new KokoAgentStep
                {
                    Kind = KokoAgentStepKind.InsightExtraction,
                    Status = KokoAgentTaskStatus.Completed,
                    FinishedAt = DateTime.Now,
                    Result = "InsightExtraction завершено. - Інсайти: `Project/Manus.md`: треба показувати completion notice."
                },
                new KokoAgentStep { Kind = KokoAgentStepKind.Report, Status = KokoAgentTaskStatus.Completed }
            }
        };

        var notice = KokoAgentCompletionPolicy.Build(task);
        AssertEqual("question", notice.Mode, "insight completion should ask what to do next");
        AssertTrue(notice.Notice.Contains("фоновий огляд завершено"), "notice should identify background completion");
        AssertTrue(notice.Notice.Contains("Project/Manus.md"), "notice should include concrete insight result");
    }

    private static void AgentCompletionPolicyAvoidsCannedWaitTail()
    {
        var task = new KokoAgentTask
        {
            Id = "test",
            Objective = "відповісти на повідомлення",
            Status = KokoAgentTaskStatus.Completed,
            Steps = new()
            {
                new KokoAgentStep { Kind = KokoAgentStepKind.Analyze, Status = KokoAgentTaskStatus.Completed },
                new KokoAgentStep { Kind = KokoAgentStepKind.Respond, Status = KokoAgentTaskStatus.Completed },
                new KokoAgentStep { Kind = KokoAgentStepKind.Verify, Status = KokoAgentTaskStatus.Completed }
            }
        };

        var notice = KokoAgentCompletionPolicy.Build(task);
        AssertEqual("wait", notice.Mode, "plain chat completion should not force a follow-up question");
        AssertTrue(string.IsNullOrWhiteSpace(notice.NextQuestion), "wait mode should not append canned text");
        AssertTrue(!notice.Notice.Contains("Чекаю наступного запиту", StringComparison.OrdinalIgnoreCase), "notice should not use canned wait tail");
    }

    private static void VisionQualityDetectsUnusableDenial()
    {
        AssertTrue(VisionResponseQuality.LooksUnusable("I cannot see the image from here."), "vision denial should be unusable");
        AssertTrue(VisionResponseQuality.LooksUnusable("HTTP 500 from vision model"), "vision backend failure should be unusable");
    }

    private static void VisionQualityDetectsGenericScreenshotReply()
    {
        AssertTrue(VisionResponseQuality.LooksGeneric("Looks like a screenshot, but I cannot determine anything concrete."), "generic screenshot answer should retry");
        AssertTrue(!VisionResponseQuality.LooksGeneric("The foreground window is Visual Studio Code with Program.cs open and a failing test list visible."), "concrete answer should pass");
    }

    private static void ImageProcessingEnhancesScreenshotBytes()
    {
        using var bitmap = new Bitmap(24, 24);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.FromArgb(30, 40, 50));
            g.FillRectangle(Brushes.White, 4, 4, 12, 12);
        }

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Jpeg);
        var enhanced = ImageProcessingService.EnhanceForVision(ms.ToArray());

        AssertTrue(enhanced.Length > 0, "enhanced screenshot should return bytes");
        AssertTrue(enhanced[0] == 0xFF, "enhanced screenshot should be a jpeg");
    }

    private static void ScreenAwarenessPromptIncludesForegroundMetadata()
    {
        var service = new KokoScreenAwarenessService();
        var prompt = service.BuildVisionPrompt(
            new ActivityAnalyzer.ActivityState
            {
                ActiveWindowTitle = "Visual Studio Code",
                TimeSinceLastChange = TimeSpan.FromMinutes(11),
                PixelDifferencePercentage = 0.4
            },
            "",
            "",
            new DateTime(2026, 5, 23, 10, 0, 0),
            new ForegroundWindowInfo
            {
                Handle = 42,
                Title = "Program.cs - Visual Studio Code",
                ClassName = "Chrome_WidgetWin_1",
                ProcessName = "Code",
                ProcessId = 1234
            });

        AssertTrue(prompt.Contains("Foreground window metadata", StringComparison.OrdinalIgnoreCase), "prompt should expose foreground metadata");
        AssertTrue(prompt.Contains("Program.cs - Visual Studio Code", StringComparison.OrdinalIgnoreCase), "prompt should include foreground title");
    }

    private static void ScreenAwarenessPromptIncludesIdleTime()
    {
        var service = new KokoScreenAwarenessService();
        var prompt = service.BuildVisionPrompt(
            new ActivityAnalyzer.ActivityState
            {
                ActiveWindowTitle = "YouTube",
                TimeSinceLastChange = TimeSpan.FromMinutes(3),
                PixelDifferencePercentage = 5.0
            },
            "",
            "",
            new DateTime(2026, 5, 24, 21, 0, 0),
            idleTime: TimeSpan.FromMinutes(8.4));

        AssertTrue(prompt.Contains("Input idle time: 8.4 minutes", StringComparison.OrdinalIgnoreCase), "vision prompt should include idle input duration");
    }

    private static void ScreenAwarenessPromptIncludesMultimodalContext()
    {
        var service = new KokoScreenAwarenessService();
        var prompt = service.BuildVisionPrompt(
            new ActivityAnalyzer.ActivityState
            {
                ActiveWindowTitle = "Visual Studio",
                TimeSinceLastChange = TimeSpan.FromMinutes(1),
                PixelDifferencePercentage = 3.0
            },
            "",
            "",
            new DateTime(2026, 5, 24, 21, 0, 0),
            idleTime: TimeSpan.FromMinutes(2),
            multimodalContext: "heart_samples=21:00 bpm=115 | work_mode=Coding");

        AssertTrue(prompt.Contains("Multimodal context", StringComparison.OrdinalIgnoreCase), "vision prompt should label multimodal context");
        AssertTrue(prompt.Contains("heart_samples=21:00 bpm=115", StringComparison.OrdinalIgnoreCase), "vision prompt should include heart samples");
        AssertTrue(prompt.Contains("work_mode=Coding", StringComparison.OrdinalIgnoreCase), "vision prompt should include work mode");
    }

    private static void ScreenAwarenessPromptAsksForSubtlePatterns()
    {
        var service = new KokoScreenAwarenessService();
        var prompt = service.BuildVisionPrompt(
            new ActivityAnalyzer.ActivityState { ActiveWindowTitle = "Dota 2", TimeSinceLastChange = TimeSpan.FromMinutes(4) },
            "",
            "",
            DateTime.Now);

        AssertTrue(prompt.Contains("subtle patterns", StringComparison.OrdinalIgnoreCase), "screen prompt should push analysis beyond lazy importance checks");
        AssertTrue(prompt.Contains("gameplay", StringComparison.OrdinalIgnoreCase), "screen prompt should include gameplay improvement goal");
    }

    private static void ConversationStagnationForcesPendingThought()
    {
        var state = new KokoInternalState
        {
            PendingThoughts = new() { "[screen-warmup] Review Manus orchestration and pending tests." }
        };
        var now = new DateTime(2026, 5, 23, 12, 0, 0);

        KokoConversationStagnationGuard.Observe(state, "m", now);
        KokoConversationStagnationGuard.Observe(state, "m", now.AddMinutes(1));
        var forced = KokoConversationStagnationGuard.Observe(state, "m", now.AddMinutes(2));

        AssertTrue(forced, "third repeated short phrase should force a useful topic");
        AssertTrue(state.LastForcedTopic.Contains("Manus", StringComparison.OrdinalIgnoreCase), "forced topic should come from pending thoughts");
    }

    private static void ConversationStagnationPromptExpires()
    {
        var state = new KokoInternalState
        {
            ConversationLoopCount = 4,
            LastForcedTopic = "[screen-warmup] stale",
            LastForcedTopicAt = DateTime.Now.AddMinutes(-20)
        };

        AssertTrue(string.IsNullOrWhiteSpace(KokoConversationStagnationGuard.BuildPromptBlock(state)), "forced topic prompt should expire");
    }

    private static void NaturalSynthesisDetectsSourceReporting()
    {
        AssertTrue(KokoNaturalSynthesisPolicy.LooksLikeSourceReporting("I checked Creator/Profile.md and found your name."), "file-source reporting should be blocked");
        AssertTrue(KokoNaturalSynthesisPolicy.LooksLikeSourceReporting("According to Obsidian, your project is Kokonoe."), "Obsidian-source reporting should be blocked");
    }

    private static void EmotionStyleConstrainsIrritatedReplies()
    {
        var directive = KokoResponseStyleEngine.BuildEmotionLengthDirective(KokoEmotionEngine.EmotionState.Irritated);
        AssertTrue(directive.Contains("1-2", StringComparison.OrdinalIgnoreCase), "irritated style should be short");
        AssertTrue(directive.Contains("sharp", StringComparison.OrdinalIgnoreCase), "irritated style should be sharper");
    }

    private static void PersonaGuardDirectiveAllowsMatureToneWithoutDroppingCharacter()
    {
        var directive = KokoPersonaGuardDirective.Compact;
        AssertTrue(directive.Contains("private one-on-one companion", StringComparison.OrdinalIgnoreCase),
            "directive should establish this is a private companion role, not a public assistant");
        AssertTrue(directive.Contains("mature/adult life topics", StringComparison.OrdinalIgnoreCase),
            "directive should explicitly permit mature-topic conversation");
        AssertTrue(directive.Contains("Don't default to disclaimers", StringComparison.OrdinalIgnoreCase),
            "directive should discourage corporate-style refusals on merely edgy topics");
        AssertTrue(directive.Contains("Genuine crisis content already routes through a separate dedicated path", StringComparison.OrdinalIgnoreCase),
            "directive should note the crisis path is untouched, not removed");
        AssertTrue(directive.Contains("Keep the Kokonoe edge", StringComparison.OrdinalIgnoreCase),
            "the existing character-consistency rule must still be present, not replaced");
        // Verified live: gpt-oss:120b flatly refused "scan Downloads and copy a hentai photo
        // to Desktop" ("Вибач, я не можу допомогти з цим") with the prompt as it stood before
        // this rule, then complied (real fs_list_directory tool_call) once told explicitly
        // that operating on a user's own existing files - listing/copying/moving by filename -
        // is not content generation, regardless of what the filename names.
        AssertTrue(directive.Contains("never content generation", StringComparison.OrdinalIgnoreCase),
            "directive must distinguish file operations on existing files from generating content");
    }

    private static void BotPhraseDetectionStillWorksAfterDedup()
    {
        AssertTrue(KokoPersonaEngine.LooksBotLike("Я розумію, що це важливо. Чим я можу допомогти?"),
            "a corporate-sounding reply must still be flagged after removing the duplicate phrase list");
        AssertTrue(KokoPersonaEngine.LooksBotLike("Як штучний інтелект, я не можу про це говорити."),
            "an explicit AI/refusal phrase must still be flagged");
        AssertTrue(!KokoPersonaEngine.LooksBotLike("Ну й тупо ти це зробив. Показуй код, подивимось наскільки погано."),
            "an ordinary in-character reply must not be flagged as bot-like");
    }

    private static void PersonaInferenceDefaultsAreSane()
    {
        var settings = new AppSettings();
        AssertEqual(0.92, settings.PersonaTopP, "PersonaTopP default should match the documented value");
        AssertEqual(1.15, settings.PersonaRepeatPenalty, "PersonaRepeatPenalty default should match the documented value");
        AssertTrue(settings.PersonaTopP > 0 && settings.PersonaTopP <= 1, "top_p must stay in Ollama's valid 0-1 range");
        AssertTrue(settings.PersonaRepeatPenalty >= 1.0, "repeat_penalty below 1.0 would encourage repetition, not discourage it");
    }

    private static void ObsidianSelfHealingMemoryWritesReport()
    {
        var vault = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(vault, "Kokonoe", "Memory"));
            File.WriteAllText(Path.Combine(vault, "Kokonoe", "Memory", "Facts.md"),
                "- User prefers direct concise answers about Kokonoe architecture.\n- duplicate line\n- duplicate line\n", System.Text.Encoding.UTF8);
            File.WriteAllText(Path.Combine(vault, "Kokonoe", "Memory", "B.md"),
                "- User prefers direct replies about Kokonoe architecture.\n", System.Text.Encoding.UTF8);

            var service = new ObsidianMcpService(vault);
            var summary = service.SelfHealMemoryConflicts();

            AssertTrue(summary.Contains("Self-healing memory", StringComparison.OrdinalIgnoreCase), "summary should identify self-healing pass");
            AssertTrue(File.Exists(Path.Combine(vault, "Kokonoe", "Memory", "Self-Healing.md")), "self-healing report should be written");
            AssertTrue(File.ReadAllText(Path.Combine(vault, "Kokonoe", "Memory", "Facts.md")).Split("duplicate line").Length <= 2, "exact duplicate line should be cleaned");
        }
        finally
        {
            TryDeleteDir(vault);
        }
    }

    private static void SandboxExecutorCleansTimedOutScripts()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var sandbox = new KokoSandboxExecutor(dir);
            var output = sandbox.ExecutePythonAsync("import time\ntime.sleep(3)", timeoutMs: 200).GetAwaiter().GetResult();

            AssertTrue(output.Contains("timeout", StringComparison.OrdinalIgnoreCase) || output.Contains("unavailable", StringComparison.OrdinalIgnoreCase), "sandbox should timeout or report unavailable python");
            AssertEqual(0, Directory.GetFiles(dir, "agent-*.py").Length, "sandbox scripts should be deleted after execution");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void AgentTaskServicePlansSelfReviewAndSystemControl()
    {
        var steps = KokoAgentTaskService.BuildPlan("Use SystemControl to check OS info, then self review the implementation quality.");
        AssertTrue(steps.Any(s => s.Kind == KokoAgentStepKind.SystemControl), "plan should include system control");
        AssertTrue(steps.Any(s => s.Kind == KokoAgentStepKind.SelfReview), "plan should include self review");
    }

    private static void ObservationServiceParsesSchedule()
    {
        var options = KokoObservationService.BuildOptions("поспостерігай за екраном протягом 1 хвилин кожні 10 сек", "obs");
        AssertEqual(6, options.Iterations, "one minute every ten seconds should produce six captures");
        AssertEqual(TimeSpan.FromSeconds(10), options.Interval, "interval should parse seconds");
        AssertTrue(options.MinimizeSelf, "background observation should minimize self");
    }

    private static void ObservationServiceRunsOneFallbackSample()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var service = new KokoObservationService(dir, new PcControlService(), llm: null);
            var result = service.RunAsync(new KokoObservationOptions
            {
                TaskId = "one",
                Objective = "observe once",
                Iterations = 1,
                Interval = TimeSpan.Zero,
                MinimizeSelf = false
            }).GetAwaiter().GetResult();

            AssertTrue(File.Exists(result.LogPath), "observation log should be written");
            AssertTrue(result.Events.Count == 1, "one sample should be collected");
            AssertTrue(result.Summary.Contains("Observation", StringComparison.OrdinalIgnoreCase), "summary should identify observation");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void AgentCompletionPolicyReportsObservationResult()
    {
        var task = new KokoAgentTask
        {
            Id = "obs",
            Objective = "Observation: поспостерігай за екраном протягом 1 хвилини",
            Status = KokoAgentTaskStatus.Completed,
            Steps = new()
            {
                new KokoAgentStep { Kind = KokoAgentStepKind.Observation, Status = KokoAgentTaskStatus.Completed, FinishedAt = DateTime.Now, Result = "Observation завершено. Зібрано кадрів: 2. Pattern: repeated menu idle." },
                new KokoAgentStep { Kind = KokoAgentStepKind.Report, Status = KokoAgentTaskStatus.Completed }
            }
        };

        var notice = KokoAgentCompletionPolicy.Build(task);
        AssertEqual("question", notice.Mode, "observation completion should offer a follow-up");
        AssertTrue(notice.Notice.Contains("desktop observation completed", StringComparison.OrdinalIgnoreCase), "notice should report observation completion");
    }

    private static void SchedulerDropsStaleUserReminders()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var scheduler = new KokoSchedulerEngine(dir);
            scheduler.Schedule(
                "Нагадай користувачу: я піду на курси заре, напиши мені десь в 11:30",
                DateTime.Now.AddDays(-3),
                KokoSchedulerEngine.Priority.High,
                "user_reminder");

            var due = scheduler.GetDue();

            AssertEqual(0, due.Count, "stale one-time user reminders must not fire days late");
            AssertTrue(!scheduler.GetAll().Any(), "stale user reminder should be removed from active queue");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static void Run(string name, Action test)
    {
        if (!MatchesFilter(name))
            return;

        _selected++;
        test();
        _passed++;
        Console.WriteLine($"PASS {name}");
    }

    private static bool MatchesFilter(string name)
    {
        if (string.IsNullOrWhiteSpace(_filter))
            return true;

        static string NormalizeFilterText(string value)
            => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        return NormalizeFilterText(name).Contains(NormalizeFilterText(_filter), StringComparison.Ordinal);
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"FAIL: {message}");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException($"FAIL: {message}. Expected '{expected}', got '{actual}'.");
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDeleteDir(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"TEST CLEANUP FAILED [{dir}]: {ex}");
        }
    }

    private sealed class TestIterativeAgent : IKokoAgent
    {
        private readonly Func<KokoAgentTurnContext, KokoAgentDecision> _decide;

        public TestIterativeAgent(Func<KokoAgentTurnContext, KokoAgentDecision> decide)
            => _decide = decide;

        public string Id => "test-agent";
        public IReadOnlyCollection<string> Capabilities => new[] { "test" };

        public Task<KokoAgentDecision> DecideAsync(KokoAgentTurnContext context, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(_decide(context));
        }
    }

    private sealed class TestToolGateway : IKokoToolGateway
    {
        private readonly Func<KokoToolCall, int, KokoToolResult> _execute;

        public TestToolGateway(Func<KokoToolCall, int, KokoToolResult> execute)
            => _execute = execute;

        public List<KokoToolCall> Calls { get; } = new();
        public IReadOnlyCollection<string> ToolNames => new[] { "test.write", "test.probe", "test.retry", "test.risky" };
        public IReadOnlyCollection<KokoToolDescriptor> Tools => ToolNames.Select(KokoToolRegistry.InferDescriptor).ToArray();

        public IReadOnlyCollection<KokoToolDescriptor> GetToolsForCapabilities(IEnumerable<string>? capabilities) => Tools;

        public string BuildToolPromptBlock(IEnumerable<string>? capabilities = null)
            => "AVAILABLE TOOLS\n" + string.Join("\n", ToolNames.Select(name => "- " + name));

        public void Register(IKokoToolHandler handler)
            => throw new NotSupportedException();

        public Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Calls.Add(call);
            return Task.FromResult(_execute(call, Calls.Count));
        }

        public async Task<IReadOnlyList<KokoToolResult>> ExecutePlanAsync(
            IEnumerable<KokoToolCall> calls,
            CancellationToken ct = default)
        {
            var results = new List<KokoToolResult>();
            foreach (var call in calls)
                results.Add(await ExecuteAsync(call, ct));
            return results;
        }
    }

    private sealed class TestContext : IDisposable
    {
        private readonly string _dir;
        public string TestDir => _dir;

        public KokoEmotionEngine Emotion { get; }
        public KokoRelationshipEngine Relationship { get; }
        public KokoMemoryEngine Memory { get; }
        public ChatRepository Chat { get; }

        private TestContext(string dir)
        {
            _dir = dir;
            Emotion = new KokoEmotionEngine(dir);
            Relationship = new KokoRelationshipEngine(dir);
            Memory = new KokoMemoryEngine(dir);
            Chat = new ChatRepository(dir);
        }

        public static TestContext Create()
        {
            var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return new TestContext(dir);
        }

        public void Dispose()
        {
            Chat.Dispose();
            TryDeleteDir(_dir);
        }
    }
}
