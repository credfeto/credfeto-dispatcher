using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("Count: {Notifications.Count}, CandidateETag: {CandidateETag}")]
public sealed record NotificationPollResult(IReadOnlyList<GitHubNotification> Notifications, string? CandidateETag);
