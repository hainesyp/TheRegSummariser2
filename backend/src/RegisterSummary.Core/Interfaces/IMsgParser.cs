namespace RegisterSummary.Core.Interfaces;

public interface IMsgParser
{
    /// <summary>
    /// Extracts qualifying Register article URLs (utm_content=top-article|article) from a single .msg file stream.
    /// </summary>
    IReadOnlyList<string> ExtractArticleUrls(Stream msgFileStream);
}
