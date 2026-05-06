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
            Run("Inspector renders state report", InspectorRendersStateReport);
            Run("Obsidian vault architecture maintenance", ObsidianVaultArchitectureMaintenance);
            Run("Obsidian unique memory append", ObsidianUniqueMemoryAppend);
            Run("Obsidian memory quality and task queue", ObsidianMemoryQualityAndTaskQueue);
            Run("Obsidian memory duplicate cleanup", ObsidianMemoryDuplicateCleanup);
            Run("Obsidian memory review suggestions", ObsidianMemoryReviewSuggestions);
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
            LastInitiativeDecision = "act:self_regulation_protect"
        };
        internalState.CuriosityQueue.Add("What should I optimize next?");
        internalState.InnerMonologues.Add("[somatic/focus] Signal is clean. Work mode.");

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
        AssertTrue(markdown.Contains("## Головні факти"), "markdown should include facts");
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
            AssertTrue(File.ReadAllText(Path.Combine(dir, "Kokonoe", "Vault Index.md")).Contains("managed-by: Kokonoe"), "managed notes should keep ownership marker");
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

    private static void ObsidianPreflightContextLoadsVaultBeforeReply()
    {
        var dir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant.Tests", "vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var obsidian = new ObsidianMcpService(dir);
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
