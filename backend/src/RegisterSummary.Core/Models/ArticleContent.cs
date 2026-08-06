namespace RegisterSummary.Core.Models;

public sealed class ArticleContent
{
    public required string ResolvedUrl { get; init; }

    public string? Title { get; init; }

    public required string Text { get; init; }
}
