using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public sealed class KokoCodeActValidation
    {
        public bool Allowed { get; init; }
        public string Reason { get; init; } = "";
    }

    public static class KokoCodeActPolicy
    {
        public const int MaxCodeChars = 16_000;

        private static readonly HashSet<string> AllowedImports = new(StringComparer.Ordinal)
        {
            "collections", "datetime", "decimal", "fractions", "functools", "itertools",
            "json", "math", "random", "re", "statistics", "string"
        };

        private static readonly Regex ImportPattern = new(
            @"(?m)^\s*(?:from\s+(?<from>[A-Za-z0-9_\.]+)\s+import|import\s+(?<imports>[^#\r\n]+))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ForbiddenPattern = new(
            @"(?ix)(?:\b(?:open|eval|exec|compile|input|breakpoint|globals|locals|vars|getattr|setattr|delattr|memoryview)\s*\(|__|\b(?:os|sys|subprocess|socket|pathlib|shutil|ctypes|winreg|requests|urllib|http|importlib|pickle|shelve)\b)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static KokoCodeActValidation Validate(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Deny("code is empty");
            if (code.Length > MaxCodeChars)
                return Deny($"code exceeds {MaxCodeChars} characters");
            if (code.IndexOf('\0') >= 0)
                return Deny("code contains a null character");

            var forbidden = ForbiddenPattern.Match(code);
            if (forbidden.Success)
                return Deny("forbidden host-access construct: " + forbidden.Value.Trim());

            foreach (Match match in ImportPattern.Matches(code))
            {
                var names = match.Groups["from"].Success
                    ? new[] { match.Groups["from"].Value }
                    : match.Groups["imports"].Value.Split(',').Select(item => item.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);
                foreach (var name in names)
                {
                    var root = name.Split('.')[0];
                    if (!AllowedImports.Contains(root))
                        return Deny("import is not allowed: " + root);
                }
            }

            return new KokoCodeActValidation { Allowed = true, Reason = "policy accepted" };
        }

        public static IReadOnlyCollection<string> GetAllowedImports() => AllowedImports.OrderBy(x => x).ToArray();

        private static KokoCodeActValidation Deny(string reason) => new() { Reason = reason };
    }

    public sealed class KokoCodeActExecutionResult
    {
        public bool Executed { get; set; }
        public bool Success { get; set; }
        public string Reason { get; set; } = "";
        public string Output { get; set; } = "";
        public string CodeArtifact { get; set; } = "";
        public string ResultArtifact { get; set; } = "";
    }

    public sealed class KokoCodeActToolHandler : IKokoToolHandler
    {
        private readonly string _workspace;
        private readonly KokoSandboxExecutor _sandbox;

        public KokoCodeActToolHandler(string dataDir)
        {
            _workspace = Path.Combine(Path.GetFullPath(dataDir), "codeact-runs");
            Directory.CreateDirectory(_workspace);
            _sandbox = new KokoSandboxExecutor(Path.Combine(_workspace, ".runtime"));
        }

        public string Name => "codeact_python";

        public async Task<KokoToolResult> ExecuteAsync(KokoToolCall call, CancellationToken ct)
        {
            var code = Arg(call, "code");
            var validation = KokoCodeActPolicy.Validate(code);
            if (!validation.Allowed)
                return Failure(call, "CodeAct blocked: " + validation.Reason);

            var runId = SanitizeSegment(Arg(call, "runId"), "adhoc");
            var callId = SanitizeSegment(call.Id, Guid.NewGuid().ToString("N")[..12]);
            var runDirectory = Path.Combine(_workspace, runId);
            Directory.CreateDirectory(runDirectory);
            var codePath = Path.Combine(runDirectory, callId + ".py");
            var resultPath = Path.Combine(runDirectory, callId + ".result.txt");
            await File.WriteAllTextAsync(codePath, code, Encoding.UTF8, ct).ConfigureAwait(false);

            var timeoutMs = int.TryParse(Arg(call, "timeoutMs"), out var parsedTimeout)
                ? Math.Clamp(parsedTimeout, 500, 15_000)
                : 8_000;
            var wrapped = BuildRestrictedWrapper(code);
            var output = await _sandbox.ExecutePythonAsync(
                wrapped,
                timeoutMs,
                ct,
                stdoutLimit: 12_000,
                stderrLimit: 8_000).ConfigureAwait(false);
            await File.WriteAllTextAsync(resultPath, output, Encoding.UTF8, ct).ConfigureAwait(false);

            var success = output.StartsWith("exit=0", StringComparison.Ordinal);
            var execution = new KokoCodeActExecutionResult
            {
                Executed = true,
                Success = success,
                Reason = success ? "CodeAct completed." : "CodeAct execution failed; evidence preserved.",
                Output = output,
                CodeArtifact = Path.GetRelativePath(_workspace, codePath),
                ResultArtifact = Path.GetRelativePath(_workspace, resultPath)
            };
            return new KokoToolResult
            {
                CallId = call.Id,
                ToolName = Name,
                Success = success,
                Verified = success,
                Reason = execution.Reason,
                Output = $"{output}\nartifacts: {execution.CodeArtifact}; {execution.ResultArtifact}".Trim(),
                RawResult = execution
            };
        }

        private static string BuildRestrictedWrapper(string code)
        {
            var encodedCode = JsonConvert.SerializeObject(code);
            var encodedImports = JsonConvert.SerializeObject(KokoCodeActPolicy.GetAllowedImports());
            return $$"""
            import ast
            import builtins

            _CODE = {{encodedCode}}
            _ALLOWED_IMPORTS = set({{encodedImports}})
            _SAFE_NAMES = (
                "abs", "all", "any", "bool", "dict", "enumerate", "Exception", "filter",
                "float", "int", "isinstance", "len", "list", "map", "max", "min", "pow",
                "print", "range", "reversed", "round", "set", "slice", "sorted", "str",
                "sum", "tuple", "TypeError", "ValueError", "zip"
            )

            def _safe_import(name, globals=None, locals=None, fromlist=(), level=0):
                root = name.split(".")[0]
                if root not in _ALLOWED_IMPORTS:
                    raise ImportError("module blocked by CodeAct policy: " + root)
                return builtins.__import__(name, globals, locals, fromlist, level)

            _safe_builtins = {name: getattr(builtins, name) for name in _SAFE_NAMES}
            _safe_builtins["__import__"] = _safe_import
            _scope = {"__builtins__": _safe_builtins}
            _tree = ast.parse(_CODE, filename="codeact-user.py", mode="exec")
            exec(compile(_tree, "codeact-user.py", "exec"), _scope, _scope)
            """;
        }

        private static KokoToolResult Failure(KokoToolCall call, string reason) => new()
        {
            CallId = call.Id,
            ToolName = "codeact_python",
            Reason = reason
        };

        private static string Arg(KokoToolCall call, string key)
            => call.Arguments.TryGetValue(key, out var value) ? value ?? "" : "";

        private static string SanitizeSegment(string? value, string fallback)
        {
            value = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            var safe = new string(value.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
            return string.IsNullOrWhiteSpace(safe) ? fallback : safe[..Math.Min(safe.Length, 64)];
        }
    }
}
