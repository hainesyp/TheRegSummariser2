namespace RegisterSummary.Core.Interfaces;

public interface IArticleFetcher
{
    /// <summary>
    /// Follows the given URL (redirectors included) and returns the readable text content of the resulting page.
    /// </summary>
    Task<string> FetchArticleTextAsync(string url, CancellationToken cancellationToken);
}
