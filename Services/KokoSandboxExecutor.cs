using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KokonoeAssistant.Services
{
    public sealed class KokoSandboxExecutor
    {
        private readonly string _workspace;

        public KokoSandboxExecutor(string workspace)
        {
            _workspace = workspace;
            Directory.CreateDirectory(_workspace);
        }

        public async Task<string> ExecutePythonAsync(
            string code,
            int timeoutMs = 8000,
            CancellationToken ct = default,
            int stdoutLimit = 3000,
            int stderrLimit = 2000)
        {
            if (string.IsNullOrWhiteSpace(code))
                return "Sandbox skipped: empty code.";

            var scriptPath = Path.Combine(_workspace, $"agent-{DateTime.Now:yyyyMMdd-HHmmss-fff}.py");
            await File.WriteAllTextAsync(scriptPath, code, Encoding.UTF8, ct).ConfigureAwait(false);

            var python = ResolvePythonCommand();
            if (python == null)
                return "Python sandbox unavailable: neither py nor python was found.";

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(timeoutMs);

            var startInfo = new ProcessStartInfo
            {
                FileName = python.Value.FileName,
                Arguments = $"{python.Value.ArgumentsPrefix}\"{scriptPath}\"",
                WorkingDirectory = _workspace,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return "Python sandbox failed: process did not start.";

            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);

            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return $"Python sandbox timeout after {timeoutMs} ms.";
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            return $"""
            exit={process.ExitCode}
            stdout:
            {Trim(stdout, stdoutLimit)}
            stderr:
            {Trim(stderr, stderrLimit)}
            """.Trim();
        }

        private static (string FileName, string ArgumentsPrefix)? ResolvePythonCommand()
        {
            if (CommandExists("py")) return ("py", "-3 ");
            if (CommandExists("python")) return ("python", "");
            return null;
        }

        private static bool CommandExists(string command)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (process == null) return false;
                process.WaitForExit(1500);
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static string Trim(string value, int max)
        {
            value ??= "";
            value = value.Trim();
            return value.Length <= max ? value : value[..max] + "...";
        }
    }
}
