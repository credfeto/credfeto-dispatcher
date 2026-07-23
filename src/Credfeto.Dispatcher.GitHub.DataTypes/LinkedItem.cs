using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{Number}")]
public sealed record LinkedItem(int Number, IReadOnlyList<string> Labels, IReadOnlyList<string> Assignees);
