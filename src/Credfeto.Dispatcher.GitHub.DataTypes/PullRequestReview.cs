using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{State}: {Author}")]
public sealed record PullRequestReview(string Author, string State, string? Body, Uri Url, DateTimeOffset SubmittedAt);
