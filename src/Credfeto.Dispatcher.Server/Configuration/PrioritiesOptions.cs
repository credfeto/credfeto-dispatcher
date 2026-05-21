using System.Collections.Generic;
using System.Diagnostics;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.Server.Configuration;

[DebuggerDisplay(
    "Owners={Owners.Count} Repos={Repos.Count} StuckDependabotTimeoutHours={StuckDependabotTimeoutHours} MaxIssues={MaxIssues} AdditionalBotPrRules={AdditionalBotPrRules.Count}"
)]
public sealed class PrioritiesOptions
{
    public IReadOnlyList<string> Owners { get; set; } = [];

    public IReadOnlyList<string> Repos { get; set; } = [];

    public int StuckDependabotTimeoutHours { get; set; } = 3;

    public int MaxIssues { get; set; } = 10;

    public IReadOnlyList<BotPrRule> AdditionalBotPrRules { get; set; } = [];
}
