using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.Configuration;

[DebuggerDisplay("Reasons: {Reasons.Count}, LabelFilter: {LabelFilter.Count}")]
public sealed class GitHubFilterOptions
{
    public IReadOnlyList<string> Reasons { get; set; } = [];

    public IReadOnlyList<string> LabelFilter { get; set; } = [];

    public IReadOnlyList<string> NoWorkFilter { get; set; } = [];

    public IReadOnlyList<string> AllowedOwners { get; set; } = [];

    public IReadOnlyList<string> AllowedRepos { get; set; } = [];

    public IReadOnlyList<string> ExcludedRepos { get; set; } = [];

    public bool PollIssueEdits { get; set; }

    public bool PollEvents { get; set; }

    public string MentionedUser { get; set; } = string.Empty;
}
