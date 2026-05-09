using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{Id}: {Timestamp}")]
public sealed record LastNotification(string Id, DateTimeOffset Timestamp);
