using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Credfeto.Database;
using Credfeto.Dispatcher.Storage.Configuration;
using Credfeto.Dispatcher.Storage.Database.Rows;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Integration.Tests;

public abstract class SqlServerIntegrationTestBase : TestBase, IAsyncDisposable
{
    private static readonly string? ConnectionString = Environment.GetEnvironmentVariable(
        "INTEGRATION_TEST_SQL_CONNECTION_STRING"
    );

    private readonly ServiceProvider? _serviceProvider;
    private readonly TransactionScope? _transactionScope;

    protected SqlServerIntegrationTestBase()
    {
        if (string.IsNullOrEmpty(ConnectionString))
        {
            Assert.Skip(
                "SQL Server integration tests are disabled: INTEGRATION_TEST_SQL_CONNECTION_STRING is not set."
            );

            return;
        }

        this._transactionScope = new TransactionScope(
            TransactionScopeOption.RequiresNew,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled
        );

        ServiceCollection services = new();
        services.AddSingleton<IOptions<DatabaseConfiguration>>(_ =>
            Options.Create(new DatabaseConfiguration { ConnectionString = ConnectionString })
        );
        services.AddStorage();

        this._serviceProvider = services.BuildServiceProvider();
    }

    protected string TestRepository { get; } = $"test-{Guid.NewGuid():N}/repo";

    protected IDatabase Database =>
        this._serviceProvider?.GetRequiredService<IDatabase>()
        ?? throw new InvalidOperationException("Service provider not initialised");

    private protected IReadOnlyList<PullRequestRow> ForTestRepo(IEnumerable<PullRequestRow> rows) =>
        [.. rows.Where(r => StringComparer.Ordinal.Equals(r.Repository, this.TestRepository))];

    private protected IReadOnlyList<IssueRow> ForTestRepo(IEnumerable<IssueRow> rows) =>
        [.. rows.Where(r => StringComparer.Ordinal.Equals(r.Repository, this.TestRepository))];

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        this._transactionScope?.Dispose();

        if (this._serviceProvider is not null)
        {
            await this._serviceProvider.DisposeAsync();
        }
    }
}
