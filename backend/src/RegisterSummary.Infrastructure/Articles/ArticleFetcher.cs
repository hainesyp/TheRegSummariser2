using System.Text.RegularExpressions;
using HtmlAgilityPack;
using RegisterSummary.Core.Interfaces;
using RegisterSummary.Core.Models;

namespace RegisterSummary.Infrastructure.Articles;

public sealed partial class ArticleFetcher(HttpClient httpClient) : IArticleFetcher
{
    private const int MaxArticleTextLength = 15_000;

    private static readonly string[] NoiseNodeSelectors =
    [
        "//script", "//style", "//nav", "//header", "//footer", "//aside", "//noscript", "//form", "//iframe"
    ];

    public async Task<ArticleContent> FetchArticleContentAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // The <title> tag identifies the actual article for dedup purposes (the newsletter's
        // go.reg.cx tracking link is unique per send, even when it points at the same article).
        var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText;
        title = string.IsNullOrWhiteSpace(title) ? null : System.Net.WebUtility.HtmlDecode(title).Trim();

        foreach (var selector in NoiseNodeSelectors)
        {
            var nodes = doc.DocumentNode.SelectNodes(selector);
            if (nodes is null)
            {
                continue;
            }

            foreach (var node in nodes)
            {
                node.Remove();
            }
        }

        var bodyNode = doc.DocumentNode.SelectSingleNode("//article") ?? doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
        var text = System.Net.WebUtility.HtmlDecode(bodyNode.InnerText);
        text = WhitespaceRegex().Replace(text, " ").Trim();

        if (text.Length > MaxArticleTextLength)
        {
            text = text[..MaxArticleTextLength];
        }

        var resolvedUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;

        return new ArticleContent
        {
            ResolvedUrl = resolvedUrl,
            Title = title,
            Text = text
        };
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
