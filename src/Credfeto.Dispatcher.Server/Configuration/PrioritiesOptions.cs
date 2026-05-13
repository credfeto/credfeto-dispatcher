using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Dispatcher.Server.Configuration;

[DebuggerDisplay("Owners={Owners.Count} Repos={Repos.Count} StuckDependabotTimeoutHours={StuckDependabotTimeoutHours}")]
public sealed class PrioritiesOptions
{
    public IReadOnlyList<string> Owners { get; set; } = [];

    public IReadOnlyList<string> Repos { get; set; } = [];

    public int StuckDependabotTimeoutHours { get; set; } = 3;
}
