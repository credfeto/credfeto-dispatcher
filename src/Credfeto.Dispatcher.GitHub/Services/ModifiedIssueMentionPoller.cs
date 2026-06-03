using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Models;
using Credfeto.Dispatcher.GitHub.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.GitHub.Services;

public sealed class ModifiedIssueMentionPoller : IModifiedIssueMentionPoller
{
    private readonly IETagStore _eTagStore;
    private readonly GitHubRepoHelper _helper;
    private readonly ILogger<ModifiedIssueMentionPoller> _logger;
    private readonly GitHubOptions _options;
    private readonly TimeProvider _timeProvider;

    public ModifiedIssueMentionPoller(
        GitHubRepoHelper helper,
        IETagStore eTagStore,
        TimeProvider timeProvider,
        IOptions<GitHubOptions> options,
        ILogger<ModifiedIssueMentionPoller> logger
    )
    {
        this._helper = helper;
        this._eTagStore = eTagStore;
        this._timeProvider = timeProvider;
        this._options = options.Value;
        this._logger = logger;
    }

    public async ValueTask<IReadOnlyList<GitHubNotification>> PollAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(this._options.Filter.MentionedUser))
        {
            return [];
        }

        (_, IReadOnlyList<string> repos, _) = await this._helper.DiscoverReposAsync(
            shouldInclude: this.ShouldIncludeRepo,
            cancellationToken: cancellationToken
        );

        if (repos.Count == 0)
        {
            return [];
        }

        List<GitHubNotification> notifications = [];

        foreach (string repo in repos)
        {
            IReadOnlyList<GitHubNotification> repoNotifications = await this.PollRepoAsync(
                repo: repo,
                cancellationToken: cancellationToken
            );
            notifications.AddRange(repoNotifications);
        }

        this._logger.LogPollComplete(count: notifications.Count);

        return notifications;
    }

    private async Task<IReadOnlyList<GitHubNotification>> PollRepoAsync(
        string repo,
        CancellationToken cancellationToken
    )
    {
        string sinceKey = $"issues.mentions.since:{repo}";
        string? since = await this._eTagStore.GetETagAsync(key: sinceKey, cancellationToken: cancellationToken);

        DateTimeOffset pollStart = this._timeProvider.GetUtcNow();
        this._logger.LogPollingRepoSince(repo: repo, since: since ?? "(all time)");

        string sinceParam = since is not null ? $"&since={Uri.EscapeDataString(since)}" : string.Empty;
        string? url = $"repos/{repo}/issues?state=open&per_page=100{sinceParam}";
        string mentionTarget = $"@{this._options.Filter.MentionedUser}";
        List<GitHubNotification> notifications = [];

        while (url is not null)
        {
            (ApiIssue[]? items, string? nextUrl) = await this._helper.GetPagedAsync(
                url: url,
                jsonTypeInfo: NotificationSerializerContext.Default.ApiIssueArray,
                cancellationToken: cancellationToken
            );

            if (items is null)
            {
                break;
            }

            foreach (ApiIssue issue in items)
            {
                if (issue.PullRequest is not null)
                {
                    continue;
                }

                if (issue.HtmlUrl is not { } issueHtmlUrl)
                {
                    continue;
                }

                if (ContainsMention(text: issue.Body, mention: mentionTarget))
                {
                    this._logger.LogFoundMentionInIssue(repo: repo, number: issue.Number);
                    notifications.Add(
                        BuildMentionNotification(repo: repo, issue: issue, issueUri: new Uri(issueHtmlUrl))
                    );
                }
            }

            url = nextUrl;
        }

        await this._eTagStore.SaveETagAsync(
            key: sinceKey,
            eTag: pollStart.ToString("O"),
            cancellationToken: cancellationToken
        );

        return notifications;
    }

    private bool ShouldIncludeRepo(ApiUserRepo repo)
    {
        if (repo.Archived || repo.Disabled)
        {
            return false;
        }

        if (repo.Permissions?.Push != true)
        {
            return false;
        }

        return this.PassesRepoFilter(repo.FullName);
    }

    [SuppressMessage(
        "Philips.CodeAnalysis.DuplicateCodeAnalyzer",
        "PH2071:Duplicate shape found",
        Justification = "Structurally identical filter pattern shared with WorkItemScanner and RepoEventPoller."
    )]
    private bool PassesRepoFilter(string fullName)
    {
        if (this._options.Filter.AllowedOwners.Count > 0)
        {
            string owner = GetOwner(fullName);

            if (
                !this._options.Filter.AllowedOwners.Any(o =>
                    string.Equals(a: o, b: owner, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return false;
            }
        }

        if (this._options.Filter.ExcludedRepos.Count > 0)
        {
            if (
                this._options.Filter.ExcludedRepos.Any(r =>
                    string.Equals(a: r, b: fullName, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return false;
            }
        }

        return true;
    }

    private static string GetOwner(string fullName)
    {
        int slash = fullName.IndexOf(value: '/', comparisonType: StringComparison.Ordinal);

        return slash >= 0 ? fullName[..slash] : fullName;
    }

    private static bool ContainsMention(string? text, string mention)
    {
        return text is not null && text.Contains(value: mention, comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static GitHubNotification BuildMentionNotification(string repo, ApiIssue issue, Uri issueUri)
    {
        Uri repoUri = new($"https://github.com/{repo}");

        return new GitHubNotification(
            Id: $"modified-mention:{repo}:issue:{issue.Number}:{issue.UpdatedAt.ToUnixTimeSeconds()}",
            Reason: "mention",
            Subject: new NotificationSubject(Title: issue.Title, Url: issueUri, Type: "Issue"),
            Repository: new NotificationRepository(FullName: repo, Url: repoUri),
            UpdatedAt: issue.UpdatedAt,
            Unread: true
        );
    }
}
