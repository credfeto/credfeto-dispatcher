namespace Credfeto.Dispatcher.GitHub.Services;

internal enum CheckOutcomeKind
{
    UNCHANGED,
    REFRESHED,
    FAILED,
    RATE_LIMITED,
}
