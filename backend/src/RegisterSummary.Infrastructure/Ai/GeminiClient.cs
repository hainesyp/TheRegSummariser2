using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using RegisterSummary.Core.Interfaces;
using RegisterSummary.Core.Models;

namespace RegisterSummary.Infrastructure.Ai;

public sealed class GeminiClient(HttpClient httpClient, IOptions<AiOptions> options) : IAiClient
{
    public string ProviderName => "Gemini";

    public async Task<AiSummaryResult> SummarizeAsync(string articleUrl, string articleText, string promptTemplate, CancellationToken cancellationToken)
    {
        var opts = options.Value.Gemini;
        var prompt = PromptBuilder.Build(promptTemplate, articleUrl, articleText);

        var requestBody = new JsonObject
        {
            ["contents"] = new JsonArray
            {
                new JsonObject
                {
                    ["parts"] = new JsonArray { new JsonObject { ["text"] = prompt } }
                }
            },
            ["generationConfig"] = new JsonObject { ["responseMimeType"] = "application/json" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"models/{opts.Model}:generateContent")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("x-goog-api-key", opts.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new AiResponseParseException($"Gemini API returned {(int)response.StatusCode}: {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var content = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString()
            ?? throw new AiResponseParseException("Gemini response had no text content.");

        return AiResponseParser.Parse(content);
    }
}
