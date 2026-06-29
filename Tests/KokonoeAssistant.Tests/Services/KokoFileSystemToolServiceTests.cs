using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KokonoeAssistant.Services;
using Xunit;

namespace KokonoeAssistant.Tests.Services;

public sealed class KokoFileSystemToolServiceTests
{
    private readonly string _workspaceRoot;
    private readonly KokoFileSystemToolService _service;

    public KokoFileSystemToolServiceTests()
    {
        _workspaceRoot = Path.Combine(Path.GetTempPath(), "kokonoe-fs-tests", Guid.NewGuid().ToString("N"));
        _service = new KokoFileSystemToolService(_workspaceRoot);
    }

    [Fact]
    public void Constructor_WhenWorkspaceRootIsEmpty_ThrowsArgumentException()
    {
        Action act = () => _ = new KokoFileSystemToolService(" ");

        act.Should().Throw<ArgumentException>()
            .WithMessage("Workspace root is empty.*");
    }

    [Fact]
    public void Constructor_WhenWorkspaceRootDoesNotExist_CreatesDirectory()
    {
        Directory.Exists(_workspaceRoot).Should().BeTrue();
    }

    [Fact]
    public void GetToolNames_ReturnsStablePublicToolContract()
    {
        _service.GetToolNames().Should().BeEquivalentTo(new[]
        {
            "fs_read_text",
            "fs_write_text",
            "fs_create_directory",
            "fs_delete",
            "fs_move",
            "fs_list_directory"
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _service.ExecuteAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPathIsEmpty_ThrowsArgumentException()
    {
        var request = new KokoFileOperationRequest
        {
            Kind = KokoFileOperationKind.ReadText,
            Path = " "
        };

        Func<Task> act = () => _service.ExecuteAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Path is empty.*");
    }

    [Fact]
    public async Task ReadText_WhenFileDoesNotExist_ReturnsFailure()
    {
        var result = await _service.ExecuteAsync(new KokoFileOperationRequest
        {
            Kind = KokoFileOperationKind.ReadText,
            Path = "missing.txt"
        });

        result.Success.Should().BeFalse();
        result.RequiresConfirmation.Should().BeFalse();
        result.Message.Should().Contain("File not found");
    }

    [Fact]
    public async Task WriteText_WhenNotConfirmed_ReturnsConfirmationRequestAndDoesNotWriteFile()
    {
        var result = await _service.ExecuteAsync(new KokoFileOperationRequest
        {
            Kind = KokoFileOperationKind.WriteText,
            Path = "notes/today.md",
            Content = "hello",
            Confirmed = false
        });

        result.Success.Should().BeFalse();
        result.RequiresConfirmation.Should().BeTrue();
        result.Message.Should().Contain("Confirmation required");
        File.Exists(Path.Combine(_workspaceRoot, "notes", "today.md")).Should().BeFalse();
    }

    [Fact]
    public async Task WriteText_WhenConfirmed_CreatesParentDirectoryAndWritesUtf8Text()
    {
        var result = await _service.ExecuteAsync(new KokoFileOperationRequest
        {
            Kind = KokoFileOperationKind.WriteText,
            Path = "notes/today.md",
            Content = "Привіт Kokonoe",
            Confirmed = true
        });

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Wrote file");

        var writtenPath = Path.Combine(_workspaceRoot, "notes", "today.md");
        File.Exists(writtenPath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(writtenPath);
        content.Should().Be("Привіт Kokonoe");
    }

    [Fact]
    public async Task ReadText_WhenFileExists_ReturnsFileContent()
    {
        var filePath = Path.Combine(_workspaceRoot, "memory.txt");
        await File.WriteAllTextAsync(filePath, "stable memory");

        var result = await _service.ExecuteAsync(new KokoFileOperationRequest
        {
            Kind = KokoFileOperationKind.ReadText,
            Path = "memory.txt"
        });

        result.Success.Should().BeTrue();
        result.Output.Should().Be("stable memory");
        result.Message.Should().Be("Read complete.");
    }

    [Fact]
    public async Task CreateDirectory_WhenNestedPathIsProvided_CreatesDirectoryWithoutConfirmation()
    {
        var result = await _service.ExecuteAsync(new KokoFileOperationRequest
        {
            Kind = KokoFileOperationKind.CreateDirectory,
            Path = "vault/daily/2026"
        });

        result.Success.Should().BeTrue();
        Directory.Exists(Path.Combine(_workspaceRoot, "vault", "daily", "2026")).Should().BeTrue();
    }

    [Fact]
    public async Task ListDirectory_WhenDirectoryExists_ReturnsEntriesWithoutConfirmation()
    {
        Directory.CreateDirectory(Path.Combine(_workspaceRoot, "notes"));
        Directory.CreateDirectory(Path.Combine(_workspaceRoot, "notes", "archive"));
        await File.WriteAllTextAsync(Path.Combine(_workspaceRoot, "notes", "today.md"), "stable memory");

        var result = await _service.ExecuteAsync(new KokoFileOperationRequest
        {
            Kind = KokoFileOperationKind.ListDirectory,
            Path = "notes"
        });

        result.Success.Should().BeTrue();
        result.RequiresConfirmation.Should().BeFalse();
        result.Message.Should().Contain("entries in");
        result.Output.Should().Contain("[dir]  archive");
        result.Output.Should().Contain("[file] today.md");
    }

    [Fact]
    public async Task Move_WhenDestinationIsMissing_ReturnsFailure()
    {
        var sourcePath = Path.Combine(_workspaceRoot, "source.txt");
        await File.WriteAllTextAsync(sourcePath, "content");

        var result = await _service.ExecuteAsync(new KokoFileOperationRequest
        {
            Kind = KokoFileOperationKind.Move,
            Path = "source.txt",
            DestinationPath = " ",
            Confirmed = true
        });

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("DestinationPath is required");
    }

    [Fact]
    public async Task Move_WhenConfirmed_MovesFileAndCreatesDestinationDirectory()
    {
        var sourcePath = Path.Combine(_workspaceRoot, "source.txt");
        await File.WriteAllTextAsync(sourcePath, "content");

        var result = await _service.ExecuteAsync(new KokoFileOperationRequest
        {
            Kind = KokoFileOperationKind.Move,
            Path = "source.txt",
            DestinationPath = "archive/source.txt",
            Confirmed = true
        });

        result.Success.Should().BeTrue();
        File.Exists(sourcePath).Should().BeFalse();
        File.ReadAllText(Path.Combine(_workspaceRoot, "archive", "source.txt")).Should().Be("content");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRelativePathHasNoMatchingFile_ReportsNotFoundNotAnEscape()
    {
        // The workspace sandbox was removed by design (full disk access, confirmed with the
        // user) - a path outside the old workspace root is no longer special-cased, it's just
        // a path that may or may not exist.
        var request = new KokoFileOperationRequest
        {
            Kind = KokoFileOperationKind.ReadText,
            Path = "../outside.txt"
        };

        var result = await _service.ExecuteAsync(request);

        result.Success.Should().BeFalse();
        result.Message.Should().StartWith("File not found:");
    }

    [Fact]
    public async Task ExecuteAsync_WhenAbsolutePathIsOutsideWorkspace_ReadsItAnyway()
    {
        var outsidePath = Path.Combine(Path.GetTempPath(), "kokonoe-outside-" + Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(outsidePath, "real disk access");

        var request = new KokoFileOperationRequest
        {
            Kind = KokoFileOperationKind.ReadText,
            Path = outsidePath
        };

        var result = await _service.ExecuteAsync(request);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("real disk access");

        File.Delete(outsidePath);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSiblingDirectorySharesWorkspacePrefix_StillReadsIt()
    {
        var sibling = _workspaceRoot + "Sibling";
        Directory.CreateDirectory(sibling);
        var siblingFile = Path.Combine(sibling, "sample.txt");
        await File.WriteAllTextAsync(siblingFile, "sample");

        var request = new KokoFileOperationRequest
        {
            Kind = KokoFileOperationKind.ReadText,
            Path = siblingFile
        };

        var result = await _service.ExecuteAsync(request);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("sample");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationWasRequested_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new KokoFileOperationRequest
        {
            Kind = KokoFileOperationKind.ReadText,
            Path = "anything.txt"
        };

        Func<Task> act = () => _service.ExecuteAsync(request, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnsupportedOperation_ReturnsFailure()
    {
        var result = await _service.ExecuteAsync(new KokoFileOperationRequest
        {
            Kind = (KokoFileOperationKind)999,
            Path = "anything.txt"
        });

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Unsupported operation");
    }
}
