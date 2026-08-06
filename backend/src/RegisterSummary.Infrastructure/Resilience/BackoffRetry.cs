using Microsoft.Extensions.Logging;

namespace RegisterSummary.Infrastructure.Resilience;

/// <summary>
/// Retries a transient operation (article fetch or AI call) with exponential backoff. Used so a
/// single flaky response (e.g. a 503 from the AI provider) doesn't fail an article outright.
/// </summary>
public static class BackoffRetry
{
    public static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts,
        TimeSpan initialDelay,
        ILogger logger,
        string operationDescription,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < maxAttempts && ex is not OperationCanceledException)
            {
                var delay = TimeSpan.FromMilliseconds(initialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                logger.LogWarning(
                    ex,
                    "{Operation} failed on attempt {Attempt}/{MaxAttempts}; retrying in {Delay}.",
                    operationDescription, attempt, maxAttempts, delay);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
