using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KokonoeAssistant.Services;
using Xunit;

namespace KokonoeAssistant.Tests.Services;

public sealed class KokoSandboxExecutorTests
{
    [Fact]
    public void Constructor_WhenWorkspaceIsEmpty_ThrowsArgumentException()
    {
        Action act = () => _ = new KokoSandboxExecutor(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WhenWorkspaceDoesNotExist_CreatesWorkspaceDirectory()
    {
        var workspace = CreateWorkspacePath();

        _ = new KokoSandboxExecutor(workspace);

        Directory.Exists(workspace).Should().BeTrue();
    }

    [Fact]
    public async Task ExecutePythonAsync_WhenCodeIsEmpty_ReturnsSkippedMessage()
    {
        var executor = new KokoSandboxExecutor(CreateWorkspacePath());

        var result = await executor.ExecutePythonAsync(" ");

        result.Should().Be("Sandbox skipped: empty code.");
    }

    [Fact]
    public async Task ExecutePythonAsync_WhenCancellationIsAlreadyRequested_ThrowsOperationCanceledExceptionBeforeProcessStart()
    {
        var executor = new KokoSandboxExecutor(CreateWorkspacePath());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => executor.ExecutePythonAsync("print('hello')", ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static string CreateWorkspacePath()
    {
        return Path.Combine(Path.GetTempPath(), "kokonoe-python-tests", Guid.NewGuid().ToString("N"));
    }
}
