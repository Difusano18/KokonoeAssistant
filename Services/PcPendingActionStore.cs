using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace KokonoeAssistant.Services
{
    public enum PcPendingActionStatus
    {
        Pending,
        Confirmed,
        Rejected,
        Expired,
        Cancelled
    }

    public sealed class PcPendingActionStore
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, PcPendingActionRecord> _pending = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _path;
        private readonly TimeSpan _riskyExpiry;
        private readonly TimeSpan _externalExpiry;
        private readonly Func<DateTime> _clock;
        private bool _loaded;
        private static readonly JsonSerializerSettings EventJsonSettings = new()
        {
            Converters = { new StringEnumConverter() }
        };

        public PcPendingActionStore(
            string? path = null,
            TimeSpan? riskyLocalExpiry = null,
            TimeSpan? externalExpiry = null,
            Func<DateTime>? clock = null)
        {
            _path = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "KokonoeAssistant",
                    "PcPendingActions.jsonl")
                : path;
            _riskyExpiry = riskyLocalExpiry ?? TimeSpan.FromMinutes(5);
            _externalExpiry = externalExpiry ?? TimeSpan.FromMinutes(2);
            _clock = clock ?? (() => DateTime.UtcNow);
        }

        public string StorePath => _path;

        public PcPendingActionRecord Save(PcActionPlan plan, PcPolicyDecision decision)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            EnsureLoaded();

            var now = _clock();
            var risk = decision.RiskTier > plan.RiskTier ? decision.RiskTier : plan.RiskTier;
            var record = new PcPendingActionRecord
            {
                ActionId = NormalizeActionId(plan.Id),
                PlanHash = ComputePlanHash(plan),
                CreatedAt = now,
                ExpiresAt = now + GetExpiry(risk),
                RiskTier = risk,
                UserFacingSummary = string.IsNullOrWhiteSpace(plan.UserFacingSummaryUk)
                    ? plan.Intent
                    : plan.UserFacingSummaryUk,
                OriginalPlan = ClonePlan(plan)
            };

            lock (_lock)
            {
                _pending[record.ActionId] = record;
                AppendEventLocked(record, PcPendingActionStatus.Pending, "pending confirmation", "");
            }

            return record;
        }

        public PcPendingActionLookupResult Get(string? actionId)
        {
            EnsureLoaded();
            var id = NormalizeActionId(actionId);
            if (string.IsNullOrWhiteSpace(id))
                return PcPendingActionLookupResult.Missing(id);

            lock (_lock)
            {
                if (!_pending.TryGetValue(id, out var record))
                    return PcPendingActionLookupResult.Missing(id);

                if (record.ExpiresAt <= _clock())
                {
                    _pending.Remove(id);
                    AppendEventLocked(record, PcPendingActionStatus.Expired, "pending action expired", "");
                    return PcPendingActionLookupResult.FromExpired(record);
                }

                return PcPendingActionLookupResult.Found(record);
            }
        }

        public void MarkConfirmed(PcPendingActionRecord record, string confirmationText, string resultSummary)
            => Complete(record, PcPendingActionStatus.Confirmed, resultSummary, confirmationText);

        public void MarkRejected(PcPendingActionRecord record, string reason, string confirmationText)
        {
            EnsureLoaded();
            lock (_lock)
            {
                AppendEventLocked(record, PcPendingActionStatus.Rejected, reason, confirmationText);
            }
        }

        public bool Cancel(string? actionId, string reason, out PcPendingActionRecord? record)
        {
            EnsureLoaded();
            var id = NormalizeActionId(actionId);
            lock (_lock)
            {
                if (!_pending.TryGetValue(id, out record))
                    return false;

                _pending.Remove(id);
                AppendEventLocked(record, PcPendingActionStatus.Cancelled, reason, "");
                return true;
            }
        }

        public void ForceExpire(string? actionId, DateTime? expiresAt = null)
        {
            EnsureLoaded();
            var id = NormalizeActionId(actionId);
            lock (_lock)
            {
                if (_pending.TryGetValue(id, out var record))
                    record.ExpiresAt = expiresAt ?? _clock().AddSeconds(-1);
            }
        }

        public int Count
        {
            get
            {
                EnsureLoaded();
                lock (_lock) { return _pending.Count; }
            }
        }

        public static string ComputePlanHash(PcActionPlan plan)
        {
            var canonical = new
            {
                plan.Intent,
                plan.RiskTier,
                Actions = plan.Actions
                    .OrderBy(a => a.Order)
                    .Select(a => new
                    {
                        a.Order,
                        ActionType = NormalizeActionType(a.ActionType),
                        Target = (a.Target ?? "").Trim(),
                        Arguments = a.Arguments
                            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                            .Select(kv => new { Key = kv.Key.ToLowerInvariant(), Value = kv.Value ?? "" })
                            .ToList()
                    })
                    .ToList(),
                Paths = plan.AffectedPaths.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                Processes = plan.AffectedProcesses.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
            };

            var json = JsonConvert.SerializeObject(canonical, Formatting.None);
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private void Complete(PcPendingActionRecord record, PcPendingActionStatus status, string reason, string confirmationText)
        {
            EnsureLoaded();
            var id = NormalizeActionId(record.ActionId);
            lock (_lock)
            {
                _pending.Remove(id);
                AppendEventLocked(record, status, reason, confirmationText);
            }
        }

        private void EnsureLoaded()
        {
            lock (_lock)
            {
                if (_loaded)
                    return;

                _loaded = true;
                if (!File.Exists(_path))
                    return;

                foreach (var line in File.ReadLines(_path))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var evt = JsonConvert.DeserializeObject<PcPendingActionEvent>(line, EventJsonSettings);
                        var record = evt?.Record;
                        if (evt == null || record == null || string.IsNullOrWhiteSpace(record.ActionId))
                            continue;

                        if (evt.Status == PcPendingActionStatus.Pending)
                            _pending[NormalizeActionId(record.ActionId)] = record;
                        else if (evt.Status is PcPendingActionStatus.Confirmed or PcPendingActionStatus.Cancelled or PcPendingActionStatus.Expired)
                            _pending.Remove(NormalizeActionId(record.ActionId));
                    }
                    catch
                    {
                        // Corrupt audit lines must not break PC safety. Ignore and keep loading the rest.
                    }
                }

                var now = _clock();
                foreach (var expired in _pending.Values.Where(r => r.ExpiresAt <= now).ToList())
                {
                    _pending.Remove(NormalizeActionId(expired.ActionId));
                    AppendEventLocked(expired, PcPendingActionStatus.Expired, "pending action expired during load", "");
                }
            }
        }

        private void AppendEventLocked(PcPendingActionRecord record, PcPendingActionStatus status, string reason, string confirmationText)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
            var evt = new PcPendingActionEvent
            {
                Timestamp = _clock(),
                Status = status,
                ActionId = NormalizeActionId(record.ActionId),
                PlanHash = record.PlanHash,
                RiskTier = record.RiskTier,
                Reason = reason ?? "",
                ConfirmationText = confirmationText ?? "",
                Record = CloneRecord(record)
            };
            File.AppendAllText(_path, JsonConvert.SerializeObject(evt, Formatting.None, EventJsonSettings) + Environment.NewLine);
        }

        private TimeSpan GetExpiry(PcActionRiskTier risk)
            => risk == PcActionRiskTier.ExternalOrIrreversible ? _externalExpiry : _riskyExpiry;

        private static PcActionPlan ClonePlan(PcActionPlan plan)
            => JsonConvert.DeserializeObject<PcActionPlan>(JsonConvert.SerializeObject(plan)) ?? plan;

        private static PcPendingActionRecord CloneRecord(PcPendingActionRecord record)
            => JsonConvert.DeserializeObject<PcPendingActionRecord>(JsonConvert.SerializeObject(record)) ?? record;

        private static string NormalizeActionId(string? actionId)
            => new string((actionId ?? "").Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());

        private static string NormalizeActionType(string? actionType)
            => new string((actionType ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    public sealed class PcPendingActionRecord
    {
        public string ActionId { get; set; } = "";
        public string PlanHash { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public PcActionRiskTier RiskTier { get; set; }
        public string UserFacingSummary { get; set; } = "";
        public PcActionPlan OriginalPlan { get; set; } = new();
    }

    public sealed class PcPendingActionEvent
    {
        public DateTime Timestamp { get; set; }
        public PcPendingActionStatus Status { get; set; }
        public string ActionId { get; set; } = "";
        public string PlanHash { get; set; } = "";
        public PcActionRiskTier RiskTier { get; set; }
        public string Reason { get; set; } = "";
        public string ConfirmationText { get; set; } = "";
        public PcPendingActionRecord? Record { get; set; }
    }

    public sealed class PcPendingActionLookupResult
    {
        public bool Exists { get; private init; }
        public bool Expired { get; private init; }
        public string ActionId { get; private init; } = "";
        public PcPendingActionRecord? Record { get; private init; }

        public static PcPendingActionLookupResult Found(PcPendingActionRecord record)
            => new() { Exists = true, ActionId = record.ActionId, Record = record };

        public static PcPendingActionLookupResult Missing(string actionId)
            => new() { Exists = false, ActionId = actionId };

        public static PcPendingActionLookupResult FromExpired(PcPendingActionRecord record)
            => new() { Exists = true, Expired = true, ActionId = record.ActionId, Record = record };
    }
}
