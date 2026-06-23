using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace KokonoeAssistant.Services
{
    public sealed class KokoWebVaultBridgeService : IDisposable
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
        private readonly KokoWebBridgeService _bridge;
        private readonly ObsidianMcpService _vault;
        private readonly SemaphoreSlim _scanGate = new(1, 1);
        private readonly object _cacheLock = new();
        private object? _cachedStatus;
        private DateTime _cachedAtUtc;
        private bool _disposed;

        public KokoWebVaultBridgeService(KokoWebBridgeService bridge, ObsidianMcpService vault)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _vault = vault ?? throw new ArgumentNullException(nameof(vault));
            _bridge.Register("vault.status", (_, ct) => GetStatusAsync(force: false, publish: false, ct));
            _bridge.Register("vault.refresh", (_, ct) => GetStatusAsync(force: true, publish: true, ct));
            _bridge.Register("vault_status", (_, ct) => GetStatusAsync(force: false, publish: false, ct));
        }

        private async Task<object?> GetStatusAsync(bool force, bool publish, CancellationToken ct)
        {
            await Task.Yield();

            if (_disposed)
                throw new ObjectDisposedException(nameof(KokoWebVaultBridgeService));

            lock (_cacheLock)
            {
                if (!force && _cachedStatus != null && DateTime.UtcNow - _cachedAtUtc <= CacheTtl)
                    return _cachedStatus;
            }

            await _scanGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                lock (_cacheLock)
                {
                    if (!force && _cachedStatus != null && DateTime.UtcNow - _cachedAtUtc <= CacheTtl)
                        return _cachedStatus;
                }

                var status = await Task.Run(BuildStatus, ct).ConfigureAwait(false);
                lock (_cacheLock)
                {
                    _cachedStatus = status;
                    _cachedAtUtc = DateTime.UtcNow;
                }

                if (publish && !_disposed)
                    _bridge.Publish("vault.status", status);
                return status;
            }
            finally
            {
                _scanGate.Release();
            }
        }

        private object BuildStatus()
        {
            var sw = Stopwatch.StartNew();
            var root = _vault.VaultPath;
            var available = Directory.Exists(root);
            var notes = available ? _vault.ListNotes() : new();
            var folders = available ? _vault.ListFolders() : new();
            var recent = notes
                .Select(path => new
                {
                    path,
                    modifiedAt = SafeLastWriteUtc(root, path)
                })
                .OrderByDescending(item => item.modifiedAt)
                .Take(5)
                .ToArray();
            sw.Stop();

            var status = new
            {
                available,
                path = root,
                noteCount = notes.Count,
                folderCount = folders.Count,
                recentNotes = recent,
                scannedAt = DateTime.UtcNow,
                scanMs = sw.ElapsedMilliseconds
            };
            KokoSystemLog.Write("WEB-VAULT", $"status available={available} notes={notes.Count} folders={folders.Count} scanMs={sw.ElapsedMilliseconds}");
            return status;
        }

        private static DateTime SafeLastWriteUtc(string root, string relativePath)
        {
            try
            {
                var full = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(full) ? File.GetLastWriteTimeUtc(full) : DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
