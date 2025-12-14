using System;

namespace MIDI.Utils.Telemetry.Attributes
{
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class TelemetryConfigurationAttribute : Attribute
    {
        public string SecretKey { get; }
        public string PrimaryEndpoint { get; }
        public string FallbackEndpoint { get; }

        public TelemetryConfigurationAttribute(string secretKey, string primaryEndpoint, string fallbackEndpoint)
        {
            SecretKey = secretKey;
            PrimaryEndpoint = primaryEndpoint;
            FallbackEndpoint = fallbackEndpoint;
        }
    }
}