using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Database;

namespace Credfeto.Dispatcher.Storage.Tests;

internal sealed class TestDatabaseStub : IDatabase
{
    private readonly Dictionary<Type, Delegate> _genericReturns = [];
    private int _voidCallCount;

    public int VoidExecuteCallCount => this._voidCallCount;

    public void SetReturn<T>(T value)
    {
        this._genericReturns[typeof(T)] = new Func<T>(() => value);
    }

    public ValueTask ExecuteAsync(
        Func<DbConnection, CancellationToken, ValueTask> action,
        CancellationToken cancellationToken
    )
    {
        Interlocked.Increment(ref this._voidCallCount);

        return ValueTask.CompletedTask;
    }

    public ValueTask<T> ExecuteAsync<T>(
        Func<DbConnection, CancellationToken, ValueTask<T>> action,
        CancellationToken cancellationToken
    )
    {
        if (this._genericReturns.TryGetValue(typeof(T), out Delegate? factory) && factory is Func<T> typed)
        {
            return new ValueTask<T>(typed());
        }

        return default;
    }
}
