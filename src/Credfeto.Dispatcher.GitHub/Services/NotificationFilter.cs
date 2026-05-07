using System;
using System.Linq;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub.Services;

public sealed class NotificationFilter : INotificationFilter
{
    private readonly ILogger<NotificationFilter> _logger;
    private readonly GitHubOptions _options;

    public NotificationFilter(IOptions<GitHubOptions> options, ILogger<NotificationFilter> logger)
    {
        this._options = options.Value;
        this._logger = logger;
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

        if (!this.PassesAllowedRepoFilter(notification))
        {
            return false;
        }

        if (!this.PassesExcludedRepoFilter(notification))
        {
            return false;
        }

        this._logger.LogNotificationPassed(
            notificationId: notification.Id,
            reason: notification.Reason,
            repository: notification.Repository.FullName
        );

        return true;
    }

    private bool PassesReasonFilter(GitHubNotification notification)
    {
        if (this._options.Filter.Reasons.Count == 0)
        {
            return true;
        }

        bool passes = this._options.Filter.Reasons.Any(reason =>
            string.Equals(
                a: notification.Reason,
                b: reason,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        );

        if (!passes)
        {
            this._logger.LogNotificationDroppedReason(
                notificationId: notification.Id,
                reason: notification.Reason
            );
        }

        return passes;
    }

    private bool PassesOwnerFilter(GitHubNotification notification)
    {
        if (this._options.Filter.AllowedOwners.Count == 0)
        {
            return true;
        }

        string repoOwner = GetOwner(notification.Repository.FullName);

        bool passes = this._options.Filter.AllowedOwners.Any(owner =>
            string.Equals(
                a: repoOwner,
                b: owner,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        );

        if (!passes)
        {
            this._logger.LogNotificationDroppedOwner(
                notificationId: notification.Id,
                owner: repoOwner
            );
        }

        return passes;
    }

    private bool PassesAllowedRepoFilter(GitHubNotification notification)
    {
        if (this._options.Filter.AllowedRepos.Count == 0)
        {
            return true;
        }

        bool passes = this._options.Filter.AllowedRepos.Any(repo =>
            string.Equals(
                a: notification.Repository.FullName,
                b: repo,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        );

        if (!passes)
        {
            this._logger.LogNotificationDroppedAllowedRepo(
                notificationId: notification.Id,
                repository: notification.Repository.FullName
            );
        }

        return passes;
    }

    private bool PassesExcludedRepoFilter(GitHubNotification notification)
    {
        if (this._options.Filter.ExcludedRepos.Count == 0)
        {
            return true;
        }

        bool passes = !this._options.Filter.ExcludedRepos.Any(excluded =>
            string.Equals(
                a: notification.Repository.FullName,
                b: excluded,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        );

        if (!passes)
        {
            this._logger.LogNotificationDroppedExcludedRepo(
                notificationId: notification.Id,
                repository: notification.Repository.FullName
            );
        }

        return passes;
    }

    private static string GetOwner(string fullName)
    {
        int slashIndex = fullName.IndexOf(value: '/', comparisonType: StringComparison.Ordinal);

        return slashIndex < 0 ? fullName : fullName[..slashIndex];
    }
}
