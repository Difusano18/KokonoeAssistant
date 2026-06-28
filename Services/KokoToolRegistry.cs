using System;
using System.Collections.Generic;
using System.Linq;

namespace KokonoeAssistant.Services
{
    public enum KokoToolRisk
    {
        ReadOnly,
        Write,
        Destructive,
        Privileged
    }

    public sealed class KokoToolDescriptor
    {
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public string Category { get; init; } = "general";
        public KokoToolRisk Risk { get; init; } = KokoToolRisk.ReadOnly;
        public bool RequiresConfirmation { get; init; }
        public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();
    }

    public sealed class KokoToolRegistry
    {
        private sealed record Entry(IKokoToolHandler Handler, KokoToolDescriptor Descriptor);

        private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public IReadOnlyCollection<KokoToolDescriptor> Tools
        {
            get
            {
                lock (_lock)
                    return _entries.Values.Select(entry => entry.Descriptor).OrderBy(tool => tool.Name).ToArray();
            }
        }

        public void Register(IKokoToolHandler handler, KokoToolDescriptor? descriptor = null)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var name = handler.Name?.Trim() ?? "";
            if (name.Length == 0) throw new ArgumentException("Tool handler name is empty.", nameof(handler));

            descriptor ??= InferDescriptor(name);
            if (!string.Equals(name, descriptor.Name?.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Tool descriptor name '{descriptor.Name}' does not match handler '{name}'.", nameof(descriptor));

            lock (_lock)
                _entries[name] = new Entry(handler, descriptor);
        }

        public bool TryResolve(string? name, out IKokoToolHandler? handler)
        {
            lock (_lock)
            {
                if (_entries.TryGetValue(name?.Trim() ?? "", out var entry))
                {
                    handler = entry.Handler;
                    return true;
                }
            }

            handler = null;
            return false;
        }

        public IReadOnlyCollection<KokoToolDescriptor> SelectForCapabilities(IEnumerable<string>? capabilities)
        {
            var requested = new HashSet<string>(
                capabilities?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim())
                    ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            var tools = Tools;
            if (requested.Count == 0 || requested.Overlaps(new[] { "routing", "final-approval", "all-tools" }))
                return tools;

            return tools
                .Where(tool => tool.Capabilities.Count == 0 || tool.Capabilities.Any(requested.Contains))
                .ToArray();
        }

        public string BuildPromptBlock(IEnumerable<string>? capabilities = null)
        {
            var tools = SelectForCapabilities(capabilities);
            if (tools.Count == 0)
                return "AVAILABLE TOOLS\n- none for this agent capability scope";

            return "AVAILABLE TOOLS\n" + string.Join("\n", tools.Select(tool =>
            {
                var args = tool.Arguments.Count == 0 ? "none" : string.Join(", ", tool.Arguments);
                var confirmation = tool.RequiresConfirmation ? "; confirmation required" : "";
                return $"- {tool.Name} [{tool.Category}/{tool.Risk}]{confirmation}; args: {args}. {tool.Description}";
            }));
        }

        public static KokoToolDescriptor InferDescriptor(string name) => name switch
        {
            "fs_read_text" => Descriptor(name, "Read UTF-8 text from any path on disk.", "filesystem", KokoToolRisk.ReadOnly, false, new[] { "path" }, "files", "vault-read", "code", "sandbox"),
            "fs_write_text" => Descriptor(name, "Write UTF-8 text to any path on disk.", "filesystem", KokoToolRisk.Write, true, new[] { "path", "content" }, "files", "vault-write", "code"),
            "fs_create_directory" => Descriptor(name, "Create a directory anywhere on disk.", "filesystem", KokoToolRisk.Write, false, new[] { "path" }, "files", "vault-write", "code"),
            "fs_move" => Descriptor(name, "Move a path anywhere on disk.", "filesystem", KokoToolRisk.Destructive, true, new[] { "path", "destinationPath" }, "files", "vault-write", "maintenance"),
            "fs_delete" => Descriptor(name, "Delete a path anywhere on disk.", "filesystem", KokoToolRisk.Destructive, true, new[] { "path" }, "files", "maintenance"),
            "fs_list_directory" => Descriptor(name, "List files and subdirectories at a given path on disk.", "filesystem", KokoToolRisk.ReadOnly, false, new[] { "path" }, "files", "vault-read", "code"),
            "pc_action" => Descriptor(name, "Execute a policy-checked local PC action plan.", "pc", KokoToolRisk.Privileged, false, new[] { "payload:PcActionPlan" }, "processes", "maintenance", "permission-protocol"),
            "pc_confirm" => Descriptor(name, "Confirm and execute a pending PC action.", "pc", KokoToolRisk.Privileged, true, new[] { "actionId", "confirmationText" }, "processes", "maintenance", "permission-protocol"),
            "pc_cancel" => Descriptor(name, "Cancel a pending PC action.", "pc", KokoToolRisk.Write, false, new[] { "actionId", "reason" }, "processes", "maintenance", "permission-protocol"),
            "codeact_python" => Descriptor(name, "Execute restricted Python and preserve code/result artifacts.", "code", KokoToolRisk.Write, false, new[] { "code", "runId", "timeoutMs" }, "code", "tests", "sandbox"),
            "delegate_to_agent" => Descriptor(name, "Execute a sub-task using a specialist agent from the pool. Returns the agent's full response as a string.", "agents", KokoToolRisk.Write, false, new[] { "agentId", "systemPrompt", "userMessage" }, "agents", "delegation"),
            "web_search" => Descriptor(name, "Search the web via Tavily. Returns title, url, and snippet for each result.", "research", KokoToolRisk.ReadOnly, false, new[] { "query", "maxResults" }, "research", "web-search"),
            "browser.navigate" => Descriptor(name, "Open a URL in the real Chromium browser. The window is visible to the user.", "browser", KokoToolRisk.Write, false, new[] { "url" }, "browser"),
            "browser.click" => Descriptor(name, "Click an element by CSS selector or Playwright text=... selector.", "browser", KokoToolRisk.Write, false, new[] { "selector" }, "browser"),
            "browser.type" => Descriptor(name, "Type text into an input field by CSS selector.", "browser", KokoToolRisk.Write, false, new[] { "selector", "text" }, "browser"),
            "browser.extract" => Descriptor(name, "Extract visible text from the page, or from a specific selector. Omit selector for the whole page.", "browser", KokoToolRisk.ReadOnly, false, new[] { "selector" }, "browser"),
            "browser.screenshot" => Descriptor(name, "Capture a screenshot of the current page and return its file path.", "browser", KokoToolRisk.ReadOnly, false, Array.Empty<string>(), "browser"),
            "browser.scroll" => Descriptor(name, "Scroll the page up or down by a pixel amount.", "browser", KokoToolRisk.Write, false, new[] { "direction", "pixels" }, "browser"),
            "browser.wait_for" => Descriptor(name, "Wait for an element to appear, e.g. after a click triggers loading.", "browser", KokoToolRisk.ReadOnly, false, new[] { "selector", "timeoutMs" }, "browser"),
            "browser.close" => Descriptor(name, "Close the browser session.", "browser", KokoToolRisk.Write, false, Array.Empty<string>(), "browser"),
            "artifact.save" => Descriptor(name, "Save a research result, extracted data, code, or note as a named artifact file the user can open. kind: markdown/html/csv/json/patch/note (default plain text).", "artifacts", KokoToolRisk.Write, false, new[] { "title", "content", "kind", "sourceUrl" }, "artifacts"),
            _ => Descriptor(name, "Registered runtime tool.", "general", KokoToolRisk.ReadOnly, false, Array.Empty<string>())
        };

        private static KokoToolDescriptor Descriptor(
            string name,
            string description,
            string category,
            KokoToolRisk risk,
            bool requiresConfirmation,
            IReadOnlyList<string> arguments,
            params string[] capabilities) => new()
        {
            Name = name,
            Description = description,
            Category = category,
            Risk = risk,
            RequiresConfirmation = requiresConfirmation,
            Arguments = arguments,
            Capabilities = capabilities
        };
    }
}
