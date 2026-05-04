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
