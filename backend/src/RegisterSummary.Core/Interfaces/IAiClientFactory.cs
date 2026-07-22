namespace RegisterSummary.Core.Interfaces;

public interface IAiClientFactory
{
    /// <summary>
    /// Resolves the IAiClient for the currently configured AI provider (AiOptions.Provider).
    /// </summary>
    IAiClient GetClient();
}
