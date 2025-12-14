using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using MIDI.Utils.Telemetry.Attributes;
using MIDI.Utils.Telemetry.Core;
using MIDI.Utils.Telemetry.Interfaces;

namespace MIDI.Utils.Telemetry
{
    public static class TelemetryClientFactory
    {
        public static ITelemetryClient Create()
        {
            var config = GetConfiguration() ?? throw new InvalidOperationException("TelemetryConfigurationAttribute not found on assembly or entry class.");

            var httpClient = new HttpClient();
            var deviceIdentifier = new DeviceFingerprintProvider();
            var signatureProvider = new HmacSignatureService(config.SecretKey);
            var transmitter = new HttpDataTransmitter(
                httpClient,
                config.PrimaryEndpoint,
                config.FallbackEndpoint);

            return new TelemetryClient(deviceIdentifier, signatureProvider, transmitter);
        }

        private static TelemetryConfigurationAttribute? GetConfiguration()
        {
            var assemblyConfig = Assembly.GetEntryAssembly()?
                .GetCustomAttributes(typeof(TelemetryConfigurationAttribute), false)
                .Cast<TelemetryConfigurationAttribute>()
                .FirstOrDefault();

            if (assemblyConfig != null)
            {
                return assemblyConfig;
            }

            return Assembly.GetEntryAssembly()?
                .EntryPoint?.DeclaringType?
                .GetCustomAttributes(typeof(TelemetryConfigurationAttribute), false)
                .Cast<TelemetryConfigurationAttribute>()
                .FirstOrDefault();
        }
    }
}