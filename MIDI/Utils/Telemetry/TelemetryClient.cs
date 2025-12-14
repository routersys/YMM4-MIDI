using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using MIDI.Utils.Telemetry.Core;
using MIDI.Utils.Telemetry.Interfaces;
using MIDI.Utils.Telemetry.Models;

namespace MIDI.Utils.Telemetry
{
    public class TelemetryClient : ITelemetryClient
    {
        private readonly IDeviceIdentifier _deviceIdentifier;
        private readonly ISignatureProvider _signatureProvider;
        private readonly HttpDataTransmitter _transmitter;

        public TelemetryClient(
            IDeviceIdentifier deviceIdentifier,
            ISignatureProvider signatureProvider,
            HttpDataTransmitter transmitter)
        {
            _deviceIdentifier = deviceIdentifier ?? throw new ArgumentNullException(nameof(deviceIdentifier));
            _signatureProvider = signatureProvider ?? throw new ArgumentNullException(nameof(signatureProvider));
            _transmitter = transmitter ?? throw new ArgumentNullException(nameof(transmitter));
        }

        public async Task SendAsync(string category, JsonNode payload, double? sessionDuration = null, string? logs = null)
        {
            var request = new TelemetryRequest
            {
                Category = category,
                ClientId = _deviceIdentifier.GetClientId(),
                AppVersion = _deviceIdentifier.GetAppVersion(),
                OsVersion = _deviceIdentifier.GetOsVersion(),
                SessionDuration = sessionDuration,
                RecentLogs = logs,
                Payload = payload
            };

            _signatureProvider.Sign(request);

            await _transmitter.TransmitAsync(request);
        }
    }
}