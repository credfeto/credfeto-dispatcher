using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Credfeto.Dispatcher.GitHub.Services;

[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay("{Kind}, PollIntervalSeconds: {PollIntervalSeconds}")]
internal readonly record struct CheckOutcome(CheckOutcomeKind Kind, int? PollIntervalSeconds);
