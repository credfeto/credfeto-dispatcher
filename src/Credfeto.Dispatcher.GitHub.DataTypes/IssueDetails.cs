using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{Number}: {Title} ({Status})")]
public sealed record IssueDetails(
    int Number,
    string Title,
    string Status,
    string Priority,
    bool OnHold,
    Uri HtmlUrl,
    ItemRepository Repository,
    LastNotification LastNotification
);
