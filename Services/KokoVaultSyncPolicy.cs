using System;

namespace KokonoeAssistant.Services
{
    public static class KokoVaultSyncPolicy
    {
        public static bool ShouldFlush(int pendingCount, DateTime lastSyncAt, DateTime now, TimeSpan staleAfter)
        {
            if (pendingCount <= 0)
                return false;
            if (pendingCount >= 5)
                return true;
            if (lastSyncAt <= DateTime.MinValue)
                return true;
            return now - lastSyncAt >= staleAfter;
        }
    }
}
