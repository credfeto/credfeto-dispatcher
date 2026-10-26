namespace Credfeto.Dispatcher.Server;

internal static class WorkItemQueries
{
    public static string? NormaliseFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
