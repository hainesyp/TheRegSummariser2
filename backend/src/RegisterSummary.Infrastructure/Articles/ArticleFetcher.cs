using System.Text.RegularExpressions;
using HtmlAgilityPack;
using RegisterSummary.Core.Interfaces;

namespace RegisterSummary.Infrastructure.Articles;

public sealed partial class ArticleFetcher(HttpClient httpClient) : IArticleFetcher
{
    private const int MaxArticleTextLength = 15_000;

    private static readonly string[] NoiseNodeSelectors =
    [
        "//script", "//style", "//nav", "//header", "//footer", "//aside", "//noscript", "//form", "//iframe"
    ];

    public async Task<string> FetchArticleTextAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

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

        return text;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
