using Credfeto.Dispatcher.Storage.Configuration;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests.Configuration;

public sealed class DatabaseConfigurationValidatorTests : TestBase
{
    private readonly DatabaseConfigurationValidator _validator = new();

    [Fact]
    public void DefaultConfigurationIsSqlServerWithNoConnectionString()
    {
        DatabaseConfiguration configuration = new();

        Assert.Equal(expected: DatabaseProvider.SqlServer, actual: configuration.Provider);
        Assert.Equal(expected: string.Empty, actual: configuration.ConnectionString);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SqlServerWithoutConnectionStringFails(string connectionString)
    {
        ValidateOptionsResult result = this._validator.Validate(
            name: null,
            options: new DatabaseConfiguration
            {
                Provider = DatabaseProvider.SqlServer,
                ConnectionString = connectionString,
            }
        );

        Assert.True(condition: result.Failed, userMessage: "SqlServer without a connection string should fail");
        Assert.Contains(
            expectedSubstring: "ConnectionString",
            actualString: result.FailureMessage,
            comparisonType: System.StringComparison.Ordinal
        );
    }

    [Fact]
    public void SqlServerWithConnectionStringSucceeds()
    {
        ValidateOptionsResult result = this._validator.Validate(
            name: null,
            options: new DatabaseConfiguration
            {
                Provider = DatabaseProvider.SqlServer,
                ConnectionString = "Server=localhost;Database=Dispatcher",
            }
        );

        Assert.True(condition: result.Succeeded, userMessage: "SqlServer with a connection string should succeed");
    }

    [Fact]
    public void InMemoryWithoutConnectionStringSucceeds()
    {
        ValidateOptionsResult result = this._validator.Validate(
            name: null,
            options: new DatabaseConfiguration { Provider = DatabaseProvider.InMemory, ConnectionString = string.Empty }
        );

        Assert.True(condition: result.Succeeded, userMessage: "InMemory does not need a connection string");
    }
}
