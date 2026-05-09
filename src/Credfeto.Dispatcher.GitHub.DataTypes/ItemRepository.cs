using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{Owner}/{Name}")]
public sealed record ItemRepository(string Owner, string Name, Uri Url);
