namespace RegisterSummary.Core.Models;

public enum ArticleCategory
{
    Cybersecurity,
    AI,
    Cloud,
    Other
}

public enum RiskSeverity
{
    Critical,
    High,
    Medium,
    Low,
    Information
}

public enum ArticleProcessingStatus
{
    Pending,
    Fetching,
    Summarizing,
    Completed,
    Duplicate,
    Failed
}

public enum JobStatus
{
    Pending,
    Processing,
    AwaitingDecision,
    Completed,
    Failed
}
