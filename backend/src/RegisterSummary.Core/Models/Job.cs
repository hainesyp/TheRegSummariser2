namespace RegisterSummary.Core.Models;

public sealed class Job
{
    public required Guid Id { get; init; }

    public JobStatus Status { get; set; } = JobStatus.Pending;

    public required List<ArticleOutcome> Articles { get; init; }

    public string? ReportHtml { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
