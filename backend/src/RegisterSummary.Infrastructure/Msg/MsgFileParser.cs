using System.Text;
using System.Text.RegularExpressions;
using MsgReader.Outlook;
using RegisterSummary.Core.Interfaces;

namespace RegisterSummary.Infrastructure.Msg;

/// <summary>
/// Extracts Register newsletter article links from an Outlook .msg file. Only links whose
/// utm_content query parameter is "top-article" or "article" are considered real articles
/// (the newsletter also links to ads, section headers, unsubscribe links, etc).
/// </summary>
public sealed partial class MsgFileParser : IMsgParser
{
    static MsgFileParser()
    {
        // MSGReader's RTF parser (used when a .msg's body is only available as RTF, which is
        // common for newsletters composed in Outlook) looks up legacy Windows code pages like
        // "Windows-1252" by name via Encoding.GetEncoding(string). .NET Core doesn't register
        // those by default, which otherwise throws a TypeInitializationException for
        // MsgReader.Rtf.Font's static constructor the first time such a .msg is parsed.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public IReadOnlyList<string> ExtractArticleUrls(Stream msgFileStream)
    {
        using var message = new Storage.Message(msgFileStream, FileAccess.Read);

        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(message.BodyHtml))
        {
            candidates.AddRange(ExtractHrefs(message.BodyHtml));
        }

        if (!string.IsNullOrWhiteSpace(message.BodyText))
        {
            candidates.AddRange(ExtractPlainUrls(message.BodyText));
        }

        return FilterAndDeduplicate(candidates);
    }

    /// <summary>
    /// Keeps only qualifying article URLs (utm_content=top-article|article on go.reg.cx) and
    /// removes duplicates, preserving first-seen order. Split out from ExtractArticleUrls so the
    /// filtering/dedup rules can be unit tested independently of MSGReader/.msg binary parsing.
    /// </summary>
    internal static IReadOnlyList<string> FilterAndDeduplicate(IEnumerable<string> candidateUrls)
    {
        var unique = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var url in candidateUrls)
        {
            if (IsQualifyingArticleUrl(url) && seen.Add(url))
            {
                unique.Add(url);
            }
        }

        return unique;
    }

    private static IEnumerable<string> ExtractHrefs(string html)
    {
        foreach (Match match in HrefRegex().Matches(html))
        {
            yield return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
        }
    }

    private static IEnumerable<string> ExtractPlainUrls(string text)
    {
        foreach (Match match in PlainUrlRegex().Matches(text))
        {
            yield return match.Value;
        }
    }

    internal static bool IsQualifyingArticleUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Host.Equals("go.reg.cx", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var utmContent = ParseQueryParam(uri.Query, "utm_content");

        return utmContent is "top-article" or "article";
    }

    private static string? ParseQueryParam(string query, string name)
    {
        if (string.IsNullOrEmpty(query))
        {
            return null;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && Uri.UnescapeDataString(parts[0]) == name)
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return null;
    }

    [GeneratedRegex("""href\s*=\s*["']([^"']+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex HrefRegex();

    [GeneratedRegex("""https?://go\.reg\.cx/[^\s"'<>)]+""", RegexOptions.IgnoreCase)]
    private static partial Regex PlainUrlRegex();
}
