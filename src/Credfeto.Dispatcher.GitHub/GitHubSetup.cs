using System;
using System.Net.Http;
using System.Net.Http.Headers;
using Credfeto.Dispatcher.GitHub.BackgroundServices;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub;

public static class GitHubSetup
{
    private const string GitHubApiBase = "https://api.github.com/";
    private const string UserAgent = "credfeto-dispatcher";
    private const string GitHubApiVersionHeaderName = "X-GitHub-Api-Version";
    private const string GitHubApiVersion = "2026-03-10";

    public static IServiceCollection AddGitHub(this IServiceCollection services)
    {
        return services
            .AddSingleton<IValidateOptions<GitHubOptions>, GitHubOptionsValidator>()
            .AddHttpClient(name: "GitHub", configureClient: ConfigureGitHubHttpClient)
            .AddStandardResilienceHandler()
            .Services.AddSingleton<INotificationPoller, NotificationPoller>()
            .AddSingleton<INotificationFilter, NotificationFilter>()
            .AddSingleton<IPullRequestDetailFetcher, PullRequestDetailFetcher>()
            .AddSingleton<IIssueDetailFetcher, IssueDetailFetcher>()
            .AddSingleton<IWorkItemScanner, WorkItemScanner>()
            .AddHostedService<StartupNotificationService>()
            .AddHostedService<GitHubPollingWorker>()
            .AddHostedService<WorkItemScannerService>();
    }

    private static void ConfigureGitHubHttpClient(
        IServiceProvider serviceProvider,
        HttpClient client
    )
    {
        client.BaseAddress = new Uri(GitHubApiBase);
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(productName: UserAgent, productVersion: null)
        );
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json")
        );
        client.DefaultRequestHeaders.Add(name: GitHubApiVersionHeaderName, value: GitHubApiVersion);

        GitHubOptions options = serviceProvider.GetRequiredService<IOptions<GitHubOptions>>().Value;

        if (!string.IsNullOrWhiteSpace(options.Token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                scheme: "Bearer",
                parameter: options.Token
            );
        }
    }
}
