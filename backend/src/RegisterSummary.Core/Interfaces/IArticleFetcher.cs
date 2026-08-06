using RegisterSummary.Core.Models;

namespace RegisterSummary.Core.Interfaces;

public interface IArticleFetcher
{
    /// <summary>
    /// Follows the given URL (redirectors included) and returns the resolved article URL, page
    /// title, and readable text content of the resulting page.
    /// </summary>
    Task<ArticleContent> FetchArticleContentAsync(string url, CancellationToken cancellationToken);
}
