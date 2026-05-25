using System.Collections.Generic;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Configuration;

public sealed class GitHubPullRequestFilterOptions
{
    public IReadOnlyList<BotPrRule> AdoptionRules { get; set; } = [];
}
