using System;

namespace KokonoeAssistant.Services
{
    public static class KokoWearableTrust
    {
        public static bool IsVerified(
            KokoWearableBridgeService.WearableBridgeConnectionSnapshot connection,
            KokoWearableBridgeService.WearableBridgeDiagnostics diagnostics,
            KokoWearableState wearable)
        {
            if (!connection.IsPaired || !connection.IsLinked || !connection.TelemetryFresh || !connection.SampleRecent)
                return false;

            var paired = (diagnostics.LastPairedDeviceId ?? "").Trim();
            var device = (wearable.DeviceId ?? "").Trim();
            if (!IsUsableDevice(paired) || !IsUsableDevice(device))
                return false;

            if (!device.Equals(paired, StringComparison.OrdinalIgnoreCase))
                return false;

            return !LooksDiagnostic(device) &&
                   !LooksDiagnostic(wearable.SampleSource) &&
                   !LooksDiagnostic(wearable.TrustReason) &&
                   !LooksDiagnostic(diagnostics.LastAcceptedSampleId);
        }

        public static string BlockReason(
            KokoWearableBridgeService.WearableBridgeConnectionSnapshot connection,
            KokoWearableBridgeService.WearableBridgeDiagnostics diagnostics,
            KokoWearableState wearable)
        {
            if (!connection.IsPaired) return "no paired Galaxy Watch";
            if (!connection.IsLinked) return "watch not linked";
            if (!connection.TelemetryFresh || !connection.SampleRecent) return "waiting for fresh sensor sample";
            if (LooksDiagnostic(wearable.DeviceId) ||
                LooksDiagnostic(wearable.SampleSource) ||
                LooksDiagnostic(wearable.TrustReason) ||
                LooksDiagnostic(diagnostics.LastAcceptedSampleId))
                return "diagnostic sample hidden";
            if (!string.Equals((wearable.DeviceId ?? "").Trim(), (diagnostics.LastPairedDeviceId ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                return "sample device does not match pair";
            return "telemetry not trusted";
        }

        public static bool LooksDiagnostic(string? value)
        {
            var text = (value ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(text)) return false;
            return text.Contains("smoke") ||
                   text.Contains("mock") ||
                   text.Contains("dummy") ||
                   text.Contains("emulated") ||
                   text.Contains("simulator") ||
                   text.Contains("test-watch");
        }

        private static bool IsUsableDevice(string value)
            => !string.IsNullOrWhiteSpace(value) &&
               !value.Equals("unknown", StringComparison.OrdinalIgnoreCase) &&
               !value.Equals("--", StringComparison.OrdinalIgnoreCase);
    }
}
