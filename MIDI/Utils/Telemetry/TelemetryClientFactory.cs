using System;
using System.Net.Http;
using MIDI.Utils.Telemetry.Core;
using MIDI.Utils.Telemetry.Interfaces;

namespace MIDI.Utils.Telemetry
{
    public static class TelemetryClientFactory
    {
        private const string PrimaryEndpoint = "https://telemetry.routersys.com/api/telemetry";
        private const string FallbackEndpoint = "https://telemetry.f5.si/api/telemetry";

        public static ITelemetryClient Create()
        {
            var secretKey = NativeKeyProvider.GetSecretKey();
            if (string.IsNullOrEmpty(secretKey))
            {
                throw new InvalidOperationException("Telemetry Secret Key could not be loaded from native library.");
            }

            var httpClient = new HttpClient();
            var deviceIdentifier = new DeviceFingerprintProvider();
            var signatureProvider = new HmacSignatureService(secretKey);
            var transmitter = new HttpDataTransmitter(
                httpClient,
                PrimaryEndpoint,
                FallbackEndpoint);

            return new TelemetryClient(deviceIdentifier, signatureProvider, transmitter);
        }
    }
}