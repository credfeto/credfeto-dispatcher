using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("#{Number}: {Title} ({State})")]
public sealed record LinkedItem(int Number, string Title, string State, Uri Url);
