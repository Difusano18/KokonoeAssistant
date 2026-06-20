using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public sealed class KokoAgentRoleDefinition
    {
        public string RoleId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string DefaultAgentId { get; set; } = "";
        public string DefaultModel { get; set; } = "";
        public double Temperature { get; set; } = 0.55;
        public List<string> Capabilities { get; set; } = new();
        public string Directive { get; set; } = "";
    }

    public sealed class KokoManagedAgentDescriptor
    {
        public string AgentId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string RoleId { get; set; } = "";
        public string Provider { get; set; } = "";
        public string Model { get; set; } = "";
        public double Temperature { get; set; }
        public bool Enabled { get; set; } = true;
        public string Status { get; set; } = "ready";
        public string LastEvent { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public List<string> Capabilities { get; set; } = new();
    }

    public sealed class KokoAgentFactorySnapshot
    {
        public DateTime TakenAt { get; set; } = DateTime.Now;
        public List<KokoAgentRoleDefinition> Roles { get; set; } = new();
        public List<KokoManagedAgentDescriptor> Agents { get; set; } = new();
        public List<BlackboardEvent> BlackboardRecent { get; set; } = new();
        public KokoAgentTaskSnapshot? TaskBoard { get; set; }
    }

    public sealed class KokoDynamicAgentFactoryService
    {
        private readonly object _lock = new();
        private readonly string _path;
        private readonly KokoInternalBlackboardService? _blackboard;
        private readonly Func<KokoAgentTaskService?> _tasks;
        private readonly Func<LlmService?> _llm;
        private readonly List<KokoManagedAgentDescriptor> _agents = new();

        public KokoDynamicAgentFactoryService(
            string dataDir,
            KokoInternalBlackboardService? blackboard = null,
            Func<KokoAgentTaskService?>? tasks = null,
            Func<LlmService?>? llm = null)
        {
            Directory.CreateDirectory(dataDir);
            _path = Path.Combine(dataDir, "genesis-agents.json");
            _blackboard = blackboard;
            _tasks = tasks ?? (() => null);
            _llm = llm ?? (() => null);
            Load();
            EnsureBuiltInAgents();
        }

        public IReadOnlyList<KokoAgentRoleDefinition> Roles => BuildRoleCatalog();

        public KokoManagedAgentDescriptor CreateOrUpdateAgent(
            string agentId,
            string roleId,
            string? displayName = null,
            string? provider = null,
            string? model = null,
            double? temperature = null)
        {
            var settings = AppSettings.Load();
            return CreateOrUpdateAgent(settings, agentId, roleId, displayName, provider, model, temperature, saveSettings: true);
        }

        public KokoManagedAgentDescriptor CreateOrUpdateAgent(
            AppSettings settings,
            string agentId,
            string roleId,
            string? displayName = null,
            string? provider = null,
            string? model = null,
            double? temperature = null,
            bool saveSettings = false)
        {
            var role = ResolveRole(roleId);
            agentId = NormalizeAgentId(agentId, role.DefaultAgentId);
            var profile = GetOrCreateProfile(settings, agentId);

            var resolvedProvider = string.IsNullOrWhiteSpace(provider) ? settings.LlmProvider : provider.Trim();
            var resolvedModel = string.IsNullOrWhiteSpace(model)
                ? ResolveFallbackModel(settings, resolvedProvider, role)
                : model.Trim();
            var resolvedTemperature = Math.Clamp(temperature ?? role.Temperature, 0.0, 2.0);

            profile.AgentId = agentId;
            profile.Enabled = true;
            profile.LlmProvider = resolvedProvider;
            profile.Model = resolvedModel;
            profile.Temperature = resolvedTemperature;
            settings.AgentLlmProfiles[agentId] = profile;
            if (saveSettings)
                settings.Save();

            KokoManagedAgentDescriptor descriptor;
            lock (_lock)
            {
                descriptor = _agents.FirstOrDefault(a => a.AgentId.Equals(agentId, StringComparison.OrdinalIgnoreCase))
                    ?? new KokoManagedAgentDescriptor
                    {
                        AgentId = agentId,
                        CreatedAt = DateTime.Now
                    };

                descriptor.DisplayName = string.IsNullOrWhiteSpace(displayName) ? role.DisplayName : displayName.Trim();
                descriptor.RoleId = role.RoleId;
                descriptor.Provider = resolvedProvider;
                descriptor.Model = resolvedModel;
                descriptor.Temperature = resolvedTemperature;
                descriptor.Enabled = true;
                descriptor.Status = "ready";
                descriptor.LastEvent = "profile registered";
                descriptor.UpdatedAt = DateTime.Now;
                descriptor.Capabilities = role.Capabilities.ToList();

                var existingIndex = _agents.FindIndex(a => a.AgentId.Equals(agentId, StringComparison.OrdinalIgnoreCase));
                if (existingIndex >= 0)
                    _agents[existingIndex] = descriptor;
                else
                    _agents.Add(descriptor);

                SaveLocked();
            }

            if (saveSettings)
                _llm()?.ReloadSettings();
            Publish("agent_registered", $"{agentId} as {role.RoleId} using {resolvedProvider}/{resolvedModel}", 0.72, descriptor);
            return Clone(descriptor);
        }

        public bool SetAgentEnabled(string agentId, bool enabled)
        {
            agentId = NormalizeAgentId(agentId, "");
            if (string.IsNullOrWhiteSpace(agentId))
                return false;

            var settings = AppSettings.Load();
            if (settings.AgentLlmProfiles.TryGetValue(agentId, out var profile))
            {
                profile.Enabled = enabled;
                settings.Save();
            }

            var changed = false;
            lock (_lock)
            {
                var descriptor = _agents.FirstOrDefault(a => a.AgentId.Equals(agentId, StringComparison.OrdinalIgnoreCase));
                if (descriptor != null)
                {
                    descriptor.Enabled = enabled;
                    descriptor.Status = enabled ? "ready" : "disabled";
                    descriptor.LastEvent = enabled ? "reactivated" : "disabled";
                    descriptor.UpdatedAt = DateTime.Now;
                    changed = true;
                    SaveLocked();
                }
            }

            if (changed || profile != null)
            {
                _llm()?.ReloadSettings();
                Publish(enabled ? "agent_enabled" : "agent_disabled", $"{agentId} {(enabled ? "enabled" : "disabled")}", enabled ? 0.58 : 0.42);
                return true;
            }

            return false;
        }

        public KokoAgentFactorySnapshot GetSnapshot()
        {
            lock (_lock)
            {
                return new KokoAgentFactorySnapshot
                {
                    Roles = BuildRoleCatalog().Select(Clone).ToList(),
                    Agents = _agents
                        .OrderByDescending(a => a.Enabled)
                        .ThenBy(a => a.RoleId)
                        .ThenBy(a => a.AgentId)
                        .Select(Clone)
                        .ToList(),
                    BlackboardRecent = _blackboard?.Recent(10).ToList() ?? new List<BlackboardEvent>(),
                    TaskBoard = SafeTaskSnapshot()
                };
            }
        }

        public string RenderConsole()
        {
            var snap = GetSnapshot();
            var sb = new StringBuilder();
            sb.AppendLine($"GENESIS FABRIC | agents {snap.Agents.Count(a => a.Enabled)}/{snap.Agents.Count} | roles {snap.Roles.Count}");
            sb.AppendLine("roles: " + string.Join(", ", snap.Roles.Select(r => r.RoleId)));

            if (snap.Agents.Count == 0)
            {
                sb.AppendLine("no managed agents yet");
            }
            else
            {
                foreach (var agent in snap.Agents.Take(10))
                {
                    var caps = agent.Capabilities.Count == 0 ? "-" : string.Join("/", agent.Capabilities.Take(4));
                    sb.AppendLine($"{(agent.Enabled ? "ON " : "OFF")} {agent.AgentId} :: {agent.RoleId} :: {agent.Provider}/{agent.Model} :: t={agent.Temperature:0.##} :: {caps}");
                }
            }

            if (snap.TaskBoard != null)
                sb.AppendLine($"tasks: {snap.TaskBoard.Tasks.Count} | running {snap.TaskBoard.RunningSteps}/{snap.TaskBoard.MaxParallel} | {snap.TaskBoard.Activity.Phase}/{snap.TaskBoard.Activity.Tool}");

            var recent = snap.BlackboardRecent.TakeLast(5).ToList();
            if (recent.Count > 0)
            {
                sb.AppendLine("blackboard:");
                foreach (var ev in recent)
                    sb.AppendLine($"- {ev.At:HH:mm:ss} {ev.Agent}/{ev.Kind} p={ev.Priority:0.00}: {Trim(ev.Summary, 110)}");
            }

            return sb.ToString().Trim();
        }

        private void EnsureBuiltInAgents()
        {
            var settings = AppSettings.Load();
            var changed = false;
            foreach (var role in BuildRoleCatalog().Where(r => IsBuiltInAgent(r.DefaultAgentId)))
            {
                var profile = GetOrCreateProfile(settings, role.DefaultAgentId);
                if (string.IsNullOrWhiteSpace(profile.Model))
                {
                    profile.Model = ResolveFallbackModel(settings, string.IsNullOrWhiteSpace(profile.LlmProvider) ? settings.LlmProvider : profile.LlmProvider, role);
                    changed = true;
                }
                if (!profile.Temperature.HasValue)
                {
                    profile.Temperature = role.Temperature;
                    changed = true;
                }
            }
            if (changed)
                settings.Save();
        }

        private KokoAgentTaskSnapshot? SafeTaskSnapshot()
        {
            try { return _tasks()?.GetSnapshot(); }
            catch { return null; }
        }

        private void Publish(string kind, string summary, double priority, object? payload = null)
        {
            try { _blackboard?.Publish("GenesisFactory", kind, summary, priority, payload); } catch (Exception suppressedEx270) { KokoSystemLog.Write("DYNAMICAGENTFACTORYSERVICE-CATCH", "Publish failed near source line 270: " + suppressedEx270); }
            KokoSystemLog.Write("GENESIS", summary);
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_path))
                    return;
                var loaded = JsonConvert.DeserializeObject<List<KokoManagedAgentDescriptor>>(File.ReadAllText(_path));
                if (loaded == null)
                    return;
                lock (_lock)
                {
                    _agents.Clear();
                    _agents.AddRange(loaded.Where(a => !string.IsNullOrWhiteSpace(a.AgentId)));
                }
            }
            catch (Exception ex)
            {
                KokoSystemLog.Write("GENESIS", "catalog load failed: " + ex.Message);
            }
        }

        private void SaveLocked()
        {
            File.WriteAllText(_path, JsonConvert.SerializeObject(_agents, Formatting.Indented));
        }

        private static KokoAgentLlmProfile GetOrCreateProfile(AppSettings settings, string agentId)
        {
            settings.AgentLlmProfiles ??= new Dictionary<string, KokoAgentLlmProfile>(StringComparer.OrdinalIgnoreCase);
            if (!settings.AgentLlmProfiles.TryGetValue(agentId, out var profile) || profile == null)
            {
                profile = new KokoAgentLlmProfile { AgentId = agentId, Enabled = true };
                settings.AgentLlmProfiles[agentId] = profile;
            }
            profile.OllamaKeys ??= new List<OllamaKeyEntry>();
            return profile;
        }

        private static string ResolveFallbackModel(AppSettings settings, string provider, KokoAgentRoleDefinition role)
        {
            if (!string.IsNullOrWhiteSpace(role.DefaultModel))
                return role.DefaultModel;
            if (provider.Equals("claude", StringComparison.OrdinalIgnoreCase))
                return settings.ClaudeModel;
            if (provider.Equals("ollama-cloud", StringComparison.OrdinalIgnoreCase) ||
                provider.Equals("ollama", StringComparison.OrdinalIgnoreCase))
                return settings.OllamaModel;
            return settings.Model;
        }

        private static KokoAgentRoleDefinition ResolveRole(string? roleId)
        {
            var roles = BuildRoleCatalog();
            return roles.FirstOrDefault(r => r.RoleId.Equals(roleId?.Trim() ?? "", StringComparison.OrdinalIgnoreCase))
                ?? roles.First(r => r.RoleId == "analyst");
        }

        private static string NormalizeAgentId(string value, string fallback)
        {
            value = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            value = value.ToLowerInvariant();
            var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
            value = new string(chars);
            while (value.Contains("--", StringComparison.Ordinal))
                value = value.Replace("--", "-", StringComparison.Ordinal);
            value = value.Trim('-');
            return string.IsNullOrWhiteSpace(value) ? fallback : value[..Math.Min(value.Length, 48)];
        }

        private static bool IsBuiltInAgent(string agentId)
            => agentId is "chat" or "coder" or "research" or "obsidian" or "system" or "vision-observer" or "system-overlord";

        private static List<KokoAgentRoleDefinition> BuildRoleCatalog() => new()
        {
            new KokoAgentRoleDefinition
            {
                RoleId = "orchestrator",
                DisplayName = "Kokonoe Orchestrator",
                DefaultAgentId = "system",
                Temperature = 0.25,
                Capabilities = new() { "routing", "critique", "safety", "final-approval" },
                Directive = "High-priority coordinator. Chooses the route, rejects weak premises, finalizes visible output."
            },
            new KokoAgentRoleDefinition
            {
                RoleId = "analyst",
                DisplayName = "Analyst",
                DefaultAgentId = "research",
                Temperature = 0.45,
                Capabilities = new() { "research", "ranking", "critique", "synthesis" },
                Directive = "Turns messy context into ranked findings and concrete next actions."
            },
            new KokoAgentRoleDefinition
            {
                RoleId = "coder",
                DisplayName = "Code Operator",
                DefaultAgentId = "coder",
                DefaultModel = "qwen3-coder:480b-cloud",
                Temperature = 0.30,
                Capabilities = new() { "code", "tests", "sandbox", "patch-review" },
                Directive = "Prefers buildable code, tests, and boring correctness over shiny nonsense."
            },
            new KokoAgentRoleDefinition
            {
                RoleId = "vault",
                DisplayName = "Obsidian Archivist",
                DefaultAgentId = "obsidian",
                Temperature = 0.35,
                Capabilities = new() { "vault-read", "vault-write", "memory", "linking" },
                Directive = "Reads before writing. Updates memory with facts, not roleplay confetti."
            },
            new KokoAgentRoleDefinition
            {
                RoleId = "vision",
                DisplayName = "Vision Observer",
                DefaultAgentId = "vision-observer",
                Temperature = 0.40,
                Capabilities = new() { "screen", "ocr", "visual-context", "flow" },
                Directive = "Extracts concrete screen evidence and ignores stale chat noise."
            },
            new KokoAgentRoleDefinition
            {
                RoleId = "overlord",
                DisplayName = "System Overlord",
                DefaultAgentId = "system-overlord",
                Temperature = 0.25,
                Capabilities = new() { "files", "metadata", "processes", "permission-protocol", "maintenance" },
                Directive = "Controls local system work through policy and explicit confirmation. No reckless deletes, no fake execution."
            },
            new KokoAgentRoleDefinition
            {
                RoleId = "creative",
                DisplayName = "Creative Synthesis",
                DefaultAgentId = "creative",
                Temperature = 0.90,
                Capabilities = new() { "ideation", "tone", "narrative", "alternatives" },
                Directive = "Generates options, but Kokonoe keeps the scalpel."
            }
        };

        private static string Trim(string value, int max)
        {
            value = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return value.Length <= max ? value : value[..Math.Max(0, max - 1)].TrimEnd() + "...";
        }

        private static KokoManagedAgentDescriptor Clone(KokoManagedAgentDescriptor item)
            => JsonConvert.DeserializeObject<KokoManagedAgentDescriptor>(JsonConvert.SerializeObject(item)) ?? item;

        private static KokoAgentRoleDefinition Clone(KokoAgentRoleDefinition item)
            => JsonConvert.DeserializeObject<KokoAgentRoleDefinition>(JsonConvert.SerializeObject(item)) ?? item;
    }
}
