using System;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Services;
using FunFair.Test.Common;
using FunFair.Test.Common.Helpers;
using Microsoft.Extensions.Options;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class NotificationFilterTests : TestBase
{
    private static readonly NotificationSubject DefaultSubject = new(Title: "Test", Url: new Uri("https://api.github.com/repos/owner/repo/pulls/1"), Type: "PullRequest");

    private INotificationFilter BuildFilter(GitHubOptions options)
    {
        return new NotificationFilter(options: Options.Create(options), logger: this.GetTypedLogger<NotificationFilter>());
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
    public void ShouldDispatchReturnsTrueWhenNoFiltersConfigured()
    {
        INotificationFilter filter = this.BuildFilter(new GitHubOptions());
        GitHubNotification notification = BuildNotification();

        bool result = filter.ShouldDispatch(notification);

        Assert.True(result, userMessage: "Expected notification to be dispatched when no filters are configured");
    }

    [Fact]
    public void ShouldDispatchReturnsTrueWhenReasonMatchesFilter()
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { Reasons = ["mention"] }
        };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(reason: "mention");

        bool result = filter.ShouldDispatch(notification);

        Assert.True(result, userMessage: "Expected notification to be dispatched when reason matches filter");
    }

    [Fact]
    public void ShouldDispatchReturnsFalseWhenReasonDoesNotMatchFilter()
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { Reasons = ["mention"] }
        };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(reason: "subscribed");

        bool result = filter.ShouldDispatch(notification);

        Assert.False(result, userMessage: "Expected notification to not be dispatched when reason does not match filter");
    }

    [Fact]
    public void ShouldDispatchReturnsTrueWhenReasonMatchesCaseInsensitively()
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { Reasons = ["MENTION"] }
        };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(reason: "mention");

        bool result = filter.ShouldDispatch(notification);

        Assert.True(result, userMessage: "Expected reason filter to be case-insensitive");
    }

    [Fact]
    public void ShouldDispatchReturnsTrueWhenAllowedOwnerMatchesRepoOwner()
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { AllowedOwners = ["owner"] }
        };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "owner/repo");

        bool result = filter.ShouldDispatch(notification);

        Assert.True(result, userMessage: "Expected notification to be dispatched when owner is in allowed list");
    }

    [Fact]
    public void ShouldDispatchReturnsFalseWhenOwnerNotInAllowedList()
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { AllowedOwners = ["allowed-owner"] }
        };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "other-owner/repo");

        bool result = filter.ShouldDispatch(notification);

        Assert.False(result, userMessage: "Expected notification to not be dispatched when owner is not in allowed list");
    }

    [Fact]
    public void ShouldDispatchReturnsTrueWhenOwnerMatchesCaseInsensitively()
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { AllowedOwners = ["OWNER"] }
        };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "owner/repo");

        bool result = filter.ShouldDispatch(notification);

        Assert.True(result, userMessage: "Expected owner filter to be case-insensitive");
    }

    [Fact]
    public void ShouldDispatchReturnsTrueWhenRepoNotExcluded()
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { ExcludedRepos = ["owner/other-repo"] }
        };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "owner/repo");

        bool result = filter.ShouldDispatch(notification);

        Assert.True(result, userMessage: "Expected notification to be dispatched when repo is not excluded");
    }

    [Fact]
    public void ShouldDispatchReturnsFalseWhenRepoIsExcluded()
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { ExcludedRepos = ["owner/repo"] }
        };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "owner/repo");

        bool result = filter.ShouldDispatch(notification);

        Assert.False(result, userMessage: "Expected notification to not be dispatched when repo is excluded");
    }

    [Fact]
    public void ShouldDispatchReturnsFalseWhenExcludedRepoMatchesCaseInsensitively()
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { ExcludedRepos = ["OWNER/REPO"] }
        };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "owner/repo");

        bool result = filter.ShouldDispatch(notification);

        Assert.False(result, userMessage: "Expected excluded repo filter to be case-insensitive");
    }

    [Fact]
    public void ShouldDispatchHandlesRepoFullNameWithNoSlash()
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { AllowedOwners = ["owner"] }
        };
        INotificationFilter filter = this.BuildFilter(options);

        NotificationRepository repository = new(FullName: "owner", Url: new Uri("https://github.com/owner"));
        GitHubNotification notification = new(Id: "1", Reason: "mention", Subject: DefaultSubject, Repository: repository, UpdatedAt: TimeSources.Past.UtcNowAsOffset, Unread: true);

        bool result = filter.ShouldDispatch(notification);

        Assert.True(result, userMessage: "Expected owner filter to work when repo full name has no slash");
    }

    [Theory]
    [InlineData("mention")]
    [InlineData("review_requested")]
    [InlineData("assign")]
    public void ShouldDispatchReturnsTrueWhenReasonIsInAllowedList(string reason)
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { Reasons = ["mention", "review_requested", "assign"] }
        };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(reason: reason);

        bool result = filter.ShouldDispatch(notification);

        Assert.True(result, userMessage: $"Expected notification with reason '{reason}' to be dispatched");
    }

    [Fact]
    public void ShouldDispatchReturnsTrueWhenAllowedReposIsEmpty()
    {
        INotificationFilter filter = this.BuildFilter(new GitHubOptions());
        GitHubNotification notification = BuildNotification(repoFullName: "owner/any-repo");

        bool result = filter.ShouldDispatch(notification);

        Assert.True(result, userMessage: "Expected notification to be dispatched when allowed repos list is empty");
    }

    [Fact]
    public void ShouldDispatchReturnsTrueWhenRepoIsInAllowedList()
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { AllowedRepos = ["owner/repo"] }
        };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "owner/repo");

        bool result = filter.ShouldDispatch(notification);

        Assert.True(result, userMessage: "Expected notification to be dispatched when repo is in allowed list");
    }

    [Fact]
    public void ShouldDispatchReturnsFalseWhenRepoNotInAllowedList()
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { AllowedRepos = ["owner/repo"] }
        };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "owner/other-repo");

        bool result = filter.ShouldDispatch(notification);

        Assert.False(result, userMessage: "Expected notification to not be dispatched when repo is not in allowed list");
    }

    [Fact]
    public void ShouldDispatchReturnsTrueWhenAllowedRepoMatchesCaseInsensitively()
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { AllowedRepos = ["OWNER/REPO"] }
        };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notification = BuildNotification(repoFullName: "owner/repo");

        bool result = filter.ShouldDispatch(notification);

        Assert.True(result, userMessage: "Expected allowed repo filter to be case-insensitive");
    }

    [Fact]
    public void ShouldDispatchHandlesMultipleAllowedRepos()
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { AllowedRepos = ["owner/repo-a", "owner/repo-b"] }
        };
        INotificationFilter filter = this.BuildFilter(options);
        GitHubNotification notificationA = BuildNotification(repoFullName: "owner/repo-a");
        GitHubNotification notificationB = BuildNotification(repoFullName: "owner/repo-b");
        GitHubNotification notificationC = BuildNotification(repoFullName: "owner/repo-c");

        Assert.True(filter.ShouldDispatch(notificationA), userMessage: "Expected repo-a to be dispatched");
        Assert.True(filter.ShouldDispatch(notificationB), userMessage: "Expected repo-b to be dispatched");
        Assert.False(filter.ShouldDispatch(notificationC), userMessage: "Expected repo-c to not be dispatched");
    }
}

