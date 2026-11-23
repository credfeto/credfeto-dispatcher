using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Services;
using FunFair.Test.Common;
using FunFair.Test.Infrastructure.Mocks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class WorkItemRefresherTests : TestBase
{
    private const string REPO = "owner/repo";

    private static readonly DateTimeOffset Now = MockDateTimeSources.Past.GetUtcNow();

    private readonly INotificationStateTracker _notificationStateTracker;
    private readonly PullRequestCheckTracker _tracker;

    public WorkItemRefresherTests()
    {
        this._notificationStateTracker = GetSubstitute<INotificationStateTracker>();
        this._tracker = new PullRequestCheckTracker(MockDateTimeSources.Past);
    }

    private static GitHubOptions DependabotAdoptionOptions()
    {
        return new GitHubOptions
        {
            Filter = new GitHubFilterOptions
            {
                LabelFilter = ["AI-Work"],
                NoWorkFilter = ["blocked"],
                PullRequests = new GitHubPullRequestFilterOptions
                {
                    AdoptionRules =
                    [
                        new BotPrRule
                        {
                            Author = "dependabot[bot]",
                            BranchPrefix = "dependabot/",
                            DefaultPriority = WorkPriority.MEDIUM,
                            TimeoutHours = 24,
                            Priority = WorkPriority.SECURITY,
                        },
                    ],
                },
            },
        };
    }

    private WorkItemRefresher CreateRefresher(PullRequestDetails? details, GitHubOptions options)
    {
        return new WorkItemRefresher(
            pullRequestDetailFetcher: new FixedPullRequestDetailFetcher(details),
            issueDetailFetcher: GetSubstitute<IIssueDetailFetcher>(),
            notificationStateTracker: this._notificationStateTracker,
            checkTracker: this._tracker,
            options: Options.Create(options),
            timeProvider: MockDateTimeSources.Past,
            logger: this.GetTypedLogger<WorkItemRefresher>()
        );
    }

    private static PullRequestDetails BuildDetails(
        IReadOnlyList<string> labels,
        string author = "dnyw4l3n13",
        string headRef = "feature/x",
        DateTimeOffset? createdAt = null
    )
    {
        return new PullRequestDetails(
            Number: 7,
            Title: "PR",
            Status: "Open",
            HtmlUrl: new Uri("https://github.com/owner/repo/pull/7"),
            Assignees: [],
            Labels: labels,
            Body: null,
            Comments: [],
            Reviews: [],
            Runs:
            [
                new PullRequestRun(
                    Name: "build",
                    Status: "in_progress",
                    Conclusion: null,
                    Url: new Uri("https://github.com/owner/repo/actions/runs/1"),
                    IsRequired: true,
                    HeadSha: "sha7"
                ),
            ],
            LinkedItems: [],
            Repository: new ItemRepository(Owner: "owner", Name: "repo", Url: new Uri("https://github.com/owner/repo")),
            LastNotification: new LastNotification(Id: "n", Timestamp: DateTimeOffset.MinValue),
            Author: author,
            CommitAuthors: [],
            HasDetail: true,
            HeadSha: "sha7",
            HeadRef: headRef,
            CreatedAt: createdAt ?? Now.AddHours(-1)
        );
    }

    [Fact]
    public async Task RefreshPullRequestAsync_WithLabelPassingPullRequest_UpsertsAndTracksItsChecksAsync()
    {
        WorkItemRefresher refresher = this.CreateRefresher(
            details: BuildDetails(labels: ["AI-Work", "High"]),
            options: DependabotAdoptionOptions()
        );

        bool refreshed = await refresher.RefreshPullRequestAsync(
            repo: REPO,
            number: 7,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(refreshed, userMessage: "Expected the PR to be refreshed");
        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Is<GitHubNotification>(n => n.Repository.FullName == REPO),
                details: Arg.Is<PullRequestDetails>(d => d.Number == 7 && d.HasDetail),
                priority: WorkPriority.HIGH,
                isOnHold: false,
                isAdopted: false,
                cancellationToken: Arg.Any<CancellationToken>()
            );

        PullRequestCheckState? state = this._tracker.Get(repo: REPO, number: 7);
        Assert.NotNull(state);
        Assert.Equal(expected: "sha7", actual: state.HeadSha);
        Assert.False(state.Settled, userMessage: "In-progress checks are not settled");
    }

    [Fact]
    public async Task RefreshPullRequestAsync_WithAdoptedBotPullRequest_UsesTheAdoptionPriorityAsync()
    {
        WorkItemRefresher refresher = this.CreateRefresher(
            details: BuildDetails(labels: ["dependencies"], author: "dependabot[bot]", headRef: "dependabot/nuget/x"),
            options: DependabotAdoptionOptions()
        );

        bool refreshed = await refresher.RefreshPullRequestAsync(
            repo: REPO,
            number: 7,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(refreshed, userMessage: "Expected the adopted PR to be refreshed");
        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Any<PullRequestDetails>(),
                priority: WorkPriority.MEDIUM,
                isOnHold: false,
                isAdopted: true,
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task RefreshPullRequestAsync_WithTimedOutAdoptedBotPullRequest_UsesTheEscalatedPriorityAsync()
    {
        WorkItemRefresher refresher = this.CreateRefresher(
            details: BuildDetails(
                labels: [],
                author: "dependabot[bot]",
                headRef: "dependabot/nuget/x",
                createdAt: Now.AddHours(-30)
            ),
            options: DependabotAdoptionOptions()
        );

        await refresher.RefreshPullRequestAsync(repo: REPO, number: 7, cancellationToken: this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Any<PullRequestDetails>(),
                priority: WorkPriority.SECURITY,
                isOnHold: false,
                isAdopted: true,
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task RefreshPullRequestAsync_WithIneligiblePullRequest_DoesNotUpsertAndStopsTrackingItAsync()
    {
        this._tracker.Observe(repo: REPO, number: 7, headSha: "sha7", isOnHold: false, isOpen: true);
        WorkItemRefresher refresher = this.CreateRefresher(
            details: BuildDetails(labels: ["bug"]),
            options: DependabotAdoptionOptions()
        );

        bool refreshed = await refresher.RefreshPullRequestAsync(
            repo: REPO,
            number: 7,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(refreshed, userMessage: "An ineligible PR is not refreshed");
        Assert.Empty(this._notificationStateTracker.ReceivedCalls());
        Assert.Null(this._tracker.Get(repo: REPO, number: 7));
    }

    [Fact]
    public async Task RefreshPullRequestAsync_WhenTheFetchFails_ReturnsFalseAsync()
    {
        WorkItemRefresher refresher = this.CreateRefresher(details: null, options: DependabotAdoptionOptions());

        bool refreshed = await refresher.RefreshPullRequestAsync(
            repo: REPO,
            number: 7,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(refreshed, userMessage: "A failed fetch is not a refresh");
        Assert.Empty(this._notificationStateTracker.ReceivedCalls());
    }

    [Fact]
    public async Task RefreshPullRequestAsync_WithOnHoldPullRequest_UpsertsItOnHoldAndKeepsItOutOfTheCheckRefreshAsync()
    {
        WorkItemRefresher refresher = this.CreateRefresher(
            details: BuildDetails(labels: ["AI-Work", "Blocked"]),
            options: DependabotAdoptionOptions()
        );

        await refresher.RefreshPullRequestAsync(repo: REPO, number: 7, cancellationToken: this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Any<PullRequestDetails>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: true,
                isAdopted: Arg.Any<bool?>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
        Assert.Empty(this._tracker.SelectCandidates(5));
    }

    private sealed class FixedPullRequestDetailFetcher : IPullRequestDetailFetcher
    {
        private readonly PullRequestDetails? _details;

        public FixedPullRequestDetailFetcher(PullRequestDetails? details)
        {
            this._details = details;
        }

        public ValueTask<PullRequestDetails?> FetchAsync(
            GitHubNotification notification,
            CancellationToken cancellationToken
        )
        {
            return ValueTask.FromResult(this._details);
        }
    }
}
