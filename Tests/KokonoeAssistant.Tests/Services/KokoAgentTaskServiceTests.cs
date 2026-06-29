using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using KokonoeAssistant.Services;
using Newtonsoft.Json;
using Xunit;

namespace KokonoeAssistant.Tests.Services;

public sealed class KokoAgentTaskServiceTests
{
    [Fact]
    public void ConfigureMaxParallel_ClampsAndReflectsInSnapshot()
    {
        var dataDir = NewTempDir();
        try
        {
            var service = new KokoAgentTaskService(dataDir) { AutoStartOnAdd = false };

            service.ConfigureMaxParallel(99).Should().Be(10);
            service.GetSnapshot().MaxParallel.Should().Be(10);

            service.ConfigureMaxParallel(0).Should().Be(1);
            service.GetSnapshot().MaxParallel.Should().Be(1);
        }
        finally
        {
            TryDelete(dataDir);
        }
    }

    [Fact]
    public void SplitBatchObjectives_WhenInputIsMarkedList_ReturnsDistinctTasks()
    {
        var objectives = KokoAgentTaskService.SplitBatchObjectives("""
            1. Scan vault for project notes
            2. Check system context
            - Summarize risks
            - Scan vault for project notes
            """);

        objectives.Should().BeEquivalentTo(new[]
        {
            "Scan vault for project notes",
            "Check system context",
            "Summarize risks"
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void SplitBatchObjectives_WhenInputIsPlainMultilineText_KeepsSingleObjective()
    {
        var objectives = KokoAgentTaskService.SplitBatchObjectives("""
            Improve the multitasking board.
            Keep the UI compact and show useful state.
            """);

        objectives.Should().ContainSingle()
            .Which.Should().Contain("Keep the UI compact");
    }

    [Fact]
    public void AddBatch_WhenInputIsMarkedList_CreatesMultipleTasks()
    {
        var dataDir = NewTempDir();
        try
        {
            var service = new KokoAgentTaskService(dataDir) { AutoStartOnAdd = false };

            var tasks = service.AddBatch("""
                - Inspect runtime lanes
                - Verify queue counters
                """, priority: 7);

            tasks.Should().HaveCount(2);
            service.GetSnapshot().Tasks.Should().HaveCount(2);
            service.GetSnapshot().Tasks.Should().OnlyContain(task => task.Priority == 7);
        }
        finally
        {
            TryDelete(dataDir);
        }
    }

    [Fact]
    public void RetryTask_WhenTaskWasCanceled_ResetsStepsAndEvidence()
    {
        var dataDir = NewTempDir();
        try
        {
            var service = new KokoAgentTaskService(dataDir) { AutoStartOnAdd = false };
            var task = service.AddTask("retry canceled work", priority: 6);

            service.CancelTask(task.Id).Should().BeTrue();
            var retried = service.RetryTask(task.Id);

            retried.Should().NotBeNull();
            retried!.Status.Should().Be(KokoAgentTaskStatus.Pending);
            retried.CompletionNotice.Should().BeEmpty();
            retried.NextQuestion.Should().BeEmpty();
            retried.Steps.Should().OnlyContain(step =>
                step.Status == KokoAgentTaskStatus.Pending &&
                string.IsNullOrWhiteSpace(step.Result) &&
                string.IsNullOrWhiteSpace(step.Error) &&
                step.StartedAt == null &&
                step.FinishedAt == null);
        }
        finally
        {
            TryDelete(dataDir);
        }
    }

    [Fact]
    public void Load_WhenPersistedWorkWasRunning_ResetsItToPending()
    {
        var dataDir = NewTempDir();
        try
        {
            var path = Path.Combine(dataDir, "agent-tasks.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new List<KokoAgentTask>
            {
                new()
                {
                    Id = "task-1",
                    Objective = "recover stale runner state",
                    Status = KokoAgentTaskStatus.Running,
                    Steps = new()
                    {
                        new KokoAgentStep
                        {
                            Id = "step-1",
                            Order = 1,
                            Title = "stale step",
                            Kind = KokoAgentStepKind.Analyze,
                            Status = KokoAgentTaskStatus.Running,
                            StartedAt = DateTime.Now.AddMinutes(-5)
                        }
                    }
                }
            }, Formatting.Indented));

            var service = new KokoAgentTaskService(dataDir) { AutoStartOnAdd = false };
            var task = service.GetSnapshot().Tasks.Should().ContainSingle().Subject;

            task.Status.Should().Be(KokoAgentTaskStatus.Pending);
            task.Steps.Should().ContainSingle().Subject.Status.Should().Be(KokoAgentTaskStatus.Pending);
            service.GetSnapshot().RunningSteps.Should().Be(0);
            service.GetSnapshot().ActiveLanes.Should().BeEmpty();
        }
        finally
        {
            TryDelete(dataDir);
        }
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kokonoe-agent-task-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
