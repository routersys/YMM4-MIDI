using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MIDI.Utils.Telemetry.Interfaces
{
    public interface ITelemetryClient
    {
        Task SendAsync(string category, JsonNode payload, double? sessionDuration = null, string? logs = null);
    }
}