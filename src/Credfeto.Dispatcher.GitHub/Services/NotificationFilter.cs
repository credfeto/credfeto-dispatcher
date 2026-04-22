using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub.Services;

public sealed class NotificationFilter : INotificationFilter
{
    private readonly GitHubOptions _options;

    public NotificationFilter(IOptions<GitHubOptions> options)
    {
        this._options = options.Value;
    }

    public bool ShouldDispatch(GitHubNotification notification)
    {
        if (this._options.Filter.Reasons.Count == 0)
        {
            return true;
        }

        foreach (string reason in this._options.Filter.Reasons)
        {
            if (string.Equals(a: notification.Reason, b: reason, comparisonType: System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
