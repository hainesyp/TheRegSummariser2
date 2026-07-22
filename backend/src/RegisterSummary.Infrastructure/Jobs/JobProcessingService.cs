using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RegisterSummary.Core.Interfaces;
using RegisterSummary.Core.Models;
using RegisterSummary.Infrastructure.Ai;

namespace RegisterSummary.Infrastructure.Jobs;

/// <summary>
/// Consumes queued job ids and processes each job's articles with bounded concurrency. Runs as a
/// single instance (see AiOptions/README notes on the in-memory job store limiting the app to
/// replicas: 1), so no distributed coordination is needed here.
/// </summary>
public sealed class JobProcessingService(
    IJobQueue jobQueue,
    IServiceScopeFactory scopeFactory,
    ILogger<JobProcessingService> logger) : BackgroundService
{
    private const int MaxConcurrentArticles = 3;

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

        using var semaphore = new SemaphoreSlim(MaxConcurrentArticles);

        var tasks = job.Articles.Select(async outcome =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await ProcessArticleAsync(outcome, articleFetcher, aiClient, promptTemplate, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        var succeeded = job.Articles.Where(a => a.Status == ArticleProcessingStatus.Completed).ToList();

        if (succeeded.Count == 0)
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = "All articles failed to process. See per-article errors for details.";
        }
        else
        {
            job.ReportHtml = htmlReportBuilder.Build(succeeded);
            job.Status = JobStatus.Completed;
        }

        jobStore.UpdateJob(job);
    }

    private async Task ProcessArticleAsync(
        ArticleOutcome outcome,
        IArticleFetcher articleFetcher,
        IAiClient aiClient,
        string promptTemplate,
        CancellationToken cancellationToken)
    {
        try
        {
            outcome.Status = ArticleProcessingStatus.Fetching;
            var articleText = await articleFetcher.FetchArticleTextAsync(outcome.Url, cancellationToken);

            outcome.Status = ArticleProcessingStatus.Summarizing;
            outcome.Result = await aiClient.SummarizeAsync(outcome.Url, articleText, promptTemplate, cancellationToken);

            outcome.Status = ArticleProcessingStatus.Completed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to process article {Url}", outcome.Url);
            outcome.Status = ArticleProcessingStatus.Failed;
            outcome.Error = ex.Message;
        }
    }
}
