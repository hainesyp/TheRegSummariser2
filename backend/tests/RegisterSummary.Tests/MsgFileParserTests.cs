using RegisterSummary.Infrastructure.Msg;

namespace RegisterSummary.Tests;

public class MsgFileParserTests
{
    [Theory]
    [InlineData("https://go.reg.cx/tdml/4nNc?utm_source=daily&utm_medium=newsletter&utm_content=top-article")]
    [InlineData("https://go.reg.cx/tdml/4nNB?utm_source=daily&utm_medium=newsletter&utm_content=article")]
    public void FilterAndDeduplicate_KeepsQualifyingArticleLinks(string url)
    {
        var result = MsgFileParser.FilterAndDeduplicate([url]);

        Assert.Equal([url], result);
    }

    [Theory]
    [InlineData("https://go.reg.cx/tdml/4nN0?utm_source=daily&utm_medium=newsletter&utm_content=sponsored")]
    [InlineData("https://go.reg.cx/tdml/4nN0")]
    [InlineData("https://www.theregister.com/some/other/page?utm_content=article")]
    [InlineData("not-a-url")]
    public void FilterAndDeduplicate_DropsNonQualifyingLinks(string url)
    {
        var result = MsgFileParser.FilterAndDeduplicate([url]);

        Assert.Empty(result);
    }

    [Fact]
    public void FilterAndDeduplicate_RemovesDuplicateUrlsButKeepsFirstSeenOrder()
    {
        var urls = new[]
        {
            "https://go.reg.cx/tdml/4nNc?utm_content=top-article",
            "https://go.reg.cx/tdml/4nNB?utm_content=article",
            "https://go.reg.cx/tdml/4nNc?utm_content=top-article",
            "https://go.reg.cx/tdml/4nN0?utm_content=article",
        };

        var result = MsgFileParser.FilterAndDeduplicate(urls);

        Assert.Equal(
            [
                "https://go.reg.cx/tdml/4nNc?utm_content=top-article",
                "https://go.reg.cx/tdml/4nNB?utm_content=article",
                "https://go.reg.cx/tdml/4nN0?utm_content=article",
            ],
            result);
    }
}
