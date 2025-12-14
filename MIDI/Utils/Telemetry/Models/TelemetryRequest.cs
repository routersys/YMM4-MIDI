using System.Text.Json.Nodes;

namespace MIDI.Utils.Telemetry.Models
{
    public class TelemetryRequest
    {
        public string Category { get; set; } = string.Empty;
        public string? ClientId { get; set; }
        public string? AppVersion { get; set; }
        public string? OsVersion { get; set; }
        public double? SessionDuration { get; set; }
        public string? RecentLogs { get; set; }
        public JsonNode? Payload { get; set; }
        public long Timestamp { get; set; }
        public string Nonce { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public bool IsFallback { get; set; }
    }
}