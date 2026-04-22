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

    private static INotificationFilter BuildFilter(GitHubOptions options)
    {
        return new NotificationFilter(Options.Create(options));
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
        INotificationFilter filter = BuildFilter(new GitHubOptions());
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
        INotificationFilter filter = BuildFilter(options);
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
        INotificationFilter filter = BuildFilter(options);
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
        INotificationFilter filter = BuildFilter(options);
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
        INotificationFilter filter = BuildFilter(options);
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
        INotificationFilter filter = BuildFilter(options);
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
        INotificationFilter filter = BuildFilter(options);
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
        INotificationFilter filter = BuildFilter(options);
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
        INotificationFilter filter = BuildFilter(options);
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
        INotificationFilter filter = BuildFilter(options);
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
        INotificationFilter filter = BuildFilter(options);

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
        INotificationFilter filter = BuildFilter(options);
        GitHubNotification notification = BuildNotification(reason: reason);

        bool result = filter.ShouldDispatch(notification);

        Assert.True(result, userMessage: $"Expected notification with reason '{reason}' to be dispatched");
    }

    [Theory]
    [InlineData("subscribed")]
    [InlineData("team_mention")]
    [InlineData("author")]
    public void ShouldDispatchReturnsFalseWhenReasonIsNotInAllowedList(string reason)
    {
        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { Reasons = ["mention", "review_requested", "assign"] }
        };
        INotificationFilter filter = BuildFilter(options);
        GitHubNotification notification = BuildNotification(reason: reason);

        bool result = filter.ShouldDispatch(notification);

        Assert.False(result, userMessage: $"Expected notification with reason '{reason}' to not be dispatched");
    }
}
