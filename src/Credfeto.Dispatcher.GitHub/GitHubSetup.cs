using System;
using System.Net.Http;
using System.Net.Http.Headers;
using Credfeto.Dispatcher.GitHub.BackgroundServices;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub;

public static class GitHubSetup
{
    private const string GIT_HUB_API_BASE = "https://api.github.com/";
    private const string USER_AGENT = "credfeto-dispatcher";
    private const string GIT_HUB_API_VERSION_HEADER_NAME = "X-GitHub-Api-Version";
    private const string GIT_HUB_API_VERSION = "2026-03-10";

    public static IServiceCollection AddGitHub(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        return services
            .AddSingleton<IValidateOptions<GitHubOptions>, GitHubOptionsValidator>()
            .AddHttpClient(name: "GitHub", configureClient: ConfigureGitHubHttpClient)
            .AddStandardResilienceHandler()
            .Services.AddSingleton<GitHubRepoHelper>()
            .AddSingleton<INotificationPoller, NotificationPoller>()
            .AddSingleton<IModifiedIssueMentionPoller, ModifiedIssueMentionPoller>()
            .AddSingleton<IRepoEventPoller, RepoEventPoller>()
            .AddSingleton<INotificationFilter, NotificationFilter>()
            .AddSingleton<IPullRequestDetailFetcher, PullRequestDetailFetcher>()
            .AddSingleton<IIssueDetailFetcher, IssueDetailFetcher>()
            .AddSingleton<IWorkItemScanner, WorkItemScanner>()
            .AddHostedService<StartupNotificationService>()
            .AddHostedService<GitHubPollingWorker>()
            .AddHostedService<WorkItemScannerService>()
            .AddHostedService<RepoEventPollerService>();
    }

    private static void ConfigureGitHubHttpClient(IServiceProvider serviceProvider, HttpClient client)
    {
        client.BaseAddress = new Uri(GIT_HUB_API_BASE);
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(productName: USER_AGENT, productVersion: null)
        );
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add(name: GIT_HUB_API_VERSION_HEADER_NAME, value: GIT_HUB_API_VERSION);

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
