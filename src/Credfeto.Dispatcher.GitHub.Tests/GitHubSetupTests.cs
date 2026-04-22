using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Services;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests;

public sealed class GitHubSetupTests : DependencyInjectionTestsBase
{
    public GitHubSetupTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure)
    {
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddGitHub()
                       .AddSingleton<IOptions<GitHubOptions>>(Options.Create(new GitHubOptions()));
    }

    [Fact]
    public void NotificationPollerShouldBeRegistered()
    {
        this.RequireService<INotificationPoller>();
    }

    [Fact]
    public void NotificationPollerShouldBeOfCorrectType()
    {
        this.RequireServiceInCollectionFor<INotificationPoller, NotificationPoller>();
    }

    [Fact]
    public void NotificationFilterShouldBeRegistered()
    {
        this.RequireService<INotificationFilter>();
    }

    [Fact]
    public void NotificationFilterShouldBeOfCorrectType()
    {
        this.RequireServiceInCollectionFor<INotificationFilter, NotificationFilter>();
    }
}
