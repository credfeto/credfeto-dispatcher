using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{Author}: {Body}")]
public sealed record PullRequestComment(string Author, string Body, Uri Url, DateTimeOffset CreatedAt);
