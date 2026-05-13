using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("AsOf: {AsOf}, LagSeconds: {LagSeconds}, Count: {Priorities.Count}")]
public sealed record PrioritiesResponse(IReadOnlyList<WorkItem> Priorities, DateTimeOffset AsOf, long LagSeconds);
