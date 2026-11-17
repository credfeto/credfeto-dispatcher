using System;

namespace Credfeto.Dispatcher.Server.Tests;

internal sealed class EnvironmentOverride : IDisposable
{
    private readonly string _name;
    private readonly string? _previousValue;

    public EnvironmentOverride(string name, string? value)
    {
        this._name = name;
        this._previousValue = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(variable: name, value: value);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(variable: this._name, value: this._previousValue);
    }
}
