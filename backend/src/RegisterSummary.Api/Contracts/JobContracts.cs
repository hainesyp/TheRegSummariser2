namespace RegisterSummary.Api.Contracts;

public sealed record CreateJobResponse(Guid JobId, int ArticleCount);

public sealed record ArticleStatusDto(string Url, string Status, string? Title, string? Category, string? Error);

public sealed record JobStatusResponse(
    Guid JobId,
    string Status,
    int TotalCount,
    int CompletedCount,
    int FailedCount,
    string? ErrorMessage,
    IReadOnlyList<ArticleStatusDto> Articles);

public sealed record ErrorResponse(string Message);
