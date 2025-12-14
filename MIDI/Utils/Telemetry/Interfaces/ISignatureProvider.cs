using MIDI.Utils.Telemetry.Models;

namespace MIDI.Utils.Telemetry.Interfaces
{
    public interface ISignatureProvider
    {
        void Sign(TelemetryRequest request);
    }
}