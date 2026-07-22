namespace RegisterSummary.Infrastructure.Ai;

internal static class PromptBuilder
{
    /// <summary>
    /// The response-format contract is fixed (not part of the configurable prompt template) so that
    /// AiResponseParser can reliably parse every provider's reply regardless of how an operator has
    /// customised the analytical instructions in AiOptions.PromptTemplate.
    /// </summary>
    private const string JsonFormatInstructions = """

        Respond with ONLY a single minified JSON object and nothing else - no markdown code fences, no commentary before or after it.
        The JSON object must have exactly these fields:
        {
          "title": string - the article's headline,
          "category": one of "Cybersecurity", "AI", "Cloud", "Other",
          "risk": one of "Critical", "High", "Medium", "Low", "Information" if category is "Cybersecurity", otherwise null,
          "summary": array of exactly 5 short bullet-point strings summarising the article,
          "recommendations": array of short, actionable remediation/mitigation strings - REQUIRED (at least 1) when category is "Cybersecurity", otherwise an empty array
        }
        """;

    public static string Build(string promptTemplate, string articleUrl, string articleText)
    {
        var rendered = promptTemplate
            .Replace("{ArticleUrl}", articleUrl)
            .Replace("{ArticleText}", articleText);

        return rendered + JsonFormatInstructions;
    }
}
