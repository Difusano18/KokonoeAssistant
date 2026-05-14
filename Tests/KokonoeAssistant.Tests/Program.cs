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
            Run("Post reply guard repairs sleep leak on profile question", PostReplyGuardRepairsSleepLeakOnProfileQuestion);
            Run("Proactive guard suppresses repeated generic silence", ProactiveGuardSuppressesRepeatedGenericSilence);
            Run("Proactive context anchors silence to last topic", ProactiveContextAnchorsSilenceToLastTopic);
            Run("Proactive context stays silent after goodbye sleep", ProactiveContextStaysSilentAfterGoodbyeSleep);
            Run("Proactive fallback never exposes technical silence wording", ProactiveFallbackNeverExposesTechnicalSilenceWording);
            Run("Screen awareness parses vision JSON", ScreenAwarenessParsesVisionJson);
            Run("Screen awareness suppresses repeated comments", ScreenAwarenessSuppressesRepeatedComments);
            Run("Screen awareness redacts private identifiers", ScreenAwarenessRedactsPrivateIdentifiers);
            Run("Screen awareness allows rare passive jab", ScreenAwarenessAllowsRarePassiveJab);
            Run("Screen awareness lets jab bypass comment cooldown", ScreenAwarenessLetsJabBypassCommentCooldown);
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

            Console.WriteLine($"PASS {_passed} tests");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
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
        AssertTrue(frame.ExtraContext.Contains("не кажи йому робити те, що вже в минулому"), "presence context should block stale sleep instruction");
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
        AssertTrue(note.Contains("Внутрішній день Коконое"), "vault note should have Ukrainian title");
        AssertTrue(note.Contains("вечірній огляд"), "vault note should include phase label");
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
        AssertTrue(frame.PromptBlock.Contains("Перевага: мовчати"), "prompt block should carry silence preference");
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
            AssertTrue(profile.Recommendation.Contains("тихий"), "quiet slot should recommend not interrupting");
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
        AssertTrue(frame.PromptBlock.Contains("Заборонено казати"), "self-review should explicitly block stale sleep replies");
        AssertTrue(frame.PromptBlock.Contains("не давай інструкцію в минуле"), "self-review should warn about past actions");
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

        AssertTrue(frame.CurrentState.Contains("повернувся") || frame.CurrentState.Contains("закритий"), "timeline should summarize returned state");
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
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "guard should provide hard replacement for stale sleep");
        AssertTrue(result.Violations.Any(v => v.Contains("спати")), "violation should explain stale sleep problem");
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
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "duplicate reply should use hard replacement");
        AssertTrue(result.Violations.Any(v => v.Contains("повторює")), "violation should mention duplicate");
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
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "short affection should get hard replacement instead of repair loop");
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
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "short greeting should get a direct hard replacement");
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

    private static void PostReplyGuardReplacesVisionTechnicalError()
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
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "vision technical error should get a user-safe replacement");
        AssertTrue(!result.HardReplacement!.Contains("Vision Model"), "replacement should not tell user to inspect settings");
        AssertTrue(!result.HardReplacement.Contains("500"), "replacement should not leak raw status code");
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
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "image-only prompt should get a safe replacement");
        AssertTrue(result.HardReplacement!.Contains("Фото"), "replacement should acknowledge the image");
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
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "duplicate image prompt should get a replacement");
        AssertTrue(!result.HardReplacement!.Contains("Повтор прибрала"), "replacement should not repeat stale duplicate wording");
        AssertTrue(result.HardReplacement.Contains("Фото"), "replacement should stay anchored to image handling");
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
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "therapy tone should get a direct replacement");
        AssertTrue(!result.HardReplacement!.Contains("боїшся", StringComparison.OrdinalIgnoreCase), "replacement should not infer hidden fear");
        AssertTrue(!result.HardReplacement.Contains("екран", StringComparison.OrdinalIgnoreCase), "replacement should not mention screen gaze");
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
        AssertTrue(!string.IsNullOrWhiteSpace(result.HardReplacement), "fabrication should get a hard replacement");
        AssertTrue(!result.HardReplacement!.Contains("YouTube", StringComparison.OrdinalIgnoreCase), "replacement should not preserve invented service");
        AssertTrue(!result.HardReplacement.Contains("мемберств", StringComparison.OrdinalIgnoreCase), "replacement should not preserve invented membership");
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
        AssertEqual("[мовчання]", check.Replacement, "second silence ping after assistant already replied should be suppressed, not rewritten");
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
        AssertTrue(frame.PromptBlock.Contains("Остання репліка користувача"), "prompt block should expose last user message");
        AssertTrue(frame.PromptBlock.Contains("Авто-пінгів"), "prompt block should expose ping count");
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
        AssertEqual("[мовчання]", check.Replacement, "blocked sleep reply should turn into silence marker");
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

        AssertEqual("[мовчання]", fallback, "fallback after an assistant reply should prefer silence");
        AssertTrue(!fallback.Contains("без другого кола"), "fallback must not expose guard mechanics");
        AssertTrue(!fallback.Contains("ще актуально"), "fallback must not quote a live chat line as a stale task");
        AssertTrue(!fallback.Contains("зайві символи"), "fallback must not use canned technical wording");
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
        AssertTrue(candidate.Text.Contains("вечір"), "pattern text should include time slot in Ukrainian");
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
        AssertTrue(frame.PromptBlock.Contains("режим", StringComparison.OrdinalIgnoreCase) || frame.PromptBlock.Contains("Return mode"), "startup prompt should include return mode");
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

        AssertTrue(markdown.Contains("Інспектор стану Коконое"), "markdown should have inspector title");
        AssertTrue(markdown.Contains("## Соматика"), "markdown should include somatic section");
        AssertTrue(markdown.Contains("## Присутність і день"), "markdown should include presence/day section");
        AssertTrue(markdown.Contains("Журнал автономності"), "markdown should include autonomy log");
        AssertTrue(markdown.Contains("## Головні факти"), "markdown should include facts");
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
            AssertTrue(File.ReadAllText(Path.Combine(dir, "Kokonoe", "Architecture", "Change Log.md")).Contains("Причина: test"), "change log should record reason");

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
            AssertTrue(queue.OpenTasks.Any(t => t.Text.Contains("пам'ять")), "task queue should include open task");
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
