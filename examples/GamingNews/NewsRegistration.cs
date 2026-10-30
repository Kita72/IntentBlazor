using IntentBlazor.Blazor;
using IntentBlazor.ExampleHost;
using IntentBlazor.Runtime;

namespace IntentBlazor.GamingNews;

public static class NewsRegistration
{
    public static IServiceCollection AddGamingNews(this IServiceCollection services, IConfiguration configuration, string directory)
    {
        if (configuration["News:FeedUrl"] is { Length: > 0 } address)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" || !string.IsNullOrEmpty(endpoint.UserInfo))
                throw new ArgumentException("News:FeedUrl must be a host-configured HTTPS JSON feed without credentials in the URL.");
            services.AddHttpClient<INewsSource, HttpNewsSource>(client => { client.BaseAddress = endpoint; client.Timeout = TimeSpan.FromSeconds(10); })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        }
        else services.AddSingleton<INewsSource, FixtureNewsSource>();
        services.AddSingleton(new NewsLibrary(Path.Combine(directory, "news-library.sqlite")));
        services.AddIntentRenderer<NewsCitationsContract, NewsCitations>();
        services.AddIntentAction<BookmarkArticleAction>(BookmarkArticleAction.Definition);
        services.AddIntentAction<SaveComparisonAction>(SaveComparisonAction.Definition);
        services.AddExampleScenario<NewsScenario>(configuration["Ai:Provider"] is { Length: > 0 } provider && !provider.Equals("Fixture", StringComparison.OrdinalIgnoreCase));
        return services;
    }
}
