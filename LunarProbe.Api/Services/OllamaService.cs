
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace LunarProbe.Api.Services;

public sealed class OllamaService(
    HttpClient httpClient,
    IConfiguration configuration)
{
    private readonly string _model =
        configuration["Ollama:Model"] ?? "qwen3:4b-instruct";

    public async Task<string> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var request = new OllamaChatRequest
        {
            Model = _model,
            Messages =
            [
                new OllamaMessage
                {
                    Role = "user",
                    Content = prompt
                }
            ],
            Stream = false,
            Think = false,
            Options = new OllamaOptions
            {
                NumPredict = 1024,
                Temperature = 0
            }
        };

        using var response = await httpClient.PostAsJsonAsync(
            "/api/chat",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<OllamaChatResponse>(
                cancellationToken: cancellationToken);

        if (result is null ||
            string.IsNullOrWhiteSpace(result.Message?.Content))
        {
            throw new InvalidOperationException(
                "Ollama returned an empty response.");
        }

        return result.Message.Content.Trim();
    }

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OllamaMessage> Messages { get; init; } = [];

        [JsonPropertyName("stream")]
        public bool Stream { get; init; }

        [JsonPropertyName("think")]
        public bool Think { get; init; }

        [JsonPropertyName("options")]
        public OllamaOptions Options { get; init; } = new();
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; init; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; init; } = string.Empty;
    }

    private sealed class OllamaOptions
    {
        [JsonPropertyName("num_predict")]
        public int NumPredict { get; init; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; init; }
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaResponseMessage? Message { get; init; }
    }

    private sealed class OllamaResponseMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; init; } = string.Empty;
    }
}
