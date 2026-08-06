using RegisterSummary.Api.Contracts;
using RegisterSummary.Core.Interfaces;
using RegisterSummary.Core.Models;

namespace RegisterSummary.Api.Endpoints;

public static class JobEndpoints
{
    private const long MaxFileSizeBytes = 25 * 1024 * 1024;

    public static void MapJobEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/jobs");

        group.MapPost("/", CreateJobAsync)
            .DisableAntiforgery()
            .WithName("CreateJob");

        group.MapGet("/{id:guid}", GetJobStatus)
            .WithName("GetJobStatus");

        group.MapGet("/{id:guid}/download", DownloadJobResult)
            .WithName("DownloadJobResult");

        group.MapPost("/{id:guid}/retry", RetryFailedArticles)
            .WithName("RetryFailedArticles");

        group.MapPost("/{id:guid}/finish", FinishJob)
            .WithName("FinishJob");
    }

    private static async Task<IResult> CreateJobAsync(
        IFormFileCollection files,
        IMsgParser msgParser,
        IJobStore jobStore,
        IJobQueue jobQueue,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return Results.BadRequest(new ErrorResponse("No files were uploaded."));
        }

        var nonMsgFile = files.FirstOrDefault(f => !f.FileName.EndsWith(".msg", StringComparison.OrdinalIgnoreCase));
        if (nonMsgFile is not null)
        {
            return Results.BadRequest(new ErrorResponse($"'{nonMsgFile.FileName}' is not a .msg file."));
        }

        var oversizedFile = files.FirstOrDefault(f => f.Length > MaxFileSizeBytes);
        if (oversizedFile is not null)
        {
            return Results.BadRequest(new ErrorResponse($"'{oversizedFile.FileName}' exceeds the {MaxFileSizeBytes / (1024 * 1024)}MB size limit."));
        }

        var uniqueUrls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            await using var stream = file.OpenReadStream();

            IReadOnlyList<string> urls;
            try
            {
                urls = msgParser.ExtractArticleUrls(stream);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new ErrorResponse($"Failed to parse '{file.FileName}': {ex.Message}"));
            }

            foreach (var url in urls)
            {
                if (seen.Add(url))
                {
                    uniqueUrls.Add(url);
                }
            }
        }

        if (uniqueUrls.Count == 0)
        {
            return Results.BadRequest(new ErrorResponse("No qualifying article links (utm_content=top-article or article) were found in the uploaded file(s)."));
        }

        var job = jobStore.CreateJob(uniqueUrls);
        await jobQueue.EnqueueAsync(job.Id, cancellationToken);

        return Results.Accepted($"/api/jobs/{job.Id}", new CreateJobResponse(job.Id, uniqueUrls.Count));
    }

    private static IResult GetJobStatus(Guid id, IJobStore jobStore)
    {
        var job = jobStore.GetJob(id);
        if (job is null)
        {
            return Results.NotFound(new ErrorResponse($"Job '{id}' was not found."));
        }

        var articles = job.Articles.Select(a => new ArticleStatusDto(
            a.Url,
            a.Status.ToString(),
            a.Result?.Title,
            a.Result?.Category.ToString(),
            a.Error)).ToList();

        var response = new JobStatusResponse(
            job.Id,
            job.Status.ToString(),
            job.Articles.Count,
            job.Articles.Count(a => a.Status == ArticleProcessingStatus.Completed),
            job.Articles.Count(a => a.Status == ArticleProcessingStatus.Duplicate),
            job.Articles.Count(a => a.Status == ArticleProcessingStatus.Failed),
            job.ErrorMessage,
            articles);

        return Results.Ok(response);
    }

    private static async Task<IResult> RetryFailedArticles(
        Guid id, IJobStore jobStore, IJobQueue jobQueue, CancellationToken cancellationToken)
    {
        var job = jobStore.GetJob(id);
        if (job is null)
        {
            return Results.NotFound(new ErrorResponse($"Job '{id}' was not found."));
        }

        if (job.Status != JobStatus.AwaitingDecision)
        {
            return Results.Conflict(new ErrorResponse($"Job '{id}' is not awaiting a retry decision (status: {job.Status})."));
        }

        foreach (var article in job.Articles.Where(a => a.Status == ArticleProcessingStatus.Failed))
        {
            article.Status = ArticleProcessingStatus.Pending;
            article.Error = null;
        }

        job.Status = JobStatus.Processing;
        job.ErrorMessage = null;
        jobStore.UpdateJob(job);

        await jobQueue.EnqueueAsync(job.Id, cancellationToken);

        return Results.Accepted($"/api/jobs/{job.Id}");
    }

    private static IResult FinishJob(Guid id, IJobStore jobStore, IHtmlReportBuilder htmlReportBuilder)
    {
        var job = jobStore.GetJob(id);
        if (job is null)
        {
            return Results.NotFound(new ErrorResponse($"Job '{id}' was not found."));
        }

        if (job.Status != JobStatus.AwaitingDecision)
        {
            return Results.Conflict(new ErrorResponse($"Job '{id}' is not awaiting a retry decision (status: {job.Status})."));
        }

        var succeeded = job.Articles.Where(a => a.Status == ArticleProcessingStatus.Completed).ToList();
        if (succeeded.Count == 0)
        {
            return Results.BadRequest(new ErrorResponse("No articles have been summarised yet; nothing to generate a report from."));
        }

        var failedCount = job.Articles.Count(a => a.Status == ArticleProcessingStatus.Failed);

        job.ReportHtml = htmlReportBuilder.Build(succeeded);
        job.Status = JobStatus.Completed;
        job.ErrorMessage = failedCount > 0
            ? $"Report generated with {succeeded.Count} of {job.Articles.Count} articles; {failedCount} failed and were excluded."
            : null;
        jobStore.UpdateJob(job);

        return Results.Ok();
    }

    private static IResult DownloadJobResult(Guid id, IJobStore jobStore)
    {
        var job = jobStore.GetJob(id);
        if (job is null)
        {
            return Results.NotFound(new ErrorResponse($"Job '{id}' was not found."));
        }

        if (job.Status != JobStatus.Completed || job.ReportHtml is null)
        {
            return Results.Conflict(new ErrorResponse($"Job '{id}' is not completed yet (status: {job.Status})."));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(job.ReportHtml);
        var fileName = $"register-summary-{DateTimeOffset.UtcNow:yyyy-MM-dd}.html";

        return Results.File(bytes, "text/html", fileName);
    }
}
