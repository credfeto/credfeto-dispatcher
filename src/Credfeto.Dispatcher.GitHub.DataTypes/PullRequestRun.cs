using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{Name}: {Status} ({Conclusion})")]
public sealed record PullRequestRun(
    string Name,
    string Status,
    string? Conclusion,
    Uri Url,
    bool IsRequired,
    string HeadSha
);
