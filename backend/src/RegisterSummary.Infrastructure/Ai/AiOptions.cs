namespace RegisterSummary.Infrastructure.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public required string Provider { get; set; }

    public required string PromptTemplate { get; set; }

    public AiProviderOptions OpenAI { get; set; } = new();

    public AiProviderOptions Gemini { get; set; } = new();

    public AiProviderOptions Claude { get; set; } = new();
}

public sealed class AiProviderOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;
}
