using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace KokonoeAssistant.Services
{
    public sealed class PcActionJournal
    {
        private readonly string _journalPath;

        public PcActionJournal(string? journalPath = null)
        {
            _journalPath = string.IsNullOrWhiteSpace(journalPath)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "KokonoeAssistant",
                    "PcActionJournal.jsonl")
                : journalPath;
        }

        public string JournalPath => _journalPath;

        public void Append(PcActionJournalEntry entry)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_journalPath) ?? ".");
            entry.Timestamp = entry.Timestamp == default ? DateTime.Now : entry.Timestamp;
            var json = JsonConvert.SerializeObject(entry, Formatting.None);
            File.AppendAllText(_journalPath, json + Environment.NewLine);
        }

        public void AppendDecision(
            PcActionPlan plan,
            PcPolicyDecision decision,
            string resultSummary = "",
            string error = "",
            bool? rollbackAvailable = null)
        {
            Append(new PcActionJournalEntry
            {
                ActionId = plan.Id,
                Timestamp = DateTime.Now,
                Intent = plan.Intent,
                RiskTier = decision.RiskTier.ToString(),
                Decision = decision.Kind.ToString(),
                ConfirmationRequired = decision.ConfirmationRequired,
                AffectedPaths = plan.AffectedPaths.ToList(),
                AffectedProcesses = plan.AffectedProcesses.ToList(),
                ResultSummary = resultSummary,
                Error = error,
                RollbackAvailable = rollbackAvailable ?? plan.RollbackAvailable
            });
        }

        public void AppendStatus(
            PcActionPlan plan,
            string status,
            string resultSummary = "",
            string error = "",
            bool confirmationRequired = false,
            bool? rollbackAvailable = null)
        {
            Append(new PcActionJournalEntry
            {
                ActionId = plan.Id,
                Timestamp = DateTime.Now,
                Intent = plan.Intent,
                RiskTier = plan.RiskTier.ToString(),
                Decision = status,
                ConfirmationRequired = confirmationRequired,
                AffectedPaths = plan.AffectedPaths.ToList(),
                AffectedProcesses = plan.AffectedProcesses.ToList(),
                ResultSummary = resultSummary,
                Error = error,
                RollbackAvailable = rollbackAvailable ?? plan.RollbackAvailable
            });
        }
    }

    public sealed class PcActionJournalEntry
    {
        public string ActionId { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Intent { get; set; } = "";
        public string RiskTier { get; set; } = "";
        public string Decision { get; set; } = "";
        public bool ConfirmationRequired { get; set; }
        public List<string> AffectedPaths { get; set; } = new();
        public List<string> AffectedProcesses { get; set; } = new();
        public string ResultSummary { get; set; } = "";
        public string Error { get; set; } = "";
        public bool RollbackAvailable { get; set; }
    }

    public sealed class PcRollbackService
    {
        private readonly string _backupRoot;

        public PcRollbackService(string? backupRoot = null)
        {
            _backupRoot = string.IsNullOrWhiteSpace(backupRoot)
                ? Path.Combine(Directory.GetCurrentDirectory(), ".kokonoe_backups")
                : backupRoot;
        }

        public string BackupRoot => _backupRoot;

        public PcRollbackBackupResult CreateFileBackup(string actionId, params string[] paths)
        {
            actionId = SanitizeActionId(actionId);
            var result = new PcRollbackBackupResult
            {
                ActionId = actionId,
                BackupDirectory = Path.Combine(_backupRoot, actionId)
            };

            Directory.CreateDirectory(result.BackupDirectory);

            try
            {
                var order = 0;
                foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var full = Path.GetFullPath(path);
                    if (!File.Exists(full))
                    {
                        result.Items.Add(new PcRollbackBackupItem
                        {
                            SourcePath = full,
                            Error = "file not found"
                        });
                        continue;
                    }

                    order++;
                    var backupName = $"{order:000}_{Path.GetFileName(full)}";
                    var backupPath = Path.Combine(result.BackupDirectory, backupName);
                    File.Copy(full, backupPath, overwrite: true);

                    result.Items.Add(new PcRollbackBackupItem
                    {
                        SourcePath = full,
                        BackupPath = backupPath,
                        Length = new FileInfo(full).Length,
                        Sha256 = ComputeSha256(full)
                    });
                }

                result.ManifestPath = Path.Combine(result.BackupDirectory, "manifest.json");
                File.WriteAllText(result.ManifestPath, JsonConvert.SerializeObject(result, Formatting.Indented));
                result.Success = result.Items.Count > 0 && result.Items.All(i => string.IsNullOrWhiteSpace(i.Error));
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.ManifestPath = Path.Combine(result.BackupDirectory, "manifest.json");
                File.WriteAllText(result.ManifestPath, JsonConvert.SerializeObject(result, Formatting.Indented));
                return result;
            }
        }

        private static string SanitizeActionId(string? actionId)
        {
            var clean = new string((actionId ?? "").Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
            return string.IsNullOrWhiteSpace(clean) ? Guid.NewGuid().ToString("N") : clean;
        }

        private static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    public sealed class PcRollbackBackupResult
    {
        public string ActionId { get; set; } = "";
        public string BackupDirectory { get; set; } = "";
        public string ManifestPath { get; set; } = "";
        public List<PcRollbackBackupItem> Items { get; set; } = new();
        public bool Success { get; set; }
        public string Error { get; set; } = "";
    }

    public sealed class PcRollbackBackupItem
    {
        public string SourcePath { get; set; } = "";
        public string BackupPath { get; set; } = "";
        public long Length { get; set; }
        public string Sha256 { get; set; } = "";
        public string Error { get; set; } = "";
    }
}
