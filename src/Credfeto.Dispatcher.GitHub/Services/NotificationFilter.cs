using System;
using System.Linq;
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

        return this._options.Filter.Reasons.Any(reason => string.Equals(a: notification.Reason, b: reason, comparisonType: StringComparison.OrdinalIgnoreCase));
    }

    private bool PassesOwnerFilter(GitHubNotification notification)
    {
        if (this._options.Filter.AllowedOwners.Count == 0)
        {
            return true;
        }

        string repoOwner = GetOwner(notification.Repository.FullName);

        return this._options.Filter.AllowedOwners.Any(owner => string.Equals(a: repoOwner, b: owner, comparisonType: StringComparison.OrdinalIgnoreCase));
    }

    private bool PassesExcludedRepoFilter(GitHubNotification notification)
    {
        if (this._options.Filter.ExcludedRepos.Count == 0)
        {
            return true;
        }

        return !this._options.Filter.ExcludedRepos.Any(excluded => string.Equals(a: notification.Repository.FullName, b: excluded, comparisonType: StringComparison.OrdinalIgnoreCase));
    }

    private static string GetOwner(string fullName)
    {
        int slashIndex = fullName.IndexOf(value: '/', comparisonType: StringComparison.Ordinal);

        return slashIndex < 0
            ? fullName
            : fullName[..slashIndex];
    }
}
