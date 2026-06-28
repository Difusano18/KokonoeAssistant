using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    public enum KokoFileOperationKind
    {
        ReadText,
        WriteText,
        CreateDirectory,
        Delete,
        Move
    }

    public sealed class KokoFileOperationRequest
    {
        public KokoFileOperationKind Kind { get; set; }
        public string Path { get; set; } = "";
        public string DestinationPath { get; set; } = "";
        public string Content { get; set; } = "";
        public bool Confirmed { get; set; }
    }

    public sealed class KokoFileOperationResult
    {
        public bool Success { get; set; }
        public bool RequiresConfirmation { get; set; }
        public string Message { get; set; } = "";
        public string Output { get; set; } = "";
    }

    public interface IKokoFileSystemToolService
    {
        IReadOnlyList<string> GetToolNames();
        string ResolvePath(string path);
        Task<KokoFileOperationResult> ExecuteAsync(KokoFileOperationRequest request, CancellationToken ct = default);
    }

    public sealed class KokoFileSystemToolService : IKokoFileSystemToolService
    {
        // Relative paths still resolve against this so "notes.txt" goes somewhere sane, but
        // absolute paths are no longer confined to it - confirmed with the user that the agent
        // should have real disk access (Desktop, arbitrary paths), not just its own workspace.
        // Delete/Move/WriteText still require Confirmed=true (RequiresConfirmation below).
        private readonly string _workspaceRoot;

        public KokoFileSystemToolService(string workspaceRoot)
        {
            if (string.IsNullOrWhiteSpace(workspaceRoot))
                throw new ArgumentException("Workspace root is empty.", nameof(workspaceRoot));

            _workspaceRoot = Path.GetFullPath(workspaceRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            Directory.CreateDirectory(_workspaceRoot);
        }

        public IReadOnlyList<string> GetToolNames() => new[]
        {
            "fs_read_text",
            "fs_write_text",
            "fs_create_directory",
            "fs_delete",
            "fs_move"
        };

        public string ResolvePath(string path) => ResolveFullPath(path);

        public async Task<KokoFileOperationResult> ExecuteAsync(KokoFileOperationRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            ct.ThrowIfCancellationRequested();

            var path = ResolveFullPath(request.Path);
            var destination = string.IsNullOrWhiteSpace(request.DestinationPath)
                ? ""
                : ResolveFullPath(request.DestinationPath);

            if (RequiresConfirmation(request.Kind) && !request.Confirmed)
            {
                return new KokoFileOperationResult
                {
                    RequiresConfirmation = true,
                    Message = $"Confirmation required for {request.Kind}: {path}"
                };
            }

            try
            {
                switch (request.Kind)
                {
                    case KokoFileOperationKind.ReadText:
                        if (!File.Exists(path))
                            return Fail($"File not found: {path}");
                        return Ok(await File.ReadAllTextAsync(path, Encoding.UTF8, ct).ConfigureAwait(false), "Read complete.");

                    case KokoFileOperationKind.WriteText:
                        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _workspaceRoot);
                        await File.WriteAllTextAsync(path, request.Content ?? "", Encoding.UTF8, ct).ConfigureAwait(false);
                        return Ok("", $"Wrote file: {path}");

                    case KokoFileOperationKind.CreateDirectory:
                        Directory.CreateDirectory(path);
                        return Ok("", $"Created directory: {path}");

                    case KokoFileOperationKind.Delete:
                        if (Directory.Exists(path))
                            Directory.Delete(path, recursive: true);
                        else if (File.Exists(path))
                            File.Delete(path);
                        else
                            return Fail($"Path not found: {path}");
                        return Ok("", $"Deleted: {path}");

                    case KokoFileOperationKind.Move:
                        if (string.IsNullOrWhiteSpace(destination))
                            return Fail("DestinationPath is required.");
                        Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? _workspaceRoot);
                        if (Directory.Exists(path))
                            Directory.Move(path, destination);
                        else if (File.Exists(path))
                            File.Move(path, destination, overwrite: true);
                        else
                            return Fail($"Path not found: {path}");
                        return Ok("", $"Moved: {path} -> {destination}");

                    default:
                        return Fail($"Unsupported operation: {request.Kind}");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("FILE-TOOL", $"{request.Kind} failed path={path} destination={destination}: {ex}");
                return Fail($"{request.Kind} failed: {ex.Message}");
            }
        }

        private string ResolveFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is empty.", nameof(path));

            return Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(_workspaceRoot, path));
        }

        private static bool RequiresConfirmation(KokoFileOperationKind kind) =>
            kind is KokoFileOperationKind.Delete or KokoFileOperationKind.Move or KokoFileOperationKind.WriteText;

        private static KokoFileOperationResult Ok(string output, string message) => new()
        {
            Success = true,
            Output = output,
            Message = message
        };

        private static KokoFileOperationResult Fail(string message) => new()
        {
            Success = false,
            Message = message
        };
    }
}
