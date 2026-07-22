namespace RegisterSummary.Core.Models;

public sealed class AiSummaryResult
{
    public required string Title { get; init; }

    public required ArticleCategory Category { get; init; }

    public RiskSeverity? Risk { get; init; }

    public required IReadOnlyList<string> SummaryBullets { get; init; }

    public IReadOnlyList<string>? Recommendations { get; init; }
}
