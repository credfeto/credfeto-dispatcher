using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.Configuration;

[DebuggerDisplay("DelaySeconds: {DelaySeconds}")]
public sealed class NotificationQueueOptions
{
    public int DelaySeconds { get; set; } = 300;
}
