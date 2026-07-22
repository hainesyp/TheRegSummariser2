using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using RegisterSummary.Core.Interfaces;
using RegisterSummary.Core.Models;

namespace RegisterSummary.Infrastructure.Ai;

public sealed class OpenAiClient(HttpClient httpClient, IOptions<AiOptions> options) : IAiClient
{
    public string ProviderName => "OpenAI";

    public async Task<AiSummaryResult> SummarizeAsync(string articleUrl, string articleText, string promptTemplate, CancellationToken cancellationToken)
    {
        var opts = options.Value.OpenAI;
        var prompt = PromptBuilder.Build(promptTemplate, articleUrl, articleText);

        var requestBody = new JsonObject
        {
            ["model"] = opts.Model,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = prompt
                }
            },
            ["response_format"] = new JsonObject { ["type"] = "json_object" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new AiResponseParseException($"OpenAI API returned {(int)response.StatusCode}: {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
            ?? throw new AiResponseParseException("OpenAI response had no message content.");

        return AiResponseParser.Parse(content);
    }
}
