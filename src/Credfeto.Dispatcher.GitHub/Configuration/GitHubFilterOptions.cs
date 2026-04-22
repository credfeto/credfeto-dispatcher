using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.Configuration;

[DebuggerDisplay("Reasons: {Reasons.Count}, LabelFilter: {LabelFilter.Count}")]
public sealed class GitHubFilterOptions
{
    public IReadOnlyList<string> Reasons { get; init; } = [];

    public IReadOnlyList<string> LabelFilter { get; init; } = [];

    public IReadOnlyList<string> NoWorkFilter { get; init; } = [];

    public IReadOnlyList<string> AllowedOwners { get; init; } = [];

    public IReadOnlyList<string> ExcludedRepos { get; init; } = [];
}
