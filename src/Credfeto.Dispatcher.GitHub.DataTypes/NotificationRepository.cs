using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{FullName}: {Url}")]
public sealed record NotificationRepository(string FullName, Uri Url);
