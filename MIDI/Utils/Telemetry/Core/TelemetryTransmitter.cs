using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using MIDI.Utils.Telemetry.Models;

namespace MIDI.Utils.Telemetry.Core
{
    public class TelemetryTransmitter
    {
        private readonly HttpClient _httpClient;
        private const string PrimaryEndpoint = "https://telemetry.routersys.com/api/telemetry";
        private const string FallbackEndpoint = "https://telemetry.f5.si/api/telemetry";

        public TelemetryTransmitter(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task TransmitAsync(TelemetryRequest request)
        {
            try
            {
                request.IsFallback = false;
                var response = await _httpClient.PostAsJsonAsync(PrimaryEndpoint, request);
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                request.IsFallback = true;
                var response = await _httpClient.PostAsJsonAsync(FallbackEndpoint, request);
                response.EnsureSuccessStatusCode();
            }
        }
    }
}