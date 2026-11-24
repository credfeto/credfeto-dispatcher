using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.Server.Helpers;
using Credfeto.Dispatcher.Storage.Configuration;
using FunFair.Test.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Credfeto.Dispatcher.Server.Tests.Helpers;

public sealed class ServerStartupTests : TestBase
{
    private const string DATABASE_PROVIDER_KEY = "DatabaseConfiguration:Provider";
    private const string GIT_HUB_TOKEN_KEY = "GitHub:Token";
    private const string MAX_EVENT_REFRESHES_PER_POLL_KEY = "GitHub:Filter:MaxEventRefreshesPerPoll";

    [Fact]
    public async Task StartingWithValidConfigurationSucceedsAsync()
    {
        using IHost host = BuildHost(databaseProvider: "InMemory", gitHubToken: "token");
        CancellationToken cancellationToken = this.CancellationToken();

        await host.StartAsync(cancellationToken);
        await host.StopAsync(cancellationToken);
    }

    [Fact]
    public Task StartingWithInvalidDatabaseConfigurationThrowsAsync()
    {
        return this.AssertStartThrowsOptionsValidationExceptionAsync(
            databaseProvider: "SqlServer",
            gitHubToken: "token"
        );
    }

    [Fact]
    public Task StartingWithInvalidGitHubConfigurationThrowsAsync()
    {
        return this.AssertStartThrowsOptionsValidationExceptionAsync(databaseProvider: "InMemory", gitHubToken: "");
    }

    [Fact]
    public static void EventRefreshBudgetIsReadFromTheGitHubFilterSection()
    {
        using IHost host = BuildHost(
            databaseProvider: "InMemory",
            gitHubToken: "token",
            extraSettings: [new KeyValuePair<string, string?>(key: MAX_EVENT_REFRESHES_PER_POLL_KEY, value: "35")]
        );

        GitHubOptions options = host.Services.GetRequiredService<IOptions<GitHubOptions>>().Value;

        Assert.Equal(expected: 35, actual: options.Filter.MaxEventRefreshesPerPoll);
    }

    [Fact]
    public static void EventRefreshBudgetDefaultsToTwentyWhenNotConfigured()
    {
        using IHost host = BuildHost(databaseProvider: "InMemory", gitHubToken: "token");

        GitHubOptions options = host.Services.GetRequiredService<IOptions<GitHubOptions>>().Value;

        Assert.Equal(expected: 20, actual: options.Filter.MaxEventRefreshesPerPoll);
    }

    private async Task AssertStartThrowsOptionsValidationExceptionAsync(string databaseProvider, string gitHubToken)
    {
        using IHost host = BuildHost(databaseProvider: databaseProvider, gitHubToken: gitHubToken);
        CancellationToken cancellationToken = this.CancellationToken();

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(cancellationToken));
    }

    private static IHost BuildHost(
        string databaseProvider,
        string gitHubToken,
        IReadOnlyList<KeyValuePair<string, string?>>? extraSettings = null
    )
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(key: DATABASE_PROVIDER_KEY, value: databaseProvider),
            new KeyValuePair<string, string?>(key: GIT_HUB_TOKEN_KEY, value: gitHubToken),
            .. extraSettings ?? [],
        ]);

        builder
            .Services.AddValidatedOptions(builder.Configuration)
            .AddSingleton<IValidateOptions<DatabaseConfiguration>, DatabaseConfigurationValidator>()
            .AddSingleton<IValidateOptions<GitHubOptions>, GitHubOptionsValidator>();

        return builder.Build();
    }
}
