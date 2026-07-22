namespace RegisterSummary.Core.Models;

public sealed class ArticleOutcome
{
    public required string Url { get; init; }

    public ArticleProcessingStatus Status { get; set; } = ArticleProcessingStatus.Pending;

    public AiSummaryResult? Result { get; set; }

    public string? Error { get; set; }
}
