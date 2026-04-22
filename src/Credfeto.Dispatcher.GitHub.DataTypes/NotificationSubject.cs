using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{Type}: {Title}")]
public sealed record NotificationSubject(string Title, Uri Url, string Type);
