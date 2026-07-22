using RegisterSummary.Core.Models;

namespace RegisterSummary.Core.Interfaces;

public interface IAiClient
{
    /// <summary>
    /// The provider name this client handles, e.g. "OpenAI", "Gemini", "Claude". Matched against AiOptions.Provider.
    /// </summary>
    string ProviderName { get; }

    Task<AiSummaryResult> SummarizeAsync(string articleUrl, string articleText, string promptTemplate, CancellationToken cancellationToken);
}
