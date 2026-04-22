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
        if (!this.PassesReasonFilter(notification))
        {
            return false;
        }

        if (!this.PassesOwnerFilter(notification))
        {
            return false;
        }

        if (!this.PassesExcludedRepoFilter(notification))
        {
            return false;
        }

        return true;
    }

    private bool PassesReasonFilter(GitHubNotification notification)
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

    private bool PassesOwnerFilter(GitHubNotification notification)
    {
        if (this._options.Filter.AllowedOwners.Count == 0)
        {
            return true;
        }

        string repoOwner = GetOwner(notification.Repository.FullName);

        foreach (string owner in this._options.Filter.AllowedOwners)
        {
            if (string.Equals(a: repoOwner, b: owner, comparisonType: System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool PassesExcludedRepoFilter(GitHubNotification notification)
    {
        if (this._options.Filter.ExcludedRepos.Count == 0)
        {
            return true;
        }

        foreach (string excluded in this._options.Filter.ExcludedRepos)
        {
            if (string.Equals(a: notification.Repository.FullName, b: excluded, comparisonType: System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetOwner(string fullName)
    {
        int slashIndex = fullName.IndexOf(value: '/', comparisonType: System.StringComparison.Ordinal);

        return slashIndex < 0
            ? fullName
            : fullName[..slashIndex];
    }
}
