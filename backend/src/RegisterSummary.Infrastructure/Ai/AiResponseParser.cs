using System.Text.Json;
using System.Text.Json.Serialization;
using RegisterSummary.Core.Models;

namespace RegisterSummary.Infrastructure.Ai;

internal static class AiResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Parses the raw text returned by an AI provider into an AiSummaryResult. Tolerates markdown
    /// code fences and leading/trailing prose around the JSON object, since not every provider
    /// reliably honours a "JSON only" instruction.
    /// </summary>
    public static AiSummaryResult Parse(string rawContent)
    {
        var json = ExtractJsonObject(rawContent);

        var dto = JsonSerializer.Deserialize<AiResponseDto>(json, JsonOptions)
            ?? throw new AiResponseParseException("AI response deserialized to null.");

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            throw new AiResponseParseException("AI response is missing a 'title'.");
        }

        if (!Enum.TryParse<ArticleCategory>(dto.Category, ignoreCase: true, out var category))
        {
            throw new AiResponseParseException($"AI response has an unrecognised category: '{dto.Category}'.");
        }

        RiskSeverity? risk = null;
        if (!string.IsNullOrWhiteSpace(dto.Risk))
        {
            if (!Enum.TryParse<RiskSeverity>(dto.Risk, ignoreCase: true, out var parsedRisk))
            {
                throw new AiResponseParseException($"AI response has an unrecognised risk severity: '{dto.Risk}'.");
            }

            risk = parsedRisk;
        }

        var summary = dto.Summary ?? [];
        if (summary.Count == 0)
        {
            throw new AiResponseParseException("AI response has no summary bullets.");
        }

        return new AiSummaryResult
        {
            Title = dto.Title.Trim(),
            Category = category,
            Risk = risk,
            SummaryBullets = summary,
            Recommendations = dto.Recommendations is { Count: > 0 } ? dto.Recommendations : null
        };
    }

    private static string ExtractJsonObject(string rawContent)
    {
        var text = rawContent.Trim();

        var fenceStart = text.IndexOf("```", StringComparison.Ordinal);
        if (fenceStart >= 0)
        {
            var afterFence = text[(fenceStart + 3)..];
            afterFence = afterFence.TrimStart('j', 's', 'o', 'n', '\r', '\n', ' ');
            var fenceEnd = afterFence.IndexOf("```", StringComparison.Ordinal);
            if (fenceEnd >= 0)
            {
                text = afterFence[..fenceEnd];
            }
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end < 0 || end < start)
        {
            throw new AiResponseParseException("AI response did not contain a JSON object.");
        }

        return text[start..(end + 1)];
    }

    private sealed class AiResponseDto
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("risk")]
        public string? Risk { get; set; }

        [JsonPropertyName("summary")]
        public List<string>? Summary { get; set; }

        [JsonPropertyName("recommendations")]
        public List<string>? Recommendations { get; set; }
    }
}

public sealed class AiResponseParseException(string message) : Exception(message);
