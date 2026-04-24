using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{Number}: {Title} ({Status})")]
public sealed record IssueDetails(
    int Number,
    string Title,
    string Status,
    Uri HtmlUrl,
    IReadOnlyList<string> Assignees,
    IReadOnlyList<string> Labels,
    Uri? LinkedPullRequestUrl
);
