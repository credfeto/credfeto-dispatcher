using Credfeto.Dispatcher.GitHub.BackgroundServices;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Dispatcher.GitHub;

public static class GitHubSetup
{
    public static IServiceCollection AddGitHub(this IServiceCollection services)
    {
        return services
            .AddHttpClient<IGitHubNotificationPoller, GitHubNotificationPoller>()
            .AddStandardResilienceHandler()
            .Services
            .AddSingleton<INotificationFilter, NotificationFilter>()
            .AddHostedService<GitHubPollingWorker>();
    }
}
