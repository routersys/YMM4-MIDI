using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using MIDI.API.Context;
using MIDI.Configuration.Models;
using MIDI.Utils;

namespace MIDI.API
{
    public class NamedPipeApiHandler
    {
        private readonly ApiContext _apiContext;
        private readonly JsonSerializerOptions _jsonOptions;

        public NamedPipeApiHandler(MidiSettingsViewModel viewModel, MidiConfiguration configuration)
        {
            _apiContext = new ApiContext(viewModel, configuration, MidiEditorSettings.Default);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<string> HandleRequest(string jsonRequest)
        {
            string? command = null;
            try
            {
                var request = JsonNode.Parse(jsonRequest);
                if (request == null)
                {
                    return CreateErrorResponse("Empty request received.");
                }

                command = request["command"]?.GetValue<string>();
                var parameters = request["parameters"];

                if (string.IsNullOrEmpty(command))
                {
                    return CreateErrorResponse("Command not specified.");
                }

                object? result = await GeneratedApiDispatcher.DispatchAsync(command, parameters, _apiContext);

                return CreateSuccessResponse(result);
            }
            catch (JsonException jsonEx)
            {
                return CreateErrorResponse($"Invalid JSON request: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                try { Logger.Error(LogMessages.NamedPipeApiHandlerError, ex, command ?? "Unknown"); } catch { }
                return CreateErrorResponse($"Internal server error: {ex.Message}");
            }
        }

        private string CreateSuccessResponse(object? data)
        {
            try
            {
                var response = new JsonObject
                {
                    ["status"] = "success"
                };

                if (data != null)
                {
                    if (data is JsonNode node)
                    {
                        response["data"] = node;
                    }
                    else
                    {
                        response["data"] = JsonSerializer.SerializeToNode(data, _jsonOptions);
                    }
                }

                return response.ToJsonString(_jsonOptions);
            }
            catch (Exception ex)
            {
                return CreateErrorResponse($"Serialization error: {ex.Message}");
            }
        }

        private string CreateErrorResponse(string message)
        {
            var response = new JsonObject
            {
                ["status"] = "error",
                ["message"] = message
            };
            return response.ToJsonString(_jsonOptions);
        }
    }
}