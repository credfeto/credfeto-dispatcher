using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
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
                       .AddSingleton<IOptions<GitHubOptions>>(Options.Create(new GitHubOptions()))
                       .AddSingleton<IETagStore, TestETagStore>();
    }

    [SuppressMessage(category: "Microsoft.Performance", checkId: "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by dependency injection")]
    private sealed class TestETagStore : IETagStore
    {
        public ValueTask<string?> GetETagAsync(string key, CancellationToken cancellationToken)
        {
            return new((string?)null);
        }

        public ValueTask SaveETagAsync(string key, string eTag, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
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

    [Fact]
    public void PullRequestDetailFetcherShouldBeRegistered()
    {
        this.RequireService<IPullRequestDetailFetcher>();
    }

    [Fact]
    public void PullRequestDetailFetcherShouldBeOfCorrectType()
    {
        this.RequireServiceInCollectionFor<IPullRequestDetailFetcher, PullRequestDetailFetcher>();
    }
}
