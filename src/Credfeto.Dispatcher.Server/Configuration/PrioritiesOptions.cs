using System.Collections.Generic;

namespace Credfeto.Dispatcher.Server.Configuration;

public sealed class PrioritiesOptions
{
    public IReadOnlyList<string> Owners { get; set; } = [];

    public IReadOnlyList<string> Repos { get; set; } = [];
}
