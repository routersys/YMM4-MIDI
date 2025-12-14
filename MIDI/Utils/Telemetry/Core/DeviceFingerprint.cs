using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MIDI.Utils.Telemetry.Interfaces;

namespace MIDI.Utils.Telemetry.Core
{
    public class DeviceFingerprint : IDeviceIdentifier
    {
        private string? _cachedClientId;
        private readonly string _appVersion;

        public DeviceFingerprint()
        {
            _appVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
        }

        public string GetClientId()
        {
            if (_cachedClientId != null) return _cachedClientId;

            var rawId = string.Join("-",
                Environment.MachineName,
                Environment.UserName,
                Environment.ProcessorCount,
                Environment.OSVersion.Platform
            );

            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawId));
            var builder = new StringBuilder();
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }

            _cachedClientId = builder.ToString();
            return _cachedClientId;
        }

        public string GetOsVersion()
        {
            return Environment.OSVersion.ToString();
        }

        public string GetAppVersion()
        {
            return _appVersion;
        }
    }
}