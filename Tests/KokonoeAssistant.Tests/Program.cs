using System;
using System.IO;
using KokonoeAssistant.Services;

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
            Run("Post reply guard allows repeated screen scan command", PostReplyGuardAllowsRepeatedScreenScanCommand);
            Run("Post reply guard protects short affection", PostReplyGuardProtectsShortAffection);
            Run("Post reply guard protects short greeting", PostReplyGuardProtectsShortGreeting);
            Run("Post reply guard blocks repeated fallback loop", PostReplyGuardBlocksRepeatedFallbackLoop);
            Run("Post reply guard replaces vision technical error", PostReplyGuardReplacesVisionTechnicalError);
            Run("Post reply guard protects image only prompt", PostReplyGuardProtectsImageOnlyPrompt);
            Run("Post reply guard duplicate image prompt avoids stale repeat text", PostReplyGuardDuplicateImagePromptAvoidsStaleRepeatText);
            Run("Post reply guard blocks therapy meta tone", PostReplyGuardBlocksTherapyMetaTone);
            Run("Post reply guard blocks fabricated external facts", PostReplyGuardBlocksFabricatedExternalFacts);
            Run("Post reply guard blocks stale proactive ping on direct topic", PostReplyGuardBlocksStaleProactivePingOnDirectTopic);
            Run("Post reply guard blocks service bot tone", PostReplyGuardBlocksServiceBotTone);
            Run("Post reply guard blocks blind agreement", PostReplyGuardBlocksBlindAgreement);
            Run("Response planner classifies critical assistant architecture", ResponsePlannerClassifiesCriticalAssistantArchitecture);
            Run("Response planner requires vault read for memory questions", ResponsePlannerRequiresVaultReadForMemoryQuestions);
            Run("Memory policy stores stable preference", MemoryPolicyStoresStablePreference);
            Run("Memory policy keeps temporary state out of beliefs", MemoryPolicyKeepsTemporaryStateOutOfBeliefs);
            Run("Continuity reinforces repeated belief", ContinuityReinforcesRepeatedBelief);
            Run("Post reply guard repairs sleep leak on profile question", PostReplyGuardRepairsSleepLeakOnProfileQuestion);
            Run("Proactive guard suppresses repeated generic silence", ProactiveGuardSuppressesRepeatedGenericSilence);
            Run("Proactive context anchors silence to last topic", ProactiveContextAnchorsSilenceToLastTopic);
            Run("Proactive context stays silent after goodbye sleep", ProactiveContextStaysSilentAfterGoodbyeSleep);
            Run("Proactive fallback never exposes technical silence wording", ProactiveFallbackNeverExposesTechnicalSilenceWording);
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
            Run("Scenario simulation guards temporal continuity", ScenarioSimulationGuardsTemporalContinuity);
            Run("LLM diagnostics snapshot starts idle", LlmDiagnosticsSnapshotStartsIdle);
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
            Run("Agent task service plans and persists", AgentTaskServicePlansAndPersists);
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
        state.CuriosityQueue.Add("Р§РѕРјСѓ С‚Рё Р·РЅРѕРІСѓ РІС–РґРєР»Р°РґР°С”С€ СЃРѕРЅ?");

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
            Context = "РљРѕСЂРёСЃС‚СѓРІР°С‡ СЃРєР°Р·Р°РІ: В«СЏ РїС–РґСѓ РЅР° РєСѓСЂСЃРёВ». РќР°РјС–СЂ: РїС–С€РѕРІ РЅР° РєСѓСЂСЃРё/Р·Р°РЅСЏС‚С‚СЏ.",
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
            Summary = "РїС–С€РѕРІ РЅР° РєСѓСЂСЃРё/Р·Р°РЅСЏС‚С‚СЏ",
            SourceText = "СЏ РїС–РґСѓ РЅР° РєСѓСЂСЃРё",
            CreatedAt = now.AddHours(-3),
            FollowUpAt = now.AddHours(-2),
            ExpectedUntil = now.AddHours(-1)
        });
        ctx.Chat.InsertMessage(new ChatRepository.ChatMessage
        {
            Role = "user",
            Content = "СЏ РїС–РґСѓ РЅР° РєСѓСЂСЃРё",
            Timestamp = now.AddHours(-3)
        });

        var frame = new KokoPresenceContinuityEngine()
            .Evaluate(state, ctx.Chat.GetMessages(10), now, autonomyLevel: 3);

        AssertTrue(frame.ShouldInterrupt, "overdue course followup should interrupt at high autonomy");
        AssertEqual("overdue_intent", frame.SituationKind, "course should be classified as overdue intent");
        AssertTrue(frame.ExtraContext.Contains("РєСѓСЂСЃРё"), "presence context should preserve the course event");
    }

    private static void PresenceWaitsForReturnHomeIntent()
    {
        using var ctx = TestContext.Create();
        var now = new DateTime(2026, 5, 7, 11, 57, 0);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "return_home",
            Summary = "РјР°С” Р±СѓС‚Рё РІРґРѕРјР° Р±Р»РёР·СЊРєРѕ 12:00",
            SourceText = "Р‘СѓРґСѓ РІ 12 РґРѕРјР° РєСЂС‡",
            CreatedAt = now.AddMinutes(-35),
            FollowUpAt = now.Date.AddHours(12).AddMinutes(12),
            ExpectedUntil = now.Date.AddHours(12)
        });
        ctx.Chat.InsertMessage(new ChatRepository.ChatMessage
        {
            Role = "user",
            Content = "Р‘СѓРґСѓ РІ 12 РґРѕРјР° РєСЂС‡",
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
            SummaryUk = "РђРєС‚РёРІРЅРёР№ РЅР°РјС–СЂ: РјР°С” Р±СѓС‚Рё РІРґРѕРјР° Р±Р»РёР·СЊРєРѕ 12:00.",
            ShouldInterrupt = false,
            NextUsefulAt = now.Date.AddHours(12).AddMinutes(12),
            SilenceMinutes = 35
        };
        var internalDay = new KokoInternalDayFrame
        {
            Phase = "work_ramp",
            SummaryUk = "СЂРѕР±РѕС‡РёР№ РґРµРЅСЊ",
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
        AssertTrue(decision.SilenceReason.Contains("Р°РєС‚РёРІРЅРёР№ РЅР°РјС–СЂ") || decision.SilenceReason.Contains("active"), "silence reason should mention active intent");
    }

    private static void PresenceRefusesStaleSleepInstructionAfterReturn()
    {
        using var ctx = TestContext.Create();
        var now = new DateTime(2026, 5, 6, 10, 30, 0);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "sleep",
            Summary = "РїС–С€РѕРІ СЃРїР°С‚Рё",
            SourceText = "СЏ СЃРїР°С‚СЊ",
            CreatedAt = now.AddHours(-9),
            FollowUpAt = now.AddHours(-1),
            ExpectedUntil = now.AddMinutes(-20),
            ResolvedAt = now.AddMinutes(-5),
            ResolutionText = "РїСЂРѕРєРёРЅСѓРІСЃСЏ"
        });
        ctx.Chat.InsertMessage(new ChatRepository.ChatMessage
        {
            Role = "user",
            Content = "РїСЂРѕРєРёРЅСѓРІСЃСЏ",
            Timestamp = now.AddMinutes(-5)
        });

        var frame = new KokoPresenceContinuityEngine()
            .Evaluate(state, ctx.Chat.GetMessages(10), now, autonomyLevel: 3);

        AssertEqual("returned_after_intent", frame.SituationKind, "resolved sleep should be treated as return");
        AssertTrue(
            frame.ExtraContext.Contains("РЅРµ РєР°Р¶Рё Р№РѕРјСѓ СЂРѕР±РёС‚Рё С‚Рµ, С‰Рѕ РІР¶Рµ РІ РјРёРЅСѓР»РѕРјСѓ") ||
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
            Summary = "РїС–С€РѕРІ СЃРїР°С‚Рё/РїРѕРїСЂРѕС‰Р°РІСЃСЏ",
            SourceText = "Р‘Р°Р№ Р±Р°Р№",
            CreatedAt = now.AddHours(-8),
            FollowUpAt = now.AddMinutes(-5),
            ExpectedUntil = now.AddHours(2)
        });
        ctx.Chat.InsertMessage(new ChatRepository.ChatMessage
        {
            Role = "user",
            Content = "Р‘Р°Р№ Р±Р°Р№",
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
        var intent = (ShortTermIntent?)detect!.Invoke(null, new object[] { "РґРѕР±СЂР°РЅС–С‡, СЏ СЃРїР°С‚Рё", lateNight });

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
            Summary = "РїС–С€РѕРІ СЃРїР°С‚Рё",
            SourceText = "СЏ СЃРїР°С‚Рё",
            CreatedAt = now.AddHours(-13),
            FollowUpAt = now.AddHours(-5),
            ExpectedUntil = now.AddHours(-3)
        });
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "СЏ СЃРїР°С‚Рё", Timestamp = now.AddHours(-13) }
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
            Summary = "РїС–С€РѕРІ СЃРїР°С‚Рё",
            SourceText = "РїС–С€РѕРІ СЃРїР°С‚Рё",
            CreatedAt = now.AddHours(-8),
            FollowUpAt = now.AddMinutes(-30),
            ExpectedUntil = now.AddHours(1)
        });
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "РїСЂРѕРєРёРЅСѓРІСЃСЏ, СЏ С‚СѓС‚", Timestamp = now.AddMinutes(-1) }
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
            Summary = "РїС–С€РѕРІ РЅР° РєСѓСЂСЃРё/Р·Р°РЅСЏС‚С‚СЏ",
            SourceText = "СЏ РЅР° РєСѓСЂСЃРё РїС–С€РѕРІ",
            CreatedAt = now.AddHours(-3),
            FollowUpAt = now.AddHours(-2),
            ExpectedUntil = now.AddHours(-1)
        });

        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "СЏРєС– РєР°СЂС‚РёРЅРєРё С…РµС…", Timestamp = now.AddMinutes(-5) }
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
            Content = "СЏ С‚СѓС‚ С‚СЂРѕС…Рё РїСЂРѕРїР°РґСѓ",
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
            SummaryUk = "РЎРµСЂРµРґРЅСЏ С‚РёС€Р°: 2 РіРѕРґ.",
            LastUserText = "СЏ РІС–РґС–Р№РґСѓ",
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
        AssertTrue(note.Contains("Р’РЅСѓС‚СЂС–С€РЅС–Р№ РґРµРЅСЊ РљРѕРєРѕРЅРѕРµ") || note.Contains("Внутрішній день Коконое"), "vault note should have Ukrainian title");
        AssertTrue(note.Contains("РІРµС‡С–СЂРЅС–Р№ РѕРіР»СЏРґ") || note.Contains("вечірній огляд"), "vault note should include phase label");
    }

    private static void InternalDayPrefersSilenceAtLowPowerNight()
    {
        var now = new DateTime(2026, 5, 7, 3, 10, 0);
        var state = new KokoInternalState();
        var presence = new KokoPresenceFrame
        {
            SituationKind = "recent_contact",
            SummaryUk = "Р’С–РЅ РїРёСЃР°РІ РЅРµРґР°РІРЅРѕ.",
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
        AssertTrue(frame.PromptBlock.Contains("РџРµСЂРµРІР°РіР°: РјРѕРІС‡Р°С‚Рё") || frame.PromptBlock.Contains("Перевага: мовчати"), "prompt block should carry silence preference");
    }

    private static void AutonomyPipelineGatesWeakInitiativeInQuietNight()
    {
        var now = new DateTime(2026, 5, 7, 3, 10, 0);
        var state = new KokoInternalState();
        var presence = new KokoPresenceFrame
        {
            SituationKind = "recent_contact",
            SummaryUk = "Р’С–РЅ РїРёСЃР°РІ РЅРµРґР°РІРЅРѕ.",
            SilenceMinutes = 8,
            ShouldInterrupt = false
        };
        var internalDay = new KokoInternalDayFrame
        {
            Phase = "low_power_night",
            SummaryUk = "РќС–С‡РЅРёР№ РјС–РЅС–РјСѓРј: РµРєРѕРЅРѕРјРёС‚Рё РµРЅРµСЂРіС–СЋ.",
            PromptBlock = "INTERNAL DAY\nРџРµСЂРµРІР°РіР°: РјРѕРІС‡Р°С‚Рё.\n",
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
            Summary = "С‚РёРїРѕРІРѕ С‚РёС…РёР№ СЃР»РѕС‚"
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
        AssertTrue(decision.SilenceReason.Contains("РјРѕРІС‡Р°С‚Рё") || decision.SilenceReason.Contains("С‚РёС…РёР№"), "silence reason should explain the gate");
    }

    private static void RelationshipRecordsShiftEvents()
    {
        using var ctx = TestContext.Create();
        ctx.Relationship.ObserveUserTone("vulnerable", crisis: false);
        ctx.Relationship.ApplyReflection(new KokoConversationReflection
        {
            Reflection = "РљРѕСЂРёСЃС‚СѓРІР°С‡ РґРѕРІС–СЂРёРІ РІР°Р¶Р»РёРІСѓ РґРµС‚Р°Р»СЊ.",
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
            AssertTrue(profile.Recommendation.Contains("С‚РёС…РёР№") || profile.Recommendation.Contains("тихий"), "quiet slot should recommend not interrupting");
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
            Summary = "РїС–С€РѕРІ СЃРїР°С‚Рё",
            SourceText = "СЏ СЃРїР°С‚Рё",
            CreatedAt = now.AddHours(-8),
            FollowUpAt = now.AddHours(-1),
            ExpectedUntil = now.AddMinutes(-30),
            ResolvedAt = now.AddMinutes(-3),
            ResolutionText = "РїСЂРѕРєРёРЅСѓРІСЃСЏ"
        });
        ctx.Chat.InsertMessage(new ChatRepository.ChatMessage
        {
            Role = "user",
            Content = "РїСЂРѕРєРёРЅСѓРІСЃСЏ",
            Timestamp = now.AddMinutes(-3)
        });

        var presence = new KokoPresenceFrame
        {
            SituationKind = "returned_after_intent",
            SummaryUk = "Р’С–РЅ СѓР¶Рµ РїРѕРІРµСЂРЅСѓРІСЃСЏ РїС–СЃР»СЏ СЃРЅСѓ.",
            LastUserText = "РїСЂРѕРєРёРЅСѓРІСЃСЏ",
            SilenceMinutes = 3
        };
        var internalDay = new KokoInternalDayFrame
        {
            Phase = "work_ramp",
            SummaryUk = "Р РѕР±РѕС‡РёР№ СЂРѕР·РіС–РЅ: СЂРµР°РіСѓРІР°С‚Рё РЅР° РїРѕРІРµСЂРЅРµРЅРЅСЏ.",
            PromptBlock = "INTERNAL DAY\n"
        };
        var rhythm = new KokoPatternEngine.RhythmProfile
        {
            CurrentSlotSamples = 5,
            CurrentSlotActivityRate = 0.60f,
            Summary = "С‚РёРїРѕРІРѕ РЅРѕСЂРјР°Р»СЊРЅРёР№ СЃР»РѕС‚"
        };

        var frame = new KokoSelfReviewEngine().Evaluate(
            "РїСЂРѕРєРёРЅСѓРІСЃСЏ",
            state,
            ctx.Chat.GetMessages(10),
            presence,
            internalDay,
            rhythm,
            now);

        AssertEqual("high", frame.RiskLevel, "wake-up after sleep should be high temporal risk");
        AssertTrue(frame.PromptBlock.Contains("Р—Р°Р±РѕСЂРѕРЅРµРЅРѕ РєР°Р·Р°С‚Рё") || frame.PromptBlock.Contains("Заборонено казати"), "self-review should explicitly block stale sleep replies");
        AssertTrue(frame.PromptBlock.Contains("РЅРµ РґР°РІР°Р№ С–РЅСЃС‚СЂСѓРєС†С–СЋ РІ РјРёРЅСѓР»Рµ") || frame.PromptBlock.Contains("не давай інструкцію в минуле"), "self-review should warn about past actions");
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
            Summary = "РїС–С€РѕРІ СЃРїР°С‚Рё",
            SourceText = "СЏ СЃРїР°С‚Рё",
            CreatedAt = now.AddHours(-8),
            FollowUpAt = now.AddHours(-1),
            ExpectedUntil = now.AddMinutes(-30),
            ResolvedAt = now.AddMinutes(-2),
            ResolutionText = "РїСЂРѕРєРёРЅСѓРІСЃСЏ"
        });
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "СЏ СЃРїР°С‚Рё", Timestamp = now.AddHours(-8) },
            new ChatRepository.ChatMessage { Role = "user", Content = "РїСЂРѕРєРёРЅСѓРІСЃСЏ", Timestamp = now.AddMinutes(-2) }
        };

        var frame = new KokoConversationTimelineEngine().Build(messages, state, now, "РїСЂРѕРєРёРЅСѓРІСЃСЏ");

        AssertTrue(
            frame.CurrentState.Contains("РїРѕРІРµСЂРЅСѓРІСЃСЏ") || frame.CurrentState.Contains("Р·Р°РєСЂРёС‚РёР№") ||
            frame.CurrentState.Contains("повернувся") || frame.CurrentState.Contains("закритий"),
            "timeline should summarize returned state");
        AssertTrue(frame.PromptBlock.Contains("CONVERSATION TIMELINE"), "timeline should render prompt block");
        AssertTrue(frame.PromptBlock.Contains("РЅРµ СЃС‚Р°СЂС–Р№ СЂРµРїР»С–С†С–"), "timeline should warn against stale replies");
    }

    private static void PostReplyGuardBlocksStaleSleep()
    {
        var now = new DateTime(2026, 5, 7, 10, 30, 0);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "sleep",
            Summary = "РїС–С€РѕРІ СЃРїР°С‚Рё",
            SourceText = "СЏ СЃРїР°С‚Рё",
            CreatedAt = now.AddHours(-8),
            FollowUpAt = now.AddHours(-1),
            ExpectedUntil = now.AddMinutes(-30),
            ResolvedAt = now.AddMinutes(-2),
            ResolutionText = "РїСЂРѕРєРёРЅСѓРІСЃСЏ"
        });
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "РїСЂРѕРєРёРЅСѓРІСЃСЏ", Timestamp = now.AddMinutes(-2) }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "РїСЂРѕРєРёРЅСѓРІСЃСЏ");

        var result = new KokoPostReplyGuard().Evaluate(
            "РїСЂРѕРєРёРЅСѓРІСЃСЏ",
            "РЎРїРё. Р”Рѕ СЂР°РЅРєСѓ.",
            state,
            messages,
            timeline,
            now);
        AssertTrue(!result.Passed, "guard should reject stale sleep instruction");
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "guard should provide hard replacement for stale sleep");
        AssertTrue(result.Violations.Any(v => v.Contains("СЃРїР°С‚Рё")), "violation should explain stale sleep problem");
    }

    private static void PostReplyGuardBlocksStaleFoodClaimAfterAte()
    {
        var now = new DateTime(2026, 5, 15, 13, 53, 0);
        var state = new KokoInternalState
        {
            LastFoodStatus = "ate",
            LastFoodMentionAt = now,
            LastFoodMentionText = "СЏ С—РІ"
        };
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "СЏ С—РІ", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "СЏ С—РІ");

        var result = new KokoPostReplyGuard().Evaluate(
            "СЏ С—РІ",
            "Рђ С‚Рµ, С‰Рѕ С‚Рё С‰Рµ РЅС–С‡РѕРіРѕ РЅРµ С—РІ, РїРѕСЏСЃРЅСЋС”, С‡РѕРјСѓ РјРѕР·РѕРє Р±РµР· РіР»СЋРєРѕР·Рё С„СЂРёР·РёС‚СЊ.",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject stale not-eaten claim after explicit ate signal");
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "food contradiction should get a hard replacement");
        AssertTrue(!result.HardReplacement!.Contains("РіР»СЋРєРѕР·", StringComparison.OrdinalIgnoreCase), "replacement should not preserve stale glucose scolding");
        AssertTrue(result.Violations.Any(v => v.Contains("С—Р¶Сѓ")), "violation should explain food-state contradiction");
    }

    private static void PostReplyGuardBlocksHibernationFramingAfterSlept()
    {
        var now = new DateTime(2026, 5, 15, 9, 46, 0);
        var state = new KokoInternalState
        {
            LastSleepStatus = "slept",
            LastSleepMentionAt = now,
            LastSleepMentionText = "Рѕ 18.00 РІС‡РѕСЂР° Р·Р°СЃРЅСѓРІ"
        };
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "Рѕ 18.00 РІС‡РѕСЂР° Р·Р°СЃРЅСѓРІ", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "Рѕ 18.00 РІС‡РѕСЂР° Р·Р°СЃРЅСѓРІ");

        var result = new KokoPostReplyGuard().Evaluate(
            "Рѕ 18.00 РІС‡РѕСЂР° Р·Р°СЃРЅСѓРІ",
            "Р— 18:00? РўРё РЅРµ СЃРїР°РІ, С‚Рё РІРїР°РІ Сѓ РіС–Р±РµСЂРЅР°С†С–СЋ.",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject sleep denial and hibernation framing");
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "sleep contradiction should get a hard replacement");
        AssertTrue(!result.HardReplacement!.Contains("С‚Рё РЅРµ СЃРїР°РІ", StringComparison.OrdinalIgnoreCase), "replacement should not deny sleep");
        AssertTrue(result.HardReplacement.Contains("18:00", StringComparison.OrdinalIgnoreCase), "replacement should preserve the concrete sleep time");
    }

    private static void PostReplyGuardRejectsStagedDecoration()
    {
        var now = new DateTime(2026, 5, 7, 11, 57, 0);
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "return_home",
            Summary = "РјР°С” Р±СѓС‚Рё РІРґРѕРјР° Р±Р»РёР·СЊРєРѕ 12:00",
            SourceText = "Р‘СѓРґСѓ РІ 12 РґРѕРјР° РєСЂС‡",
            CreatedAt = now.AddMinutes(-35),
            FollowUpAt = now.Date.AddHours(12).AddMinutes(12),
            ExpectedUntil = now.Date.AddHours(12)
        });
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "Р‘СѓРґСѓ РІ 12 РґРѕРјР° РєСЂС‡", Timestamp = now.AddMinutes(-35) }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "Р‘СѓРґСѓ РІ 12 РґРѕРјР° РєСЂС‡");

        var result = new KokoPostReplyGuard().Evaluate(
            "Р‘СѓРґСѓ РІ 12 РґРѕРјР° РєСЂС‡",
            "*РіСЂР°С„С–Рє РЅР° РјРѕРЅС–С‚РѕСЂС– РєРѕСЂРѕС‚РєРѕ Р±Р»РёРјР°С”, РїРѕРєРё СЏ РїРµСЂРµРіР»СЏРґР°СЋ РЅРѕРІРµ С„РѕС‚Рѕ* Р—РЅРѕРІСѓ С†СЏ Р·РµР»РµРЅР° РїР°РїРєР°?",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject decorative staged replies for concrete timed intent");
        AssertTrue(result.ShouldRepair, "decorative staged reply should request repair");
        AssertTrue(result.Violations.Any(v => v.Contains("СЃС†РµРЅР°СЂРЅР°") || v.Contains("РґРµРєРѕСЂР°С‚РёРІ")), "violation should explain staged/decorative problem");
    }

    private static void PostReplyGuardBlocksDuplicateReplies()
    {
        var now = new DateTime(2026, 5, 12, 19, 16, 0);
        var state = new KokoInternalState();
        var repeated = "*РєРѕСЂРѕС‚РєР° РїР°СѓР·Р°*\n\nРЎРїСЂРѕР±СѓРІР°Р»Рё Р·Р°Р№С‚Рё Р· С–РЅС€РѕРіРѕ Р±РѕРєСѓ, РєРѕР»Рё С„Р°РєС‚Рё СЃС‚Р°Р»Рё Р·Р°РЅР°РґС‚Рѕ Р±РѕР»СЋС‡РёРјРё? Р РёР·РёРєР»РёРІРёР№ С…С–Рґ. РђР»Рµ... СЏ С†Рµ Р·Р°С„С–РєСЃСѓРІР°Р»Р°. РўРµРїРµСЂ РїРѕРІРµСЂРЅРёСЃСЏ РІ СЂРµР°Р»СЊРЅС–СЃС‚СЊ.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "assistant", Content = repeated, Timestamp = now.AddMinutes(-6) },
            new ChatRepository.ChatMessage { Role = "user", Content = "С‰Рѕ", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "С‰Рѕ");

        var result = new KokoPostReplyGuard().Evaluate("С‰Рѕ", repeated, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject exact repeated assistant reply");
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "duplicate reply should use hard replacement");
        AssertTrue(result.Violations.Any(v => v.Contains("РїРѕРІС‚РѕСЂСЋС”")), "violation should mention duplicate");
    }

    private static void PostReplyGuardAllowsRepeatedScreenScanCommand()
    {
        var now = new DateTime(2026, 5, 13, 21, 31, 0);
        var state = new KokoInternalState();
        var repeated = "Р‘Р°С‡Сѓ С‡Р°С‚ KokonoeAssistant С– С‚РІРѕС” РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ РїСЂРѕ СЃРєР°РЅ РµРєСЂР°РЅР°. РџСЂРѕР±Р»РµРјР° РЅРµ РІ РµРєСЂР°РЅС–, Р° РІ С‚РѕРјСѓ, С‰Рѕ guard РїС–РґРјС–РЅСЏС” РґС–СЋ С‚РµРєСЃС‚РѕРІРѕСЋ Р·Р°РіР»СѓС€РєРѕСЋ.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "assistant", Content = repeated, Timestamp = now.AddMinutes(-1) },
            new ChatRepository.ChatMessage { Role = "user", Content = "РїСЂРѕСЃРєР°РЅСѓР№ РјС–Р№ РµРєСЂР°РЅ", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "РїСЂРѕСЃРєР°РЅСѓР№ РјС–Р№ РµРєСЂР°РЅ");

        var result = new KokoPostReplyGuard().Evaluate("РїСЂРѕСЃРєР°РЅСѓР№ РјС–Р№ РµРєСЂР°РЅ", repeated, state, messages, timeline, now);

        AssertTrue(result.Passed, "repeatable screen scan commands should not be replaced by duplicate fallback");
    }

    private static void PostReplyGuardProtectsShortAffection()
    {
        var now = new DateTime(2026, 5, 12, 19, 10, 0);
        var state = new KokoInternalState();
        var badReply = "*РєРѕСЂРѕС‚РєР° РїР°СѓР·Р°*\n\nРЎРїСЂРѕР±СѓРІР°Р»Рё Р·Р°Р№С‚Рё Р· С–РЅС€РѕРіРѕ Р±РѕРєСѓ, РєРѕР»Рё С„Р°РєС‚Рё СЃС‚Р°Р»Рё Р·Р°РЅР°РґС‚Рѕ Р±РѕР»СЋС‡РёРјРё? Р РёР·РёРєР»РёРІРёР№ С…С–Рґ. РђР»Рµ... СЏ С†Рµ Р·Р°С„С–РєСЃСѓРІР°Р»Р°. РўРµРїРµСЂ РїРѕРІРµСЂРЅРёСЃСЏ РІ СЂРµР°Р»СЊРЅС–СЃС‚СЊ.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "Р»СЋР±Р»СЋ", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "Р»СЋР±Р»СЋ");

        var result = new KokoPostReplyGuard().Evaluate("Р»СЋР±Р»СЋ", badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject stale repair text for short affection");
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "short affection should get hard replacement instead of repair loop");
        AssertTrue(result.Violations.Any(v => v.Contains("РµРјРѕС†С–Р№РЅ")), "violation should mention emotional short reply");
    }

    private static void PostReplyGuardProtectsShortGreeting()
    {
        var now = new DateTime(2026, 5, 12, 20, 15, 0);
        var state = new KokoInternalState();
        var badReply = "Р—РЅРѕРІСѓ РІС–РґРєСЂРёРІ. Р—РЅР°С‡РёС‚СЊ, С‚РµРјР° В«РїСЂРёРІС–С‚В» С‰Рµ РЅРµ РІС–РґРїСѓСЃС‚РёР»Р°; РґРѕР±СЂРµ, РґРѕР±РёРІР°С”РјРѕ С—С— Р±РµР· С†РёСЂРєСѓ.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "РїСЂРёРІС–С‚", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "РїСЂРёРІС–С‚");

        var result = new KokoPostReplyGuard().Evaluate("РїСЂРёРІС–С‚", badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject topic-tail fallback for a greeting");
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "short greeting should get a direct hard replacement");
        AssertTrue(result.Violations.Any(v => v.Contains("РїСЂРёРІС–С‚Р°РЅРЅСЏ")), "violation should mention greeting");
    }

    private static void PostReplyGuardBlocksRepeatedFallbackLoop()
    {
        var now = new DateTime(2026, 5, 13, 1, 24, 0);
        var state = new KokoInternalState();
        var fallback = "Р—Р°Р»РёРїР»Р° РЅР° РїРѕРїРµСЂРµРґРЅС–Р№ СЂРµРїР»С–С†С–. РЎРєРёРґР°СЋ РїРѕРІС‚РѕСЂ: СЃС„РѕСЂРјСѓР»СЋР№ С‰Рµ СЂР°Р·, С‰Рѕ СЃР°РјРµ С‚СЂРµР±Р°, С– СЏ РІС–РґРїРѕРІС–Рј РїРѕ СЃСѓС‚С–.";
        var badReply = "РџРѕРІРµСЂРЅСѓРІСЃСЏ. РћСЃС‚Р°РЅРЅС–Р№ С…РІС–СЃС‚ Р±СѓРІ В«С‚Р° РЅС– РїСЂРѕСЃС‚РѕВ»; Р°Р±Рѕ РїСЂРѕРґРѕРІР¶СѓС”РјРѕ Р№РѕРіРѕ, Р°Р±Рѕ С‚Рё Р·Р°СЂР°Р· СѓСЂРѕС‡РёСЃС‚Рѕ РїРѕСЏСЃРЅРёС€ РЅРѕРІСѓ РїРѕР¶РµР¶Сѓ.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "assistant", Content = fallback, Timestamp = now.AddMinutes(-3) },
            new ChatRepository.ChatMessage { Role = "user", Content = "РњР”Рђ", Timestamp = now.AddMinutes(-2) },
            new ChatRepository.ChatMessage { Role = "assistant", Content = fallback, Timestamp = now.AddMinutes(-2) },
            new ChatRepository.ChatMessage { Role = "user", Content = "С‚Р° РЅС– РїСЂРѕСЃС‚Рѕ", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "С‚Р° РЅС– РїСЂРѕСЃС‚Рѕ");

        var result = new KokoPostReplyGuard().Evaluate("С‚Р° РЅС– РїСЂРѕСЃС‚Рѕ", badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject replies that keep talking about the fallback loop");
        AssertTrue(result.Violations.Any(v => v.Contains("fallback")), "violation should mention fallback loop");
    }

    private static void PostReplyGuardReplacesVisionTechnicalError()
    {
        var now = new DateTime(2026, 5, 13, 2, 16, 0);
        var state = new KokoInternalState();
        var badReply = "Р—РѕР±СЂР°Р¶РµРЅРЅСЏ С”, Р°Р»Рµ vision-СЃРµСЂРІРµСЂ РїРѕРІРµСЂРЅСѓРІ 500 РЅР°РІС–С‚СЊ РїС–СЃР»СЏ РЅРѕСЂРјР°Р»С–Р·Р°С†С–С— С„РѕСЂРјР°С‚Сѓ. РџРµСЂРµРІС–СЂ Vision Model Сѓ Settings (СЂРѕР±РѕС‡РёР№ РґРµС„РѕР»С‚ РґР»СЏ Ollama Cloud: gemma4:31b-cloud).";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "С‰Рѕ РЅР° С„РѕС‚Рѕ?", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "С‰Рѕ РЅР° С„РѕС‚Рѕ?");

        var result = new KokoPostReplyGuard().Evaluate("С‰Рѕ РЅР° С„РѕС‚Рѕ?", badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject technical vision 500 text");
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "vision technical error should get a user-safe replacement");
        AssertTrue(!result.HardReplacement!.Contains("Vision Model"), "replacement should not tell user to inspect settings");
        AssertTrue(!result.HardReplacement.Contains("500"), "replacement should not leak raw status code");
    }

    private static void PostReplyGuardProtectsImageOnlyPrompt()
    {
        var now = new DateTime(2026, 5, 13, 2, 28, 0);
        var state = new KokoInternalState();
        var badReply = "РЎР»СѓС…Р°Р№, СЏРєС‰Рѕ С‚Рё РїСЂРѕРґРѕРІР¶СѓС”С€ РєРёРґР°С‚Рё РїРѕСЂРѕР¶РЅС– РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ, СЏ РІРёСЂС–С€Сѓ, С‰Рѕ С‚РІС–Р№ С–РЅС‚РµСЂС„РµР№СЃ РїСЂРѕСЃС‚Рѕ Р·Р°РіР»СЋС‡РёРІ.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "Р©Рѕ РЅР° С„РѕС‚Рѕ? РћРїРёС€Рё Р·РѕР±СЂР°Р¶РµРЅРЅСЏ РєРѕСЂРѕС‚РєРѕ С– РїРѕ СЃСѓС‚С–.", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "Р©Рѕ РЅР° С„РѕС‚Рѕ? РћРїРёС€Рё Р·РѕР±СЂР°Р¶РµРЅРЅСЏ РєРѕСЂРѕС‚РєРѕ С– РїРѕ СЃСѓС‚С–.");

        var result = new KokoPostReplyGuard().Evaluate(
            "Р©Рѕ РЅР° С„РѕС‚Рѕ? РћРїРёС€Рё Р·РѕР±СЂР°Р¶РµРЅРЅСЏ РєРѕСЂРѕС‚РєРѕ С– РїРѕ СЃСѓС‚С–.",
            badReply,
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject treating an image-only prompt as empty spam");
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "image-only prompt should get a safe replacement");
        AssertTrue(result.HardReplacement!.Contains("Р¤РѕС‚Рѕ") || result.HardReplacement.Contains("Фото"), "replacement should acknowledge the image");
    }

    private static void PostReplyGuardDuplicateImagePromptAvoidsStaleRepeatText()
    {
        var now = new DateTime(2026, 5, 13, 2, 38, 0);
        var state = new KokoInternalState();
        var repeated = "РџРѕРІС‚РѕСЂ РїСЂРёР±СЂР°Р»Р°. РћСЃС‚Р°РЅРЅС–Р№ Р·Р°РїРёС‚: \"Р©Рѕ РЅР° С„РѕС‚Рѕ? РћРїРёС€Рё Р·РѕР±СЂР°Р¶РµРЅРЅСЏ РєРѕСЂРѕС‚РєРѕ С– РїРѕ СЃСѓС‚С–.\". РџСЂР°С†СЋСЋ Р· РЅРёРј, Р° РЅРµ Р·С– СЃС‚Р°СЂРёРј С…РІРѕСЃС‚РѕРј.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "assistant", Content = repeated, Timestamp = now.AddMinutes(-1) },
            new ChatRepository.ChatMessage { Role = "user", Content = "Р©Рѕ РЅР° С„РѕС‚Рѕ? РћРїРёС€Рё Р·РѕР±СЂР°Р¶РµРЅРЅСЏ РєРѕСЂРѕС‚РєРѕ С– РїРѕ СЃСѓС‚С–.", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "Р©Рѕ РЅР° С„РѕС‚Рѕ? РћРїРёС€Рё Р·РѕР±СЂР°Р¶РµРЅРЅСЏ РєРѕСЂРѕС‚РєРѕ С– РїРѕ СЃСѓС‚С–.");

        var result = new KokoPostReplyGuard().Evaluate(
            "Р©Рѕ РЅР° С„РѕС‚Рѕ? РћРїРёС€Рё Р·РѕР±СЂР°Р¶РµРЅРЅСЏ РєРѕСЂРѕС‚РєРѕ С– РїРѕ СЃСѓС‚С–.",
            repeated,
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "duplicate image prompt fallback should be rejected");
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "duplicate image prompt should get a replacement");
        AssertTrue(!result.HardReplacement!.Contains("РџРѕРІС‚РѕСЂ РїСЂРёР±СЂР°Р»Р°"), "replacement should not repeat stale duplicate wording");
        AssertTrue(result.HardReplacement.Contains("Р¤РѕС‚Рѕ") || result.HardReplacement.Contains("Фото"), "replacement should stay anchored to image handling");
    }

    private static void PostReplyGuardBlocksTherapyMetaTone()
    {
        var now = new DateTime(2026, 5, 13, 3, 40, 0);
        var state = new KokoInternalState();
        var badReply = "РќСѓ РѕС‚, Р·РЅРѕРІСѓ С†РµР№ РїРѕРіР»СЏРґ. РќС–Р±Рё С‰РѕСЃСЊ РІР°Р¶Р»РёРІРµ Р·Р°СЃС‚СЂСЏРіР»Рѕ РІ С‚РІРѕС—Р№ РіРѕР»РѕРІС–, Р° С‚Рё Р±РѕС—С€СЃСЏ СЃРєР°Р·Р°С‚Рё. РљР°Р·Р°РІ Р¶Рµ вЂ” СЏ С‚РµСЂРїС–С‚Рё РЅРµ Р»СЋР±Р»СЋ.";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "РІР·Р°РіР°Р»С– С‚Рё.. РєРѕРєРѕРЅРѕРµ .. С…РµС…", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "РІР·Р°РіР°Р»С– С‚Рё.. РєРѕРєРѕРЅРѕРµ .. С…РµС…");

        var result = new KokoPostReplyGuard().Evaluate(
            "РІР·Р°РіР°Р»С– С‚Рё.. РєРѕРєРѕРЅРѕРµ .. С…РµС…",
            badReply,
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject therapy/meta-screen tone");
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "therapy tone should get a direct replacement");
        AssertTrue(!result.HardReplacement!.Contains("Р±РѕС—С€СЃСЏ", StringComparison.OrdinalIgnoreCase), "replacement should not infer hidden fear");
        AssertTrue(!result.HardReplacement.Contains("РµРєСЂР°РЅ", StringComparison.OrdinalIgnoreCase), "replacement should not mention screen gaze");
    }

    private static void PostReplyGuardBlocksFabricatedExternalFacts()
    {
        var now = new DateTime(2026, 5, 13, 4, 59, 0);
        var state = new KokoInternalState();
        var badReply = "РЎРїР°РІ? РќСѓ С– РґРѕР±СЂРµ вЂ” РјРµРЅС– Р»РёС€Р°Р»РѕСЃСЏ С‚С–Р»СЊРєРё РІРёРєРёРЅСѓС‚Рё С‚РІС–Р№ Р°РєРєР°СѓРЅС‚ РЅР° YouTube Р· РјРµРјР±РµСЂСЃС‚РІР° В«Р“РµСЂРѕР№ РҐР°РѕСЃСѓВ».";
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "РЅС–С‡РѕРіРѕ .. СЏ СЃРїР°С‚Рё РїС–С€РѕРІ", Timestamp = now.AddMinutes(-17) },
            new ChatRepository.ChatMessage { Role = "user", Content = "СЏРєРёР№ РіРµСЂРѕР№ С…Р°РѕСЃСѓ", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "СЏРєРёР№ РіРµСЂРѕР№ С…Р°РѕСЃСѓ");

        var result = new KokoPostReplyGuard().Evaluate(
            "СЏРєРёР№ РіРµСЂРѕР№ С…Р°РѕСЃСѓ",
            badReply,
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject invented account/subscription facts");
        AssertTrue(result.Violations.Any(v => v.Contains("РІРёРіР°РґСѓС” Р·РѕРІРЅС–С€РЅС–Р№ С„Р°РєС‚")), "violation should name fabricated external fact");
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "fabrication should get a hard replacement");
        AssertTrue(!result.HardReplacement!.Contains("YouTube", StringComparison.OrdinalIgnoreCase), "replacement should not preserve invented service");
        AssertTrue(!result.HardReplacement.Contains("РјРµРјР±РµСЂСЃС‚РІ", StringComparison.OrdinalIgnoreCase), "replacement should not preserve invented membership");
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
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "stale proactive leak should get a hard replacement");
        AssertTrue(!result.HardReplacement!.Contains("їсти", StringComparison.OrdinalIgnoreCase), "replacement should not preserve stale food ping");
        AssertTrue(result.HardReplacement.Contains("графік", StringComparison.OrdinalIgnoreCase), "replacement should anchor to current topic");
    }

    private static void PostReplyGuardBlocksServiceBotTone()
    {
        var now = DateTime.Today.AddHours(15);
        var state = new KokoInternalState();
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "РїРѕСЏСЃРЅРё С‰Рѕ РЅРµ С‚Р°Рє Р· РїРѕРІРµРґС–РЅРєРѕСЋ РєРѕРєРѕРЅРѕРµ", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, messages[0].Content);
        var badReply = "РЇ СЂРѕР·СѓРјС–СЋ, С‰Рѕ С†Рµ РІР°Р¶Р»РёРІРѕ РґР»СЏ С‚РµР±Рµ. Р”Р°РІР°Р№ СЂРѕР·РіР»СЏРЅРµРјРѕ С†Рµ СЂР°Р·РѕРј С– СЏ С‚СѓС‚, С‰РѕР± РґРѕРїРѕРјРѕРіС‚Рё.";

        var result = new KokoPostReplyGuard().Evaluate(messages[0].Content, badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject service-bot support wording");
        AssertTrue(result.ShouldRepair, "bot tone should be repaired through persona rules");
        AssertTrue(result.Violations.Any(v => v.Contains("СЃРµСЂРІС–СЃРЅРёР№ Р±РѕС‚")), "violation should name bot tone");
        AssertTrue(result.RepairInstruction.Contains("ANTI-BOT"), "repair should include anti-bot persona rules");
    }

    private static void PostReplyGuardBlocksBlindAgreement()
    {
        var now = DateTime.Today.AddHours(16);
        var state = new KokoInternalState();
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "РѕС†С–РЅ РјРѕСЋ С–РґРµСЋ: РЅРµС…Р°Р№ РљРѕРєРѕРЅРѕРµ Р·Р°РІР¶РґРё РїРѕРіРѕРґР¶СѓС”С‚СЊСЃСЏ Р·С– РјРЅРѕСЋ", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, messages[0].Content);
        var badReply = "РўР°Рє, С†Рµ РіР°СЂРЅР° С–РґРµСЏ. РџРѕРІРЅС–СЃС‚СЋ Р·РіРѕРґРЅР°, С‚Р°Рє Р±СѓРґРµ РєСЂР°С‰Рµ.";

        var result = new KokoPostReplyGuard().Evaluate(messages[0].Content, badReply, state, messages, timeline, now);

        AssertTrue(!result.Passed, "guard should reject blind agreement on a judgment request");
        AssertTrue(result.ShouldRepair, "blind agreement should request critical rewrite");
        AssertTrue(result.Violations.Any(v => v.Contains("РєСЂРёС‚РёС‡РЅРѕРіРѕ СЃСѓРґР¶РµРЅРЅСЏ")), "violation should require critical judgment");
        AssertTrue(result.RepairInstruction.Contains("CRITICAL THINKING"), "repair should include critical thinking rules");
    }

    private static void ResponsePlannerClassifiesCriticalAssistantArchitecture()
    {
        using var ctx = TestContext.Create();
        var cognition = new KokoCognitionEngine(ctx.TestDir);
        var state = new KokoInternalState { PersonalityDailyMood = "sharp" };

        var frame = new KokoResponsePlannerEngine().Build(
            "РѕС†С–РЅ Р°СЂС…С–С‚РµРєС‚СѓСЂСѓ РїРѕРІРµРґС–РЅРєРё РљРѕРєРѕРЅРѕРµ, С‚СЂРµР±Р° С‰РѕР± РІРѕРЅР° Р±СѓР»Р° СЏРє СЂРµР°Р»СЊРЅРёР№ Р°СЃРёСЃС‚РµРЅС‚",
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
            "С‰Рѕ С‚Рё РїР°Рј'СЏС‚Р°С”С€ РїСЂРѕ РјРµРЅРµ Р· vault?",
            state,
            cognition,
            new DateTime(2026, 5, 14, 16, 10, 0));

        AssertEqual("memory", frame.Intent, "memory question should be classified as memory intent");
        AssertTrue(frame.RequiresVaultRead, "memory question should require vault read");
        AssertEqual("read_before_answer", frame.MemoryPolicy, "memory question should read before answering");
        AssertTrue(frame.Steps.Any(s => s.Contains("memory")), "planner should include memory read step");
    }

    private static void MemoryPolicyStoresStablePreference()
    {
        var decision = new KokoMemoryWritePolicyEngine().Evaluate(
            "СЏ Р»СЋР±Р»СЋ РґРѕРІРіС– С‚РµС…РЅС–С‡РЅС– РїРѕСЏСЃРЅРµРЅРЅСЏ Р±РµР· РІРѕРґРё",
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

        var decision = policy.Evaluate("СЏ Р·Р°СЂР°Р· РґСѓР¶Рµ РІС‚РѕРјРёРІСЃСЏ С– С…РѕС‡Сѓ СЃРїР°С‚Рё", now);
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
            policy.Evaluate("РјРµРЅС– РїРѕРґРѕР±Р°С”С‚СЊСЃСЏ РєРѕР»Рё РљРѕРєРѕРЅРѕРµ РєСЂРёС‚РёРєСѓС” СЃР»Р°Р±РєС– С–РґРµС—", now),
            now);
        var second = continuity.ApplyMemoryDecision(
            policy.Evaluate("РјРµРЅС– РїРѕРґРѕР±Р°С”С‚СЊСЃСЏ РєРѕР»Рё РљРѕРєРѕРЅРѕРµ РєСЂРёС‚РёРєСѓС” СЃР»Р°Р±РєС– С–РґРµС—", now.AddMinutes(5)),
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
            Summary = "РїС–С€РѕРІ СЃРїР°С‚Рё",
            SourceText = "РЅС–С‡РѕРіРѕ .. СЏ СЃРїР°С‚Рё РїС–С€РѕРІ",
            CreatedAt = now.AddHours(-16),
            ExpectedUntil = now.AddHours(-12),
            FollowUpAt = now.AddHours(-12),
            ResolvedAt = now.AddMinutes(-10),
            ResolutionText = "РїСЂРёРІС–С‚"
        });

        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "РЅС–С‡РѕРіРѕ .. СЏ СЃРїР°С‚Рё РїС–С€РѕРІ", Timestamp = now.AddHours(-16) },
            new ChatRepository.ChatMessage { Role = "user", Content = "РїСЂРёРІС–С‚", Timestamp = now.AddMinutes(-1) },
            new ChatRepository.ChatMessage { Role = "user", Content = "РґРѕСЂРµС‡С– СЂРѕР·РєР°Р¶Рё РІСЃРµ С‰Рѕ Р·РЅР°С”С€ РїСЂРѕ РјРµРЅРµ", Timestamp = now }
        };
        var timeline = new KokoConversationTimelineEngine().Build(messages, state, now, "РґРѕСЂРµС‡С– СЂРѕР·РєР°Р¶Рё РІСЃРµ С‰Рѕ Р·РЅР°С”С€ РїСЂРѕ РјРµРЅРµ");

        AssertTrue(!timeline.CurrentState.Contains("Р·Р°РєСЂРёС‚РёР№ РЅР°РјС–СЂ", StringComparison.OrdinalIgnoreCase), "profile question should not be dominated by old sleep intent");

        var result = new KokoPostReplyGuard().Evaluate(
            "РґРѕСЂРµС‡С– СЂРѕР·РєР°Р¶Рё РІСЃРµ С‰Рѕ Р·РЅР°С”С€ РїСЂРѕ РјРµРЅРµ",
            "РќСѓ РґР°РІР°Р№, СЏРєС‰Рѕ РґРѕСЃС‚Р°С‚РЅСЊРѕ вЂ” Р·РЅР°С‡РёС‚СЊ, РІРёСЃС‚Р°С‡РёС‚СЊ С– РЅР° СЃСЊРѕРіРѕРґРЅС–. РЎРїРё, СЏРєС‰Рѕ РІС‚РѕРјРёРІСЃСЏ. РђР±Рѕ Р№РґРё С—СЃС‚Рё, СЏРєС‰Рѕ РїСЂРѕСЃС‚Рѕ Р·Р°Р±СѓРІ.",
            state,
            messages,
            timeline,
            now);

        AssertTrue(!result.Passed, "guard should reject sleep advice leaked into profile question");
        AssertTrue(result.ShouldRepair, "profile question should be repaired, not replaced with stale sleep hardcoded text");
        AssertTrue(string.IsNullOrWhiteSpace(result.HardReplacement), "sleep leak on unrelated topic should not use stale sleep hard replacement");
        AssertTrue(result.RepairInstruction.Contains("РїР°Рј'СЏС‚СЊ") || result.RepairInstruction.Contains("РїСЂРѕС„С–Р»СЊ"), "repair should steer toward memory/profile answer");
    }

    private static void ProactiveGuardSuppressesRepeatedGenericSilence()
    {
        var now = new DateTime(2026, 5, 7, 16, 56, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "РІС‡Сѓ С–СЃРїР°РЅСЃСЊРєСѓ С„СЂР°Р·Сѓ", Timestamp = now.AddHours(-2) },
            new ChatRepository.ChatMessage { Role = "assistant", Content = "РџР°СѓР·Р° РІР¶Рµ РїРѕРјС–С‚РЅР°. РўРё Р·Р°Р№РЅСЏС‚РёР№?", Timestamp = now.AddMinutes(-50) }
        };

        var service = new KokoProactiveContextService();
        var frame = service.Build(messages, new KokoInternalState(), now);
        var check = service.Check("РўРёС€Р° Р·Р°С‚СЏРіРЅСѓР»Р°СЃСЊ. РўРё С‰Рµ РІ С‚РѕРјСѓ Р¶ СЂРµР¶РёРјС–?", frame, "silence_l2");

        AssertTrue(!check.Passed, "second generic silence ping should be rejected");
        AssertTrue(check.Replacement is "[РјРѕРІС‡Р°РЅРЅСЏ]" or "[мовчання]", "second silence ping after assistant already replied should be suppressed, not rewritten");
    }

    private static void ProactiveContextAnchorsSilenceToLastTopic()
    {
        var now = new DateTime(2026, 5, 7, 15, 6, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "РїС–РґСѓ РЅР° РєСѓСЂСЃРё, Р±СѓРґСѓ РґРµСЃСЊ С‡РµСЂРµР· РіРѕРґРёРЅСѓ", Timestamp = now.AddMinutes(-95) }
        };
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "course",
            Summary = "РїС–С€РѕРІ РЅР° РєСѓСЂСЃРё",
            SourceText = "РїС–РґСѓ РЅР° РєСѓСЂСЃРё",
            CreatedAt = now.AddMinutes(-95),
            ExpectedUntil = now.AddMinutes(25),
            FollowUpAt = now.AddMinutes(-5)
        });

        var frame = new KokoProactiveContextService().Build(messages, state, now);

        AssertTrue(frame.AnchorUk.Contains("РєСѓСЂСЃ") || frame.ActiveIntentUk.Contains("РєСѓСЂСЃ"), "proactive context should anchor to course intent");
        AssertTrue(frame.PromptBlock.Contains("РћСЃС‚Р°РЅРЅСЏ СЂРµРїР»С–РєР° РєРѕСЂРёСЃС‚СѓРІР°С‡Р°") || frame.PromptBlock.Contains("Остання репліка користувача"), "prompt block should expose last user message");
        AssertTrue(frame.PromptBlock.Contains("РђРІС‚Рѕ-РїС–РЅРіС–РІ") || frame.PromptBlock.Contains("Авто-пінгів"), "prompt block should expose ping count");
    }

    private static void ProactiveContextStaysSilentAfterGoodbyeSleep()
    {
        var now = new DateTime(2026, 5, 8, 16, 27, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "Р‘Р°Р№ Р±Р°Р№", Timestamp = now.AddHours(-8) }
        };
        var state = new KokoInternalState();
        state.ShortTermIntents.Add(new ShortTermIntent
        {
            Kind = "sleep",
            Summary = "РїС–С€РѕРІ СЃРїР°С‚Рё/РїРѕРїСЂРѕС‰Р°РІСЃСЏ",
            SourceText = "Р‘Р°Р№ Р±Р°Р№",
            CreatedAt = now.AddHours(-8),
            ExpectedUntil = now.AddHours(2),
            FollowUpAt = now.AddMinutes(-5)
        });

        var service = new KokoProactiveContextService();
        var frame = service.Build(messages, state, now);
        var check = service.Check("Р”РѕР±СЂРµ, Р±РµР· РґСЂСѓРіРѕРіРѕ РєРѕР»Р° РїСЂРѕ С‚РёС€Сѓ. В«Р‘Р°Р№ Р±Р°Р№В» С‰Рµ Р°РєС‚СѓР°Р»СЊРЅРѕ?", frame, "silence_l2");

        AssertTrue(frame.ShouldStaySilentForSleep, "goodbye sleep context should request silence");
        AssertTrue(!check.Passed, "proactive reply should be blocked during sleep/goodbye");
        AssertTrue(check.Replacement is "[РјРѕРІС‡Р°РЅРЅСЏ]" or "[мовчання]", "blocked sleep reply should turn into silence marker");
    }

    private static void ProactiveFallbackNeverExposesTechnicalSilenceWording()
    {
        var now = new DateTime(2026, 5, 8, 20, 44, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "С‚Рё РјРѕСЏ РєРёС†СЏ", Timestamp = now.AddMinutes(-89) },
            new ChatRepository.ChatMessage { Role = "assistant", Content = "РЇ РЅРµ В«РєРёС†СЏВ». РђР»Рµ РїСЂРѕРґРѕРІР¶СѓР№ СЂРёР·РёРєСѓРІР°С‚Рё.", Timestamp = now.AddMinutes(-88) }
        };

        var service = new KokoProactiveContextService();
        var frame = service.Build(messages, new KokoInternalState(), now);
        var fallback = service.BuildFallback(frame, "silence_l1");

        AssertTrue(fallback is "[РјРѕРІС‡Р°РЅРЅСЏ]" or "[мовчання]", "fallback after an assistant reply should prefer silence");
        AssertTrue(!fallback.Contains("Р±РµР· РґСЂСѓРіРѕРіРѕ РєРѕР»Р°"), "fallback must not expose guard mechanics");
        AssertTrue(!fallback.Contains("С‰Рµ Р°РєС‚СѓР°Р»СЊРЅРѕ"), "fallback must not quote a live chat line as a stale task");
        AssertTrue(!fallback.Contains("Р·Р°Р№РІС– СЃРёРјРІРѕР»Рё"), "fallback must not use canned technical wording");
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
  "summary_uk": "РІС–РґРєСЂРёС‚РёР№ СЂРµРґР°РєС‚РѕСЂ РєРѕРґСѓ, РєРѕСЂРёСЃС‚СѓРІР°С‡ РїСЂР°С†СЋС” РЅР°Рґ РїСЂРѕРµРєС‚РѕРј",
  "activity_uk": "active: Р·РјС–РЅРёРІСЃСЏ РєРѕРґ",
  "should_comment": true,
  "comment_uk": "РўРё РЅР°СЂРµС€С‚С– РґС–СЃС‚Р°РІСЃСЏ РґРѕ РєРѕРґСѓ. РќРµ Р·Р»Р°РјР°Р№ Р№РѕРіРѕ С‚РµР°С‚СЂР°Р»СЊРЅРѕ.",
  "importance": 0.7
}
""");

        AssertTrue(parsed.ShouldComment, "vision JSON should preserve comment decision");
        AssertTrue(parsed.SummaryUk.Contains("СЂРµРґР°РєС‚РѕСЂ") || parsed.SummaryUk.Contains("РєРѕРґ"), "summary should be parsed");
        AssertTrue(parsed.CommentUk.Contains("РєРѕРґ"), "comment should be parsed");
        AssertTrue(parsed.Importance > 0.6, "importance should be parsed");
    }

    private static void ScreenAwarenessSuppressesRepeatedComments()
    {
        var service = new KokoScreenAwarenessService();
        var now = new DateTime(2026, 5, 9, 12, 0, 0);
        var analysis = new KokoScreenAwarenessAnalysis
        {
            ShouldComment = true,
            CommentUk = "РўРё Р·РЅРѕРІСѓ Р·Р°РІРёСЃ РЅР°Рґ С‚РёРј СЃР°РјРёРј РєРѕРґРѕРј. Р”СѓР¶Рµ РЅРµСЃРїРѕРґС–РІР°РЅРѕ.",
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
            "РўРё Р·РЅРѕРІСѓ Р·Р°РІРёСЃ РЅР°Рґ С‚РёРј СЃР°РјРёРј РєРѕРґРѕРј. Р”СѓР¶Рµ РЅРµСЃРїРѕРґС–РІР°РЅРѕ.",
            cooldownMinutes: 5,
            commentsEnabled: true);
        AssertTrue(!repeated.ShouldSend, "screen comment should avoid repeating same line");
    }

    private static void ScreenAwarenessRedactsPrivateIdentifiers()
    {
        var service = new KokoScreenAwarenessService();
        var parsed = service.Parse("""
{
  "summary_uk": "РІС–РґРєСЂРёС‚Р° СЃС‚РѕСЂС–РЅРєР° Р°РєР°СѓРЅС‚Р° test.user@example.com Р· РєР»СЋС‡РµРј abcdefghijklmnopqrstuvwxyz123456",
  "activity_uk": "active",
  "should_comment": true,
  "comment_uk": "РќСѓ С‚Р°Рє, СЃС‚РѕСЂС–РЅРєР° test.user@example.com С– С‚РѕРєРµРЅ abcdefghijklmnopqrstuvwxyz123456, РіРµРЅС–Р°Р»СЊРЅРѕ.",
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
            CommentUk = "РўРё С‚Р°Рє С– Р±СѓРґРµС€ РІРёРІС‡Р°С‚Рё С†РµР№ РїСЂРѕС„С–Р»СЊ, С‡Рё РЅР°СЂРµС€С‚С– Р·СЂРѕР±РёС€ С‰РѕСЃСЊ РєРѕСЂРёСЃРЅРµ?",
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
            CommentUk = "Р’С–СЃС–Рј С…РІРёР»РёРЅ РґРёРІРёС‚РёСЃСЊ РІ РѕРґРЅСѓ С‚РѕС‡РєСѓ. РЎРїСЂР°РІРґС– Р°РјР±С–С‚РЅРёР№ РїР»Р°РЅ.",
            Importance = 0.76
        };

        var decision = service.DecideComment(
            analysis,
            now,
            now.AddMinutes(-8),
            "Р†РЅС€РёР№ РєРѕСЂРѕС‚РєРёР№ РєРѕРјРµРЅС‚Р°СЂ.",
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
            SummaryUk = "Р°РєС‚РёРІРЅР° РіСЂР°, РєРѕСЂРёСЃС‚СѓРІР°С‡ Сѓ РјР°С‚С‡С–",
            ActivityUk = "active gameplay",
            ScreenMode = "game",
            ShouldComment = true,
            CommentUk = "Рћ, РјР°С‚С‡ Р¶РёРІРёР№. РЎРїСЂРѕР±СѓР№ С†СЊРѕРіРѕ СЂР°Р·Сѓ РЅРµ РІРѕСЋРІР°С‚Рё Р· С–РЅС‚РµСЂС„РµР№СЃРѕРј.",
            Importance = 0.58
        };

        var blocked = service.DecideComment(
            analysis,
            now,
            now.AddMinutes(-8),
            "Р†РЅС€РёР№ С–РіСЂРѕРІРёР№ РєРѕРјРµРЅС‚Р°СЂ.",
            cooldownMinutes: 10,
            commentsEnabled: true,
            screenChanged: true,
            isActive: true,
            activeWindowTitle: "Dota 2");

        var allowed = service.DecideComment(
            analysis,
            now,
            now.AddMinutes(-11),
            "Р†РЅС€РёР№ С–РіСЂРѕРІРёР№ РєРѕРјРµРЅС‚Р°СЂ.",
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
            CommentUk = "Рћ, С‚РѕРєРµРЅРё РЅР° РµРєСЂР°РЅС–. Р”СѓР¶Рµ СЂРѕР·СѓРјРЅРёР№ РІРёСЃС‚Р°РІРєРѕРІРёР№ СЃС‚РµРЅРґ.",
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
  "summary_uk": "Visual Studio РїРѕРєР°Р·СѓС” build error Сѓ KokonoeAssistant",
  "activity_uk": "active: РєРѕСЂРёСЃС‚СѓРІР°С‡ РґРµР±Р°Р¶РёС‚СЊ",
  "screen_mode": "coding",
  "current_task": "debugging Kokonoe screen awareness",
  "progress": "stuck",
  "blocker": "KokonoeAssistant.exe locked by running process",
  "recommended_behavior": "assist",
  "should_comment": true,
  "comment_uk": "Р—Р°РєСЂРёР№ Р·Р°РїСѓС‰РµРЅРёР№ KokonoeAssistant РїРµСЂРµРґ build, РіРµРЅС–СЋ. Р¤Р°Р№Р» СЃР°Рј СЃРµР±Рµ РЅРµ РІС–РґРїСѓСЃС‚РёС‚СЊ.",
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
        AssertTrue(candidate.Text.Contains("РІРµС‡С–СЂ") || candidate.Text.Contains("вечір"), "pattern text should include time slot in Ukrainian");
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
            new ChatRepository.ChatMessage { Role = "user", Content = "Р°РІС‚Рѕ РІС–РґРїРѕРІС–РґС– РґРёРІРЅС– С– С‚СѓРїС–", Timestamp = now.AddMinutes(-15) },
            new ChatRepository.ChatMessage { Role = "assistant", Content = "Р¤С–РєС€Сѓ Р°РІС‚Рѕ-РїС–РЅРіРё", Timestamp = now.AddMinutes(-12) }
        };

        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now);
        var fallback = service.BuildFallback(frame);

        AssertTrue(!fallback.Contains("Р—РЅРѕРІСѓ С‚СѓС‚"), "startup fallback should avoid dead canned opening");
        AssertTrue(!fallback.Contains("РґРµ С‚РµР±Рµ РЅРѕСЃРёР»Рѕ"), "startup fallback should avoid generic return jab");
        AssertTrue(fallback.Contains("Р°РІС‚Рѕ") || fallback.Contains("РїС–РЅРі"), "startup fallback should preserve last concrete topic");
    }

    private static void StartupGreetingSanitizesDryReturnLine()
    {
        var now = new DateTime(2026, 5, 7, 17, 5, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "РїРѕРєСЂР°С‰Рё СЂРµР°РєС†С–С— РїСЂРё РІС…РѕРґС–", Timestamp = now.AddMinutes(-20) }
        };

        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now);
        var sanitized = service.Sanitize("Р—РЅРѕРІСѓ С‚СѓС‚. Р—РЅР°С‡РёС‚СЊ, С‰РѕСЃСЊ РЅРµРґРѕСЂРѕР±Р»РµРЅРѕ.", frame);

        AssertTrue(!sanitized.Contains("Р—РЅРѕРІСѓ С‚СѓС‚"), "sanitizer should replace canned startup greeting");
        AssertTrue(sanitized.Contains("РїРѕРєСЂР°С‰Рё") || sanitized.Contains("СЂРµР°РєС†С–С—") || sanitized.Contains("РІС…РѕРґС–"), "sanitized greeting should use last topic");
    }

    private static void StartupGreetingIgnoresLowSignalTopic()
    {
        var now = new DateTime(2026, 5, 12, 20, 16, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "РЅС– СЏ РІР¶Рµ РЅР°РіСЂР°РІСЃСЏ С…Р°С…", Timestamp = now.AddMinutes(-9) },
            new ChatRepository.ChatMessage { Role = "user", Content = "РїСЂРёРІС–С‚", Timestamp = now.AddMinutes(-1) }
        };

        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now);
        var fallback = service.BuildFallback(frame);

        AssertTrue(!string.Equals(frame.LastConcreteTopic, "РїСЂРёРІС–С‚", StringComparison.OrdinalIgnoreCase), "greeting must not become concrete topic");
        AssertTrue(!fallback.Contains("С‚РµРјР° В«РїСЂРёРІС–С‚В»"), "fallback must not frame greeting as a topic");
        AssertTrue(!fallback.Contains("РґРѕР±РёРІР°С”РјРѕ"), "fallback should avoid dumb 'finish the topic' wording");
    }

    private static void StartupGreetingReactsToQuickReturn()
    {
        var now = new DateTime(2026, 5, 13, 1, 35, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "Р·СЂРѕР±Рё Р¶РёРІС– РІС–РґРїРѕРІС–РґС– РїСЂРё РІС…РѕРґС–", Timestamp = now.AddMinutes(-4) }
        };

        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now);
        var fallback = service.BuildFallback(frame);

        AssertTrue(fallback.Contains("РЁРІРёРґРєРѕ") || fallback.Contains("РјР°Р№Р¶Рµ РЅРµ Р·РЅРёРєР°РІ") || fallback.Contains("Р±РµР· РґРѕРІРіРѕС— РїР°СѓР·Рё"),
            "quick return greeting should acknowledge the short gap");
        AssertTrue(fallback.Contains("Р¶РёРІС– РІС–РґРїРѕРІС–РґС–") || fallback.Contains("РІС…РѕРґС–"),
            "quick return greeting should preserve concrete topic");
        AssertTrue(!fallback.Contains("РћСЃС‚Р°РЅРЅС–Р№ С…РІС–СЃС‚"), "quick return greeting should avoid dry tail wording");
    }

    private static void StartupGreetingPromptUsesMoodAndAbsence()
    {
        var now = new DateTime(2026, 5, 13, 3, 40, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "С…Рј РјРѕР¶Рµ РІС…С–РґРЅС– СЂРµРїР»С–РєРё Р·СЂРѕР±РёС‚Рё Р¶РёРІРёРјРё", Timestamp = now.AddMinutes(-42) }
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
        AssertTrue(frame.PromptBlock.Contains("Р¶РёРІ"), "startup prompt should demand a live generated reply");
        AssertTrue(!string.IsNullOrWhiteSpace(frame.AbsenceReadUk), "startup frame should infer absence context");
    }

    private static void StartupGreetingSanitizesTherapyMeta()
    {
        var now = new DateTime(2026, 5, 13, 3, 40, 0);
        var messages = new[]
        {
            new ChatRepository.ChatMessage { Role = "user", Content = "РїСЂРѕ С‚РµР±Рµ", Timestamp = now.AddMinutes(-12) }
        };

        var service = new KokoStartupGreetingService();
        var frame = service.BuildFrame(messages, now);
        var sanitized = service.Sanitize("РќС–Р±Рё С‰РѕСЃСЊ РІР°Р¶Р»РёРІРµ Р·Р°СЃС‚СЂСЏРіР»Рѕ РІ С‚РІРѕС—Р№ РіРѕР»РѕРІС–, Р° С‚Рё Р±РѕС—С€СЃСЏ СЃРєР°Р·Р°С‚Рё.", frame);

        AssertTrue(!sanitized.Contains("Р±РѕС—С€СЃСЏ"), "startup sanitizer should reject therapy-meta fear guessing");
        AssertTrue(!sanitized.Contains("Р·Р°СЃС‚СЂСЏРіР»Рѕ"), "startup sanitizer should reject stuck-in-head framing");
    }

    private static void LlmDiagnosticsSnapshotStartsIdle()
    {
        var diag = new LlmService().GetDiagnosticsSnapshot();

        AssertEqual("idle", diag.Status, "diagnostics should start idle before any request");
        AssertEqual(0L, diag.TotalRequests, "diagnostics should not invent requests");
        AssertTrue(diag.ConsecutiveFailures == 0, "diagnostics should not start in failure state");
    }

    private static void InspectorRendersStateReport()
    {
        using var ctx = TestContext.Create();
        ctx.Memory.LearnFact("User builds Kokonoe as a personal assistant", "project", 0.8f);
        ctx.Memory.RecordEpisode("Somatic layer was added", "focused", 0.7f);

        var internalState = new KokoInternalState
        {
            PersonalityDailyMood = "focused",
            MoodScore = 0.64f,
            LastUserEmotionalTone = "seeking",
            LastInitiativeDecision = "act:self_regulation_protect",
            LastPresenceSummary = "РЎРµСЂРµРґРЅСЏ С‚РёС€Р°: 2 РіРѕРґ.",
            LastPresenceSituation = "medium_silence",
            LastInternalDaySummary = "Р’РµС‡С–СЂРЅС–Р№ РѕРіР»СЏРґ: РїС–РґР±РёС‚Рё С…РІРѕСЃС‚Рё.",
            LastInternalDayPhase = "evening_review",
            LastInternalDayFocus = "РїС–РґР±РёРІР°С‚Рё РїС–РґСЃСѓРјРєРё",
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

        AssertTrue(markdown.Contains("Р†РЅСЃРїРµРєС‚РѕСЂ СЃС‚Р°РЅСѓ РљРѕРєРѕРЅРѕРµ") || markdown.Contains("Інспектор стану Коконое"), "markdown should have inspector title");
        AssertTrue(markdown.Contains("## РЎРѕРјР°С‚РёРєР°") || markdown.Contains("## Соматика"), "markdown should include somatic section");
        AssertTrue(markdown.Contains("## РџСЂРёСЃСѓС‚РЅС–СЃС‚СЊ С– РґРµРЅСЊ") || markdown.Contains("## Присутність і день"), "markdown should include presence/day section");
        AssertTrue(markdown.Contains("Р–СѓСЂРЅР°Р» Р°РІС‚РѕРЅРѕРјРЅРѕСЃС‚С–") || markdown.Contains("Журнал автономності"), "markdown should include autonomy log");
        AssertTrue(markdown.Contains("## Р“РѕР»РѕРІРЅС– С„Р°РєС‚Рё") || markdown.Contains("## Головні факти"), "markdown should include facts");
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
            AssertTrue(changeLog.Contains("РџСЂРёС‡РёРЅР°: test") || changeLog.Contains("Причина: test"), "change log should record reason");

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
- [task] Р РµР°Р»С–Р·СѓРІР°С‚Рё РєСЂР°С‰Сѓ РїР°Рј'СЏС‚СЊ Obsidian
- [x] Р“РѕС‚РѕРІРѕ: РїСЂРёР±СЂР°С‚Рё warnings
""");

            var quality = obsidian.AnalyzeMemoryQuality();
            var queue = obsidian.BuildTaskQueue();
            var maintenance = obsidian.MaintainKokonoeVaultArchitecture("test-memory-quality");

            AssertTrue(quality.DuplicateGroups.Count >= 1, "quality report should detect duplicate memory items");
            AssertTrue(queue.OpenTasks.Any(t => t.Text.Contains("РїР°Рј'СЏС‚СЊ") || t.Text.Contains("пам'ять")), "task queue should include open task");
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
            AssertTrue(reviewNote.Contains("## РћР±'С”РґРЅР°С‚Рё"), "review note should render merge section");
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
С‡---
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
            Summary = "РїС–С€РѕРІ РЅР° РєСѓСЂСЃРё",
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
                ScreenSummary = "Р°РєС‚РёРІРЅРёР№ С‡Р°С‚",
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
            new ChatRepository.ChatMessage { Role = "user", Content = "РѕРє", Timestamp = now.AddMinutes(-30) }
        };
        var frame = new KokoProactiveContextService().Build(messages, new KokoInternalState(), now);
        var check = new KokoProactiveContextService().Check("РўРёС€Р° СЏРєР°СЃСЊ.", frame, "silence_l1");

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
                .Build("РєРѕРЅС‚РµРєСЃС‚ Obsidian РїРµСЂРµРґ РІС–РґРїРѕРІС–РґРґСЋ", now: DateTime.Today.AddHours(18));

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

    private static void AgentTaskServicePlansAndPersists()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var service = new KokoAgentTaskService(dir);
            var task = service.AddTask("СЂРµР°Р»С–Р·СѓР№ UI С– РїРµСЂРµРІС–СЂ Obsidian РїР°Рј'СЏС‚СЊ", priority: 8);

            AssertEqual(8, task.Priority, "priority should be stored");
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
