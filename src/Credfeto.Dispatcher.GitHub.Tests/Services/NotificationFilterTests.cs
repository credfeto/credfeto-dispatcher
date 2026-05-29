using System;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Services;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class NotificationFilterTests : TestBase
{
    private static readonly NotificationSubject DefaultSubject = new(
        Title: "Test",
        Url: new Uri("https://api.github.com/repos/owner/repo/pulls/1"),
        Type: "PullRequest"
    );

    private INotificationFilter BuildFilter(GitHubOptions options)
    {
        return new NotificationFilter(
            options: Options.Create(options),
            logger: this.GetTypedLogger<NotificationFilter>()
        );
    }

    private static GitHubNotification BuildNotification(string reason = "mention", string repoFullName = "owner/repo")
    {
        NotificationRepository repository = new(FullName: repoFullName, Url: new Uri("https://github.com/owner/repo"));

        return new GitHubNotification(
            Id: "1",
            Reason: reason,
            Subject: DefaultSubject,
            Repository: repository,
            UpdatedAt: TimeSources.Past.UtcNowAsOffset,
            Unread: true
        );
    }

    [Fact]
    public void ShouldProcessReturnsTrueWhenNoFiltersConfigured()
    {
        INotificationFilter filter = this.BuildFilter(new GitHubOptions());
        GitHubNotification notification = BuildNotification();

        bool result = filter.ShouldProcess(notification);

        Assert.True(result, userMessage: "Expected notification to be processed when no filters are configured");
    }

    [Fact]
    public void ShouldProcessReturnsTrueWhenAllowedOwnerMatchesRepoOwner()
    {
        GitHubOptions options = new() { Filter = new GitHubFilterOptions { AllowedOwners = ["owner"] } };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "owner/repo");

        bool result = filter.ShouldProcess(notification);

        Assert.True(result, userMessage: "Expected notification to be processed when owner is in allowed list");
    }

    [Fact]
    public void ShouldProcessReturnsFalseWhenOwnerNotInAllowedList()
    {
        GitHubOptions options = new() { Filter = new GitHubFilterOptions { AllowedOwners = ["allowed-owner"] } };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "other-owner/repo");

        bool result = filter.ShouldProcess(notification);

        Assert.False(
            result,
            userMessage: "Expected notification to not be processed when owner is not in allowed list"
        );
    }

    [Fact]
    public void ShouldProcessReturnsTrueWhenOwnerMatchesCaseInsensitively()
    {
        GitHubOptions options = new() { Filter = new GitHubFilterOptions { AllowedOwners = ["OWNER"] } };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "owner/repo");

        bool result = filter.ShouldProcess(notification);

        Assert.True(result, userMessage: "Expected owner filter to be case-insensitive");
    }

    [Fact]
    public void ShouldProcessReturnsTrueWhenRepoNotExcluded()
    {
        GitHubOptions options = new() { Filter = new GitHubFilterOptions { ExcludedRepos = ["owner/other-repo"] } };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "owner/repo");

        bool result = filter.ShouldProcess(notification);

        Assert.True(result, userMessage: "Expected notification to be processed when repo is not excluded");
    }

    [Fact]
    public void ShouldProcessReturnsFalseWhenRepoIsExcluded()
    {
        GitHubOptions options = new() { Filter = new GitHubFilterOptions { ExcludedRepos = ["owner/repo"] } };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "owner/repo");

        bool result = filter.ShouldProcess(notification);

        Assert.False(result, userMessage: "Expected notification to not be processed when repo is excluded");
    }

    [Fact]
    public void ShouldProcessReturnsFalseWhenExcludedRepoMatchesCaseInsensitively()
    {
        GitHubOptions options = new() { Filter = new GitHubFilterOptions { ExcludedRepos = ["OWNER/REPO"] } };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "owner/repo");

        bool result = filter.ShouldProcess(notification);

        Assert.False(result, userMessage: "Expected excluded repo filter to be case-insensitive");
    }

    [Fact]
    public void ShouldProcessHandlesRepoFullNameWithNoSlash()
    {
        GitHubOptions options = new() { Filter = new GitHubFilterOptions { AllowedOwners = ["owner"] } };
        INotificationFilter filter = this.BuildFilter(options);

        NotificationRepository repository = new(FullName: "owner", Url: new Uri("https://github.com/owner"));
        GitHubNotification notification = new(
            Id: "1",
            Reason: "mention",
            Subject: DefaultSubject,
            Repository: repository,
            UpdatedAt: TimeSources.Past.UtcNowAsOffset,
            Unread: true
        );

        bool result = filter.ShouldProcess(notification);

        Assert.True(result, userMessage: "Expected owner filter to work when repo full name has no slash");
    }

    [Fact]
    public void ShouldProcessReturnsTrueWhenAllowedReposIsEmpty()
    {
        INotificationFilter filter = this.BuildFilter(new GitHubOptions());
        GitHubNotification notification = BuildNotification(repoFullName: "owner/any-repo");

        bool result = filter.ShouldProcess(notification);

        Assert.True(result, userMessage: "Expected notification to be processed when allowed repos list is empty");
    }

    [Fact]
    public void ShouldProcessReturnsTrueWhenRepoIsInAllowedList()
    {
        GitHubOptions options = new() { Filter = new GitHubFilterOptions { AllowedRepos = ["owner/repo"] } };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "owner/repo");

        bool result = filter.ShouldProcess(notification);

        Assert.True(result, userMessage: "Expected notification to be processed when repo is in allowed list");
    }

    [Fact]
    public void ShouldProcessReturnsFalseWhenRepoNotInAllowedList()
    {
        GitHubOptions options = new() { Filter = new GitHubFilterOptions { AllowedRepos = ["owner/repo"] } };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "owner/other-repo");

        bool result = filter.ShouldProcess(notification);

        Assert.False(result, userMessage: "Expected notification to not be processed when repo is not in allowed list");
    }

    [Fact]
    public void ShouldProcessReturnsTrueWhenAllowedRepoMatchesCaseInsensitively()
    {
        GitHubOptions options = new() { Filter = new GitHubFilterOptions { AllowedRepos = ["OWNER/REPO"] } };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "owner/repo");

        bool result = filter.ShouldProcess(notification);

        Assert.True(result, userMessage: "Expected allowed repo filter to be case-insensitive");
    }

    [Fact]
    public void ShouldProcessHandlesMultipleAllowedRepos()
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { AllowedRepos = ["owner/repo-a", "owner/repo-b"] },
        };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notificationA = BuildNotification(repoFullName: "owner/repo-a");
        GitHubNotification notificationB = BuildNotification(repoFullName: "owner/repo-b");
        GitHubNotification notificationC = BuildNotification(repoFullName: "owner/repo-c");

        Assert.True(filter.ShouldProcess(notificationA), userMessage: "Expected repo-a to be processed");
        Assert.True(filter.ShouldProcess(notificationB), userMessage: "Expected repo-b to be processed");
        Assert.False(filter.ShouldProcess(notificationC), userMessage: "Expected repo-c to not be processed");
    }
}
