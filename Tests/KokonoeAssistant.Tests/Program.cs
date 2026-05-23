using System;
using System.IO;
using KokonoeAssistant.Services;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Tests;

internal static class Program
{
    private static int _passed;

    private static int Main()
    {
        try
        {
            Run("Somatic wired from pulse spike", SomaticWiredFromPulseSpike);
            Run("Somatic tired from low charge", SomaticTiredFromLowCharge);
            Run("Self regulation clamps pulse spike", SelfRegulationClampsPulseSpike);
            Run("Self regulation protects vulnerable tone", SelfRegulationProtectsVulnerableTone);
            Run("Initiative respects low-power silence", InitiativeRespectsLowPowerSilence);
            Run("Initiative reacts to protective override", InitiativeReactsToProtectiveOverride);
            Run("Initiative high autonomy asks curiosity sooner", InitiativeHighAutonomyAsksCuriositySooner);
            Run("Short term intent followup bypasses ordinary initiative", ShortTermIntentFollowupBypassesOrdinaryInitiative);
            Run("Presence detects overdue course followup", PresenceDetectsOverdueCourseFollowup);
            Run("Presence waits for return home intent", PresenceWaitsForReturnHomeIntent);
            Run("Autonomy blocks generic ping during active intent", AutonomyBlocksGenericPingDuringActiveIntent);
            Run("Presence refuses stale sleep instruction after return", PresenceRefusesStaleSleepInstructionAfterReturn);
            Run("Presence never interrupts active sleep intent", PresenceNeverInterruptsActiveSleepIntent);
            Run("Sleep intent at night resolves to morning", SleepIntentAtNightResolvesToMorning);
            Run("State freshness expires stale sleep intent", StateFreshnessExpiresStaleSleepIntent);
            Run("State freshness closes intent on wake signal", StateFreshnessClosesIntentOnWakeSignal);
            Run("State freshness closes stale course on later activity", StateFreshnessClosesStaleCourseOnLaterActivity);
            Run("Vault sync policy flushes stale partial batch", VaultSyncPolicyFlushesStalePartialBatch);
            Run("Presence long silence can interrupt on high autonomy", PresenceLongSilenceCanInterruptOnHighAutonomy);
            Run("Internal day shifts phase and writes vault status", InternalDayShiftsPhaseAndWritesVaultStatus);
            Run("Internal day prefers silence at low power night", InternalDayPrefersSilenceAtLowPowerNight);
            Run("Autonomy pipeline gates weak initiative in quiet night", AutonomyPipelineGatesWeakInitiativeInQuietNight);
            Run("Relationship records shift events", RelationshipRecordsShiftEvents);
            Run("Pattern rhythm profile recommends quiet slot", PatternRhythmProfileRecommendsQuietSlot);
            Run("Self review blocks stale sleep replies", SelfReviewBlocksStaleSleepReplies);
            Run("Timeline summarizes returned state", TimelineSummarizesReturnedState);
            Run("Post reply guard blocks stale sleep", PostReplyGuardBlocksStaleSleep);
            Run("Post reply guard blocks stale food claim after ate", PostReplyGuardBlocksStaleFoodClaimAfterAte);
            Run("Post reply guard blocks hibernation framing after slept", PostReplyGuardBlocksHibernationFramingAfterSlept);
            Run("Post reply guard rejects staged decoration", PostReplyGuardRejectsStagedDecoration);
            Run("Post reply guard blocks duplicate replies", PostReplyGuardBlocksDuplicateReplies);
            Run("Screen intent detects natural screen scan requests", ScreenIntentDetectsNaturalScreenScanRequests);
            Run("Post reply guard allows repeated screen scan command", PostReplyGuardAllowsRepeatedScreenScanCommand);
            Run("Post reply guard rejects screen capability denial", PostReplyGuardRejectsScreenCapabilityDenial);
            Run("Post reply guard protects short affection", PostReplyGuardProtectsShortAffection);
            Run("Post reply guard protects short greeting", PostReplyGuardProtectsShortGreeting);
            Run("Post reply guard blocks repeated fallback loop", PostReplyGuardBlocksRepeatedFallbackLoop);
            Run("Post reply guard rejects dotted garbage", PostReplyGuardRejectsDottedGarbage);
            Run("PC intent router routes safe OS commands", PcIntentRouterRoutesSafeOsCommands);
            Run("PC intent router separates screen and destructive commands", PcIntentRouterSeparatesScreenAndDestructiveCommands);
            Run("PC control executes safe PowerShell", PcControlExecutesSafePowerShell);
            Run("PC control captures screenshot", PcControlCapturesScreenshot);
            Run("LLM vision compacts oversized screenshots", LlmVisionCompactsOversizedScreenshots);
            Run("Post reply guard repairs vision technical error", PostReplyGuardRepairsVisionTechnicalError);
            Run("Post reply guard protects image only prompt", PostReplyGuardProtectsImageOnlyPrompt);
            Run("Post reply guard duplicate image prompt avoids stale repeat text", PostReplyGuardDuplicateImagePromptAvoidsStaleRepeatText);
            Run("Post reply guard softens one-letter Telegram ambiguity", PostReplyGuardSoftensOneLetterTelegramAmbiguity);
            Run("Post reply guard prioritizes image caption over stale one-letter context", PostReplyGuardPrioritizesImageCaptionOverStaleOneLetterContext);
            Run("Post reply guard repairs blamey answer explanation", PostReplyGuardRepairsBlameyAnswerExplanation);
            Run("Post reply guard blocks assistant-owned reminder schedule", PostReplyGuardBlocksAssistantOwnedReminderSchedule);
            Run("Post reply guard blocks follow-up after conversation close", PostReplyGuardBlocksFollowupAfterConversationClose);
            Run("Post reply guard protects short apology", PostReplyGuardProtectsShortApology);
            Run("Post reply guard blocks therapy meta tone", PostReplyGuardBlocksTherapyMetaTone);
            Run("Post reply guard blocks fabricated external facts", PostReplyGuardBlocksFabricatedExternalFacts);
            Run("Post reply guard blocks stale proactive ping on direct topic", PostReplyGuardBlocksStaleProactivePingOnDirectTopic);
            Run("Post reply guard blocks service bot tone", PostReplyGuardBlocksServiceBotTone);
            Run("Post reply guard blocks punitive network threat", PostReplyGuardBlocksPunitiveNetworkThreat);
            Run("Post reply guard blocks blind agreement", PostReplyGuardBlocksBlindAgreement);
            Run("Response planner classifies critical assistant architecture", ResponsePlannerClassifiesCriticalAssistantArchitecture);
            Run("Response planner requires vault read for memory questions", ResponsePlannerRequiresVaultReadForMemoryQuestions);
            Run("Response planner routes Obsidian scan profile questions", ResponsePlannerRoutesObsidianScanProfileQuestions);
            Run("Response planner routes identity alias questions to vault", ResponsePlannerRoutesIdentityAliasQuestionsToVault);
            Run("Response planner routes natural screen questions", ResponsePlannerRoutesNaturalScreenQuestions);
            Run("Memory policy stores stable preference", MemoryPolicyStoresStablePreference);
            Run("Memory policy keeps temporary state out of beliefs", MemoryPolicyKeepsTemporaryStateOutOfBeliefs);
            Run("Continuity reinforces repeated belief", ContinuityReinforcesRepeatedBelief);
            Run("Post reply guard repairs sleep leak on profile question", PostReplyGuardRepairsSleepLeakOnProfileQuestion);
            Run("Post reply guard rejects profile quote fallback", PostReplyGuardRejectsProfileQuoteFallback);
            Run("Post reply guard rejects scripted profile source report", PostReplyGuardRejectsScriptedProfileSourceReport);
            Run("Post reply guard blocks false vault unavailable on identity query", PostReplyGuardBlocksFalseVaultUnavailableOnIdentityQuery);
            Run("Post reply guard blocks false vault unavailable on Obsidian exploration", PostReplyGuardBlocksFalseVaultUnavailableOnObsidianExploration);
            Run("Post reply guard blocks Obsidian pseudo progress", PostReplyGuardBlocksObsidianPseudoProgress);
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
            Run("Short acknowledgement resolves stale intent", ShortAcknowledgementResolvesStaleIntent);
            Run("Screen awareness parses vision JSON", ScreenAwarenessParsesVisionJson);
            Run("Screen awareness suppresses repeated comments", ScreenAwarenessSuppressesRepeatedComments);
            Run("Screen awareness redacts private identifiers", ScreenAwarenessRedactsPrivateIdentifiers);
            Run("Screen awareness allows rare passive jab", ScreenAwarenessAllowsRarePassiveJab);
            Run("Screen awareness lets jab bypass comment cooldown", ScreenAwarenessLetsJabBypassCommentCooldown);
            Run("Screen awareness game mode uses active jab cooldown", ScreenAwarenessGameModeUsesActiveJabCooldown);
            Run("Screen awareness blocks sensitive screens", ScreenAwarenessBlocksSensitiveScreens);
            Run("Screen awareness builds situation context", ScreenAwarenessBuildsSituationContext);
            Run("Screen awareness builds aggregate pattern candidate", ScreenAwarenessBuildsAggregatePatternCandidate);
            Run("Startup greeting avoids dead canned replies", StartupGreetingAvoidsDeadCannedReplies);
            Run("Startup greeting sanitizes dry return line", StartupGreetingSanitizesDryReturnLine);
            Run("Startup greeting ignores low signal topic", StartupGreetingIgnoresLowSignalTopic);
            Run("Startup greeting reacts to quick return", StartupGreetingReactsToQuickReturn);
            Run("Startup greeting prompt uses mood and absence", StartupGreetingPromptUsesMoodAndAbsence);
            Run("Startup greeting sanitizes therapy meta", StartupGreetingSanitizesTherapyMeta);
            Run("Startup greeting fallback does not quote raw latest user text", StartupGreetingFallbackDoesNotQuoteRawLatestUserText);
            Run("Scenario simulation guards temporal continuity", ScenarioSimulationGuardsTemporalContinuity);
            Run("LLM diagnostics snapshot starts idle", LlmDiagnosticsSnapshotStartsIdle);
            Run("LLM extracts reasoning content for vision replies", LlmExtractsReasoningContentForVisionReplies);
            Run("LLM uses Ollama image URL string payload", LlmUsesOllamaImageUrlStringPayload);
            Run("LLM visible text rejects dotted garbage", LlmVisibleTextRejectsDottedGarbage);
            Run("LLM rotates Ollama key after auth failure", LlmRotatesOllamaKeyAfterAuthFailure);
            Run("LLM agent key pool exhaustion does not reuse legacy key", LlmAgentKeyPoolExhaustionDoesNotReuseLegacyKey);
            Run("Inspector renders state report", InspectorRendersStateReport);
            Run("Obsidian vault architecture maintenance", ObsidianVaultArchitectureMaintenance);
            Run("Obsidian unique memory append", ObsidianUniqueMemoryAppend);
            Run("Obsidian rejects paths outside vault", ObsidianRejectsPathsOutsideVault);
            Run("LLM vault tool routing avoids accidental writes", LlmVaultToolRoutingAvoidsAccidentalWrites);
            Run("Obsidian memory quality and task queue", ObsidianMemoryQualityAndTaskQueue);
            Run("Obsidian memory duplicate cleanup", ObsidianMemoryDuplicateCleanup);
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
            Run("Agent task service imports Obsidian backlog", AgentTaskServiceImportsObsidianBacklog);
            Run("Response planner adds insight extraction", ResponsePlannerAddsInsightExtraction);
            Run("Response planner adds system control", ResponsePlannerAddsSystemControl);
            Run("Agent task service executes system control", AgentTaskServiceExecutesSystemControl);
            Run("Agent task service executes background vault insight", AgentTaskServiceExecutesBackgroundVaultInsight);
            Run("Agent completion policy asks next question", AgentCompletionPolicyAsksNextQuestion);
            Run("Agent completion policy reports insight result", AgentCompletionPolicyReportsInsightResult);
            Run("Agent completion policy avoids canned wait tail", AgentCompletionPolicyAvoidsCannedWaitTail);
            Run("Scheduler drops stale user reminders", SchedulerDropsStaleUserReminders);
            Run("Reminder parser handles relative and vague time", ReminderParserHandlesRelativeAndVagueTime);
            Run("Reminder parser handles wake and absolute time", ReminderParserHandlesWakeAndAbsoluteTime);

            Console.WriteLine($"PASS {_passed} tests");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
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
            .Evaluate(state, ctx.Chat.GetMessages(10), now, autonomyLevel: 3);

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
            .Evaluate(state, ctx.Chat.GetMessages(10), now, autonomyLevel: 3);

        AssertEqual("active_absence", frame.SituationKind, "return-home intent should stay active before the stated time");
        AssertTrue(!frame.ShouldInterrupt, "presence should not interrupt before the stated return-home follow-up");
        AssertTrue(frame.ToneHint.Contains("wait until that time"), "tone should explicitly avoid early nagging");
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

        AssertEqual("due_intent_followup", frame.SituationKind, "sleep may be due but should stay non-interrupting");
        AssertTrue(!frame.ShouldInterrupt, "sleep intent should never proactively interrupt");
        AssertTrue(frame.ToneHint.Contains("let him sleep") || frame.ToneHint.Contains("do not tell him to sleep"), "tone should preserve sleep quiet rule");
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

    private static void StateFreshnessExpiresStaleSleepIntent()
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

        AssertTrue(result.Changed, "stale sleep should change state");
        AssertTrue(result.ExpiredIntentCount == 1, "stale sleep should expire by time");
        AssertTrue(state.ShortTermIntents.All(i => i.ResolvedAt.HasValue), "expired sleep should no longer be active");
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
            .Evaluate(state, ctx.Chat.GetMessages(10), now, autonomyLevel: 3);

        AssertTrue(frame.ShouldInterrupt, "long silence should be allowed to interrupt at high autonomy after cooldown");
        AssertEqual("long_silence", frame.SituationKind, "seven hours should be long silence");
        AssertEqual("presence_long_silence", frame.Trigger, "long silence should have a presence trigger");
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
        AssertTrue(result.ShouldRepair, "short greeting should be repaired through the model");
        AssertTrue(string.IsNullOrWhiteSpace(result.HardReplacement), "short greeting should not get a canned direct reply");
        AssertTrue(result.Violations.Any(v => v.Contains("привітання")), "violation should mention greeting");
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

    private static void PcControlExecutesSafePowerShell()
    {
        var output = new PcControlService()
            .RunCommandAsync("Write-Output koko-ok", timeoutMs: 5000, enforceSafety: true)
            .GetAwaiter()
            .GetResult();
        AssertTrue(output.Contains("koko-ok", StringComparison.OrdinalIgnoreCase), "safe PowerShell command should execute");
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
        AssertTrue(reply.Contains("Автопінги") || reply.Contains("follow-up"), "reply should confirm mode change");
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

    private static void LlmDiagnosticsSnapshotStartsIdle()
    {
        var diag = new LlmService().GetDiagnosticsSnapshot();

        AssertEqual("idle", diag.Status, "diagnostics should start idle before any request");
        AssertEqual(0L, diag.TotalRequests, "diagnostics should not invent requests");
        AssertTrue(diag.ConsecutiveFailures == 0, "diagnostics should not start in failure state");
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            try { Directory.Delete(parent, recursive: true); } catch { }
        }
    }

    private static void LlmVaultToolRoutingAvoidsAccidentalWrites()
    {
        var method = typeof(LlmService).GetMethod(
            "DetectBestTool",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        AssertTrue(method != null, "DetectBestTool should exist for routing tests");

        string Detect(string text) => (string)method!.Invoke(null, new object[] { text })!;

        AssertEqual("list_notes", Detect("покажи список нотаток у vault"), "note list request should not default to append");
        AssertEqual("list_folders", Detect("покажи список папок"), "folder list request should not create a folder");
        AssertEqual("create_folder", Detect("створи папку Kokonoe/Review"), "folder creation should still create folders when explicitly requested");
        AssertEqual("read_note", Detect("прочитай Kokonoe/Memory/Facts"), "read request should stay read-only");
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            AssertTrue(systemStep!.Result.Contains("koko-system-ok", StringComparison.OrdinalIgnoreCase), "PowerShell output should be captured");
            service.Stop();
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            try { Directory.Delete(dir, recursive: true); } catch { }
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
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    private static void Run(string name, Action test)
    {
        test();
        _passed++;
        Console.WriteLine($"PASS {name}");
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
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }
    }
}
