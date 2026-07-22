using RegisterSummary.Core.Models;

namespace RegisterSummary.Core.Interfaces;

public interface IHtmlReportBuilder
{
    /// <summary>
    /// Builds the collated, categorised (and for Cybersecurity, severity-prioritised) HTML report
    /// from the successfully summarised article outcomes. Failed/pending outcomes are ignored.
    /// </summary>
    string Build(IEnumerable<ArticleOutcome> outcomes);
}
