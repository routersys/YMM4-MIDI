using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using MIDI.Utils.Telemetry.Models;

namespace MIDI.Utils.Telemetry.Core
{
    public class HttpDataTransmitter
    {
        private readonly HttpClient _httpClient;
        private readonly string _primaryEndpoint;
        private readonly string _fallbackEndpoint;

        public HttpDataTransmitter(HttpClient httpClient, string primaryEndpoint, string fallbackEndpoint)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _primaryEndpoint = primaryEndpoint ?? throw new ArgumentNullException(nameof(primaryEndpoint));
            _fallbackEndpoint = fallbackEndpoint ?? throw new ArgumentNullException(nameof(fallbackEndpoint));
        }

        public async Task TransmitAsync(TelemetryRequest request)
        {
            try
            {
                request.IsFallback = false;
                var response = await _httpClient.PostAsJsonAsync(_primaryEndpoint, request);
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                request.IsFallback = true;
                var response = await _httpClient.PostAsJsonAsync(_fallbackEndpoint, request);
                response.EnsureSuccessStatusCode();
            }
        }
    }
}