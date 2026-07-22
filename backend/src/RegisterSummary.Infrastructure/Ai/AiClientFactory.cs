using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RegisterSummary.Core.Interfaces;

namespace RegisterSummary.Infrastructure.Ai;

public sealed class AiClientFactory(IServiceProvider serviceProvider, IOptions<AiOptions> options) : IAiClientFactory
{
    public IAiClient GetClient()
    {
        var provider = options.Value.Provider;
        var client = serviceProvider.GetKeyedService<IAiClient>(provider);

        return client ?? throw new InvalidOperationException(
            $"Unknown AI provider configured: '{provider}'. Valid values: OpenAI, Gemini, Claude.");
    }
}
