using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RegisterSummary.Core.Interfaces;
using RegisterSummary.Infrastructure.Ai;
using RegisterSummary.Infrastructure.Articles;
using RegisterSummary.Infrastructure.Jobs;
using RegisterSummary.Infrastructure.Msg;
using RegisterSummary.Infrastructure.Reports;

namespace RegisterSummary.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRegisterSummaryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IMsgParser, MsgFileParser>();
        services.AddSingleton<IHtmlReportBuilder, HtmlReportBuilder>();
        services.AddSingleton<IJobStore, InMemoryJobStore>();
        services.AddSingleton<IJobQueue, ChannelJobQueue>();
        services.AddSingleton<IAiClientFactory, AiClientFactory>();
        services.AddHostedService<JobProcessingService>();

        services.AddHttpClient<IArticleFetcher, ArticleFetcher>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RegisterSummaryBot/1.0 (+https://github.com/)");
        });

        services.AddKeyedHttpClient<IAiClient, OpenAiClient>("OpenAI", client =>
        {
            client.BaseAddress = new Uri("https://api.openai.com/v1/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddKeyedHttpClient<IAiClient, GeminiClient>("Gemini", client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddKeyedHttpClient<IAiClient, ClaudeClient>("Claude", client =>
        {
            client.BaseAddress = new Uri("https://api.anthropic.com/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        return services;
    }

    private static IServiceCollection AddKeyedHttpClient<TService, TImplementation>(
        this IServiceCollection services, string key, Action<HttpClient> configureClient)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddHttpClient(key, configureClient);

        services.AddKeyedSingleton<TService>(key, (sp, _) =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(key);
            return ActivatorUtilities.CreateInstance<TImplementation>(sp, httpClient);
        });

        return services;
    }
}
