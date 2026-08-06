using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RegisterSummary.Core.Interfaces;
using RegisterSummary.Core.Models;
using RegisterSummary.Infrastructure.Ai;
using RegisterSummary.Infrastructure.Resilience;

namespace RegisterSummary.Infrastructure.Jobs;

/// <summary>
/// Consumes queued job ids and processes each job's articles with bounded concurrency. Runs as a
/// single instance (see AiOptions/README notes on the in-memory job store limiting the app to
/// replicas: 1), so no distributed coordination is needed here.
///
/// A job id can be enqueued more than once (e.g. when the user retries failed articles): each pass
/// only touches articles still in <see cref="ArticleProcessingStatus.Pending"/>, leaving already
/// resolved (Completed/Duplicate) articles untouched.
/// </summary>
public sealed class JobProcessingService(
    IJobQueue jobQueue,
    IServiceScopeFactory scopeFactory,
    ILogger<JobProcessingService> logger) : BackgroundService
{
    private const int MaxConcurrentArticles = 3;
    private const int MaxAttempts = 4;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var jobId in jobQueue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(jobId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Unhandled error processing job {JobId}", jobId);
            }
        }
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var jobStore = scope.ServiceProvider.GetRequiredService<IJobStore>();
        var articleFetcher = scope.ServiceProvider.GetRequiredService<IArticleFetcher>();
        var aiClientFactory = scope.ServiceProvider.GetRequiredService<IAiClientFactory>();
        var htmlReportBuilder = scope.ServiceProvider.GetRequiredService<IHtmlReportBuilder>();
        var aiOptions = scope.ServiceProvider.GetRequiredService<IOptions<AiOptions>>();

        var job = jobStore.GetJob(jobId);
        if (job is null)
        {
            logger.LogWarning("Job {JobId} not found when dequeued", jobId);
            return;
        }

        job.Status = JobStatus.Processing;
        jobStore.UpdateJob(job);

        var aiClient = aiClientFactory.GetClient();
        var promptTemplate = aiOptions.Value.PromptTemplate;

        var seenArticleKeys = new ConcurrentDictionary<string, byte>();
        foreach (var resolved in job.Articles.Where(a =>
            a.Status is ArticleProcessingStatus.Completed or ArticleProcessingStatus.Duplicate && a.ResolvedUrl is not null))
        {
            seenArticleKeys.TryAdd(BuildDedupKey(resolved.ResolvedTitle, resolved.ResolvedUrl!), 0);
        }

        var pending = job.Articles.Where(a => a.Status == ArticleProcessingStatus.Pending).ToList();

        using var semaphore = new SemaphoreSlim(MaxConcurrentArticles);

        var tasks = pending.Select(async outcome =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await ProcessArticleAsync(outcome, articleFetcher, aiClient, promptTemplate, seenArticleKeys, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        FinalizeJobStatus(job, htmlReportBuilder);

        jobStore.UpdateJob(job);
    }

    private static void FinalizeJobStatus(Job job, IHtmlReportBuilder htmlReportBuilder)
    {
        var failedCount = job.Articles.Count(a => a.Status == ArticleProcessingStatus.Failed);
        var succeeded = job.Articles.Where(a => a.Status == ArticleProcessingStatus.Completed).ToList();

        if (failedCount > 0)
        {
            // Some articles couldn't be summarised even after automatic retries - let the user
            // choose to retry just those, or finish the report with what succeeded so far.
            job.Status = JobStatus.AwaitingDecision;
            job.ErrorMessage = null;
            return;
        }

        if (succeeded.Count == 0)
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = "No articles could be summarised (all were duplicates or failed).";
            return;
        }

        job.ReportHtml = htmlReportBuilder.Build(succeeded);
        job.Status = JobStatus.Completed;
    }

    private async Task ProcessArticleAsync(
        ArticleOutcome outcome,
        IArticleFetcher articleFetcher,
        IAiClient aiClient,
        string promptTemplate,
        ConcurrentDictionary<string, byte> seenArticleKeys,
        CancellationToken cancellationToken)
    {
        try
        {
            outcome.Status = ArticleProcessingStatus.Fetching;
            var content = await BackoffRetry.ExecuteAsync(
                () => articleFetcher.FetchArticleContentAsync(outcome.Url, cancellationToken),
                MaxAttempts, InitialRetryDelay, logger, $"Fetching {outcome.Url}", cancellationToken);

            outcome.ResolvedUrl = content.ResolvedUrl;
            outcome.ResolvedTitle = content.Title;

            var dedupKey = BuildDedupKey(content.Title, content.ResolvedUrl);
            if (!seenArticleKeys.TryAdd(dedupKey, 0))
            {
                outcome.Status = ArticleProcessingStatus.Duplicate;
                outcome.Error = "Skipped: duplicate of another article already included in this report.";
                return;
            }

            outcome.Status = ArticleProcessingStatus.Summarizing;
            outcome.Result = await BackoffRetry.ExecuteAsync(
                () => aiClient.SummarizeAsync(outcome.Url, content.Text, promptTemplate, cancellationToken),
                MaxAttempts, InitialRetryDelay, logger, $"Summarising {outcome.Url}", cancellationToken);

            outcome.Status = ArticleProcessingStatus.Completed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to process article {Url} after {MaxAttempts} attempts", outcome.Url, MaxAttempts);
            outcome.Status = ArticleProcessingStatus.Failed;
            outcome.Error = ex.Message;
        }
    }

    private static string BuildDedupKey(string? title, string resolvedUrl)
    {
        var normalizedTitle = (title ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedUrl = NormalizeUrl(resolvedUrl);
        return $"{normalizedTitle}|{normalizedUrl}";
    }

    private static string NormalizeUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return $"{uri.Host}{uri.AbsolutePath}".TrimEnd('/').ToLowerInvariant();
        }

        return url.Trim().ToLowerInvariant();
    }
}
