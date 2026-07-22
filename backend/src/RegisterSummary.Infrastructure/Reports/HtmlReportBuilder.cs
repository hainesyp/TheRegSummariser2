using System.Net;
using System.Text;
using RegisterSummary.Core.Interfaces;
using RegisterSummary.Core.Models;

namespace RegisterSummary.Infrastructure.Reports;

public sealed class HtmlReportBuilder : IHtmlReportBuilder
{
    private static readonly RiskSeverity[] SeverityOrder =
    [
        RiskSeverity.Critical, RiskSeverity.High, RiskSeverity.Medium, RiskSeverity.Low, RiskSeverity.Information
    ];

    private static readonly ArticleCategory[] FlatCategoryOrder =
    [
        ArticleCategory.AI, ArticleCategory.Cloud, ArticleCategory.Other
    ];

    public string Build(IEnumerable<ArticleOutcome> outcomes)
    {
        var completed = outcomes
            .Where(o => o.Status == ArticleProcessingStatus.Completed && o.Result is not null)
            .ToList();

        var body = new StringBuilder();
        var sectionsWritten = 0;

        var cybersecurity = completed.Where(o => o.Result!.Category == ArticleCategory.Cybersecurity).ToList();
        if (cybersecurity.Count > 0)
        {
            AppendSeparatorIfNeeded(body, ref sectionsWritten);
            body.AppendLine("<h1>Cybersecurity</h1>");

            foreach (var severity in SeverityOrder)
            {
                var group = cybersecurity.Where(o => o.Result!.Risk == severity).ToList();
                if (group.Count == 0)
                {
                    continue;
                }

                body.AppendLine($"<h2>{severity}</h2>");

                foreach (var outcome in group)
                {
                    AppendArticle(body, outcome, includeRecommendations: true);
                }
            }
        }

        foreach (var category in FlatCategoryOrder)
        {
            var articles = completed.Where(o => o.Result!.Category == category).ToList();
            if (articles.Count == 0)
            {
                continue;
            }

            AppendSeparatorIfNeeded(body, ref sectionsWritten);
            body.AppendLine($"<h1>{category}</h1>");

            foreach (var outcome in articles)
            {
                AppendArticle(body, outcome, includeRecommendations: false);
            }
        }

        return WrapDocument(body.ToString());
    }

    private static void AppendSeparatorIfNeeded(StringBuilder body, ref int sectionsWritten)
    {
        if (sectionsWritten > 0)
        {
            body.AppendLine("<br>");
        }

        sectionsWritten++;
    }

    private static void AppendArticle(StringBuilder body, ArticleOutcome outcome, bool includeRecommendations)
    {
        var result = outcome.Result!;
        var title = WebUtility.HtmlEncode(result.Title);
        var href = WebUtility.HtmlEncode(outcome.Url);

        body.AppendLine($"<h3><a href=\"{href}\">{title}</a></h3>");

        AppendBulletList(body, result.SummaryBullets);

        if (includeRecommendations && result.Recommendations is { Count: > 0 })
        {
            body.AppendLine("<p><strong>Recommendations:</strong></p>");
            AppendBulletList(body, result.Recommendations);
        }
    }

    private static void AppendBulletList(StringBuilder body, IReadOnlyList<string> items)
    {
        body.AppendLine("<ul>");

        foreach (var item in items)
        {
            body.AppendLine($"<li>{WebUtility.HtmlEncode(item)}</li>");
        }

        body.AppendLine("</ul>");
    }

    private static string WrapDocument(string bodyContent)
    {
        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="UTF-8">
            <title>The Register Summary</title>
            </head>
            <body>
            {bodyContent.TrimEnd()}
            </body>
            </html>
            """ + Environment.NewLine;
    }
}
