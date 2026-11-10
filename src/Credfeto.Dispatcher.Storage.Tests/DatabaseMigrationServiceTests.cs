using System.Threading.Tasks;
using Credfeto.Dispatcher.Storage.Configuration;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests;

public sealed class DatabaseMigrationServiceTests : TestBase
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StartAsyncSkipsMigrationWhenNoConnectionStringIsConfiguredAsync(string connectionString)
    {
        DatabaseMigrationService service = new(
            Options.Create(new DatabaseConfiguration { ConnectionString = connectionString })
        );

        ValueTask start = service.StartAsync(this.CancellationToken());

        Assert.True(
            condition: start.IsCompletedSuccessfully,
            userMessage: "No migration should run without a database"
        );
        await start;
    }
}
