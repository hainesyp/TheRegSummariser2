using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using RegisterSummary.Core.Interfaces;
using RegisterSummary.Core.Models;

namespace RegisterSummary.Infrastructure.Ai;

public sealed class ClaudeClient(HttpClient httpClient, IOptions<AiOptions> options) : IAiClient
{
    public string ProviderName => "Claude";

    public async Task<AiSummaryResult> SummarizeAsync(string articleUrl, string articleText, string promptTemplate, CancellationToken cancellationToken)
    {
        var opts = options.Value.Claude;
        var prompt = PromptBuilder.Build(promptTemplate, articleUrl, articleText);

        var requestBody = new JsonObject
        {
            ["model"] = opts.Model,
            ["max_tokens"] = 2048,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = prompt
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("x-api-key", opts.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new AiResponseParseException($"Claude API returned {(int)response.StatusCode}: {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var content = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()
            ?? throw new AiResponseParseException("Claude response had no text content.");

        return AiResponseParser.Parse(content);
    }
}
