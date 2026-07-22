using RegisterSummary.Core.Models;
using RegisterSummary.Infrastructure.Reports;

namespace RegisterSummary.Tests;

public class HtmlReportBuilderTests
{
    private readonly HtmlReportBuilder _builder = new();

    private static ArticleOutcome Completed(
        string title,
        ArticleCategory category,
        RiskSeverity? risk = null,
        List<string>? recommendations = null,
        string? url = null) =>
        new()
        {
            Url = url ?? $"https://go.reg.cx/tdml/{title}",
            Status = ArticleProcessingStatus.Completed,
            Result = new AiSummaryResult
            {
                Title = title,
                Category = category,
                Risk = risk,
                SummaryBullets = ["point one", "point two"],
                Recommendations = recommendations
            }
        };

    [Fact]
    public void Build_OrdersCybersecurityArticlesBySeverityRegardlessOfInputOrder()
    {
        var outcomes = new[]
        {
            Completed("Low Issue", ArticleCategory.Cybersecurity, RiskSeverity.Low, ["fix"]),
            Completed("Critical Issue", ArticleCategory.Cybersecurity, RiskSeverity.Critical, ["fix"]),
            Completed("High Issue", ArticleCategory.Cybersecurity, RiskSeverity.High, ["fix"]),
        };

        var html = _builder.Build(outcomes);

        var criticalIndex = html.IndexOf("<h2>Critical</h2>", StringComparison.Ordinal);
        var highIndex = html.IndexOf("<h2>High</h2>", StringComparison.Ordinal);
        var lowIndex = html.IndexOf("<h2>Low</h2>", StringComparison.Ordinal);

        Assert.True(criticalIndex >= 0 && criticalIndex < highIndex);
        Assert.True(highIndex < lowIndex);
        Assert.DoesNotContain("<h2>Medium</h2>", html);
        Assert.DoesNotContain("<h2>Information</h2>", html);
    }

    [Fact]
    public void Build_GivesAiCloudAndOtherEachTheirOwnHeading()
    {
        var outcomes = new[]
        {
            Completed("An AI Story", ArticleCategory.AI),
            Completed("A Cloud Story", ArticleCategory.Cloud),
            Completed("An Other Story", ArticleCategory.Other),
        };

        var html = _builder.Build(outcomes);

        Assert.Contains("<h1>AI</h1>", html);
        Assert.Contains("<h1>Cloud</h1>", html);
        Assert.Contains("<h1>Other</h1>", html);
        Assert.DoesNotContain("<h1>Cybersecurity</h1>", html);
    }

    [Fact]
    public void Build_OrdersTopLevelSectionsCybersecurityThenAiThenCloudThenOther()
    {
        var outcomes = new[]
        {
            Completed("Other Story", ArticleCategory.Other),
            Completed("Cloud Story", ArticleCategory.Cloud),
            Completed("AI Story", ArticleCategory.AI),
            Completed("Breach", ArticleCategory.Cybersecurity, RiskSeverity.Critical, ["rotate keys"]),
        };

        var html = _builder.Build(outcomes);

        var cyberIndex = html.IndexOf("<h1>Cybersecurity</h1>", StringComparison.Ordinal);
        var aiIndex = html.IndexOf("<h1>AI</h1>", StringComparison.Ordinal);
        var cloudIndex = html.IndexOf("<h1>Cloud</h1>", StringComparison.Ordinal);
        var otherIndex = html.IndexOf("<h1>Other</h1>", StringComparison.Ordinal);

        Assert.True(cyberIndex < aiIndex);
        Assert.True(aiIndex < cloudIndex);
        Assert.True(cloudIndex < otherIndex);
    }

    [Fact]
    public void Build_RendersTitleAsHyperlinkToArticleUrl()
    {
        var outcomes = new[] { Completed("An AI Story", ArticleCategory.AI, url: "https://go.reg.cx/tdml/abc?utm_content=article") };

        var html = _builder.Build(outcomes);

        Assert.Contains("<a href=\"https://go.reg.cx/tdml/abc?utm_content=article\">An AI Story</a>", html);
    }

    [Fact]
    public void Build_HtmlEncodesAmpersandsInUrlsAndSpecialCharactersInTitles()
    {
        var outcomes = new[]
        {
            Completed(
                "Vendor <Patches> Flaw & Warns Users",
                ArticleCategory.AI,
                url: "https://go.reg.cx/tdml/abc?a=1&b=2")
        };

        var html = _builder.Build(outcomes);

        Assert.Contains("href=\"https://go.reg.cx/tdml/abc?a=1&amp;b=2\"", html);
        Assert.Contains("Vendor &lt;Patches&gt; Flaw &amp; Warns Users", html);
        Assert.DoesNotContain("<Patches>", html);
    }

    [Fact]
    public void Build_OmitsRecommendationsForNonCybersecurityArticles()
    {
        var outcomes = new[] { Completed("An AI Story", ArticleCategory.AI, recommendations: ["should not appear"]) };

        var html = _builder.Build(outcomes);

        Assert.DoesNotContain("Recommendations", html);
        Assert.DoesNotContain("should not appear", html);
    }

    [Fact]
    public void Build_IncludesRecommendationsForCybersecurityArticles()
    {
        var outcomes = new[] { Completed("Breach", ArticleCategory.Cybersecurity, RiskSeverity.Critical, ["rotate keys"]) };

        var html = _builder.Build(outcomes);

        Assert.Contains("Recommendations", html);
        Assert.Contains("<li>rotate keys</li>", html);
    }

    [Fact]
    public void Build_IgnoresIncompleteOutcomes()
    {
        var failed = new ArticleOutcome { Url = "https://go.reg.cx/tdml/x", Status = ArticleProcessingStatus.Failed, Error = "boom" };

        var html = _builder.Build([failed]);

        Assert.DoesNotContain("<h1>", html);
        Assert.DoesNotContain("<h3>", html);
    }

    [Fact]
    public void Build_ProducesAWellFormedHtmlDocument()
    {
        var outcomes = new[] { Completed("An AI Story", ArticleCategory.AI) };

        var html = _builder.Build(outcomes);

        Assert.StartsWith("<!DOCTYPE html>", html.TrimStart());
        Assert.Contains("<html", html);
        Assert.Contains("</html>", html);
        Assert.Contains("<body>", html);
        Assert.Contains("</body>", html);
    }
}
