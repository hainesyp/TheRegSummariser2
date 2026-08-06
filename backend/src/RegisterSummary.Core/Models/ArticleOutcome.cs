namespace RegisterSummary.Core.Models;

public sealed class ArticleOutcome
{
    public required string Url { get; init; }

    public ArticleProcessingStatus Status { get; set; } = ArticleProcessingStatus.Pending;

    public string? ResolvedUrl { get; set; }

    public string? ResolvedTitle { get; set; }

    public AiSummaryResult? Result { get; set; }

    public string? Error { get; set; }
}
