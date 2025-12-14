using System;
using System.Security.Cryptography;
using System.Text;
using MIDI.Utils.Telemetry.Interfaces;
using MIDI.Utils.Telemetry.Models;

namespace MIDI.Utils.Telemetry.Core
{
    public class HmacSignatureService : ISignatureProvider
    {
        private readonly byte[] _secretKey;

        public HmacSignatureService(string secretKey)
        {
            _secretKey = Encoding.UTF8.GetBytes(secretKey ?? throw new ArgumentNullException(nameof(secretKey)));
        }

        public void Sign(TelemetryRequest request)
        {
            request.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            request.Nonce = Guid.NewGuid().ToString("N");

            var payloadString = request.Payload?.ToJsonString() ?? "{}";
            var dataToSign = $"{request.Category}|{request.Timestamp}|{request.Nonce}|{payloadString}";

            using var hmac = new HMACSHA256(_secretKey);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
            request.Signature = Convert.ToBase64String(hash);
        }
    }
}