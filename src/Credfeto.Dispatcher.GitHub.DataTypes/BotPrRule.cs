using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("Author={Author} BranchPrefix={BranchPrefix} TimeoutHours={TimeoutHours} Priority={Priority}")]
public sealed class BotPrRule
{
    public string Author { get; set; } = string.Empty;

    public string BranchPrefix { get; set; } = string.Empty;

    public int TimeoutHours { get; set; } = 24;

    public WorkPriority Priority { get; set; } = WorkPriority.SECURITY;
}
