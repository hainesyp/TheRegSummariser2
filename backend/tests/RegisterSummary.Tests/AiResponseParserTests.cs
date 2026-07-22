using RegisterSummary.Core.Models;
using RegisterSummary.Infrastructure.Ai;

namespace RegisterSummary.Tests;

public class AiResponseParserTests
{
    [Fact]
    public void Parse_ParsesWellFormedJson()
    {
        const string json = """
            {"title":"Big Breach Hits Vendor","category":"Cybersecurity","risk":"High","summary":["a","b","c","d","e"],"recommendations":["patch now"]}
            """;

        var result = AiResponseParser.Parse(json);

        Assert.Equal("Big Breach Hits Vendor", result.Title);
        Assert.Equal(ArticleCategory.Cybersecurity, result.Category);
        Assert.Equal(RiskSeverity.High, result.Risk);
        Assert.Equal(5, result.SummaryBullets.Count);
        Assert.Equal(["patch now"], result.Recommendations);
    }

    [Fact]
    public void Parse_StripsMarkdownCodeFences()
    {
        const string wrapped = """
            Sure, here you go:
            ```json
            {"title":"New GPU Announced","category":"AI","risk":null,"summary":["a","b","c","d","e"],"recommendations":[]}
            ```
            """;

        var result = AiResponseParser.Parse(wrapped);

        Assert.Equal("New GPU Announced", result.Title);
        Assert.Equal(ArticleCategory.AI, result.Category);
        Assert.Null(result.Risk);
        Assert.Null(result.Recommendations);
    }

    [Fact]
    public void Parse_IsCaseInsensitiveForEnumValues()
    {
        const string json = """{"title":"t","category":"cloud","risk":null,"summary":["a"]}""";

        var result = AiResponseParser.Parse(json);

        Assert.Equal(ArticleCategory.Cloud, result.Category);
    }

    [Fact]
    public void Parse_ThrowsOnUnrecognisedCategory()
    {
        const string json = """{"title":"t","category":"Sports","risk":null,"summary":["a"]}""";

        Assert.Throws<AiResponseParseException>(() => AiResponseParser.Parse(json));
    }

    [Fact]
    public void Parse_ThrowsOnMissingSummary()
    {
        const string json = """{"title":"t","category":"Other","risk":null,"summary":[]}""";

        Assert.Throws<AiResponseParseException>(() => AiResponseParser.Parse(json));
    }

    [Fact]
    public void Parse_ThrowsWhenNoJsonObjectPresent()
    {
        Assert.Throws<AiResponseParseException>(() => AiResponseParser.Parse("I refuse to answer."));
    }
}
