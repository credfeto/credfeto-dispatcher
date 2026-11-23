using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Services;
using FunFair.Test.Common;
using FunFair.Test.Infrastructure.Mocks;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class PullRequestCheckTrackerTests : TestBase
{
    private const string REPO = "owner/repo";

    private readonly PullRequestCheckTracker _tracker = new(MockDateTimeSources.Past);

    private static PullRequestDetails BuildDetails(
        int number,
        string? headSha,
        IReadOnlyList<PullRequestRun> runs,
        string status = "Open"
    )
    {
        return new PullRequestDetails(
            Number: number,
            Title: "PR",
            Status: status,
            HtmlUrl: new Uri($"https://github.com/owner/repo/pull/{number}"),
            Assignees: [],
            Labels: [],
            Body: null,
            Comments: [],
            Reviews: [],
            Runs: runs,
            LinkedItems: [],
            Repository: new ItemRepository(Owner: "owner", Name: "repo", Url: new Uri("https://github.com/owner/repo")),
            LastNotification: new LastNotification(Id: "n", Timestamp: DateTimeOffset.MinValue),
            Author: "dnyw4l3n13",
            CommitAuthors: [],
            HasDetail: true,
            HeadSha: headSha
        );
    }

    private static PullRequestRun Run(string name, string status, string? conclusion)
    {
        return new PullRequestRun(
            Name: name,
            Status: status,
            Conclusion: conclusion,
            Url: new Uri("https://github.com/owner/repo/actions/runs/1"),
            IsRequired: true,
            HeadSha: "sha1"
        );
    }

    private IReadOnlyList<int> CandidateNumbers(int maxCount = 10)
    {
        return [.. this._tracker.SelectCandidates(maxCount).Select(static c => c.Number)];
    }

    [Fact]
    public void ObservedOpenPullRequestIsACandidate()
    {
        this._tracker.Observe(repo: REPO, number: 1, headSha: "sha1", isOnHold: false, isOpen: true);

        PullRequestCheckState candidate = Assert.Single(this._tracker.SelectCandidates(5));
        Assert.Equal(expected: "sha1", actual: candidate.HeadSha);
        Assert.Null(candidate.RunSignature);
    }

    [Fact]
    public void OnHoldPullRequestIsNotACandidate()
    {
        this._tracker.Observe(repo: REPO, number: 1, headSha: "sha1", isOnHold: true, isOpen: true);

        Assert.Empty(this._tracker.SelectCandidates(5));
    }

    [Fact]
    public void ClosedPullRequestIsForgotten()
    {
        this._tracker.Observe(repo: REPO, number: 1, headSha: "sha1", isOnHold: false, isOpen: true);
        this._tracker.Observe(repo: REPO, number: 1, headSha: "sha1", isOnHold: false, isOpen: false);

        Assert.Equal(expected: 0, actual: this._tracker.Count);
    }

    [Fact]
    public void PassedChecksAreSettledAndNoLongerCandidates()
    {
        this._tracker.RecordDetail(
            repo: REPO,
            number: 1,
            details: BuildDetails(number: 1, headSha: "sha1", runs: [Run("build", "completed", "success")]),
            isOnHold: false
        );

        Assert.Empty(this._tracker.SelectCandidates(5));
    }

    [Theory]
    [InlineData("in_progress", null)]
    [InlineData("queued", null)]
    [InlineData("completed", "failure")]
    [InlineData("completed", "timed_out")]
    public void PendingOrFailedChecksStayCandidates(string status, string? conclusion)
    {
        this._tracker.RecordDetail(
            repo: REPO,
            number: 1,
            details: BuildDetails(number: 1, headSha: "sha1", runs: [Run("build", status, conclusion)]),
            isOnHold: false
        );

        Assert.Single(this._tracker.SelectCandidates(5));
    }

    [Fact]
    public void NoRunsYetStaysACandidate()
    {
        this._tracker.RecordDetail(
            repo: REPO,
            number: 1,
            details: BuildDetails(number: 1, headSha: "sha1", runs: []),
            isOnHold: false
        );

        Assert.Single(this._tracker.SelectCandidates(5));
    }

    [Fact]
    public void NewHeadCommitMakesASettledPullRequestACandidateAgain()
    {
        this._tracker.RecordDetail(
            repo: REPO,
            number: 1,
            details: BuildDetails(number: 1, headSha: "sha1", runs: [Run("build", "completed", "success")]),
            isOnHold: false
        );

        this._tracker.Observe(repo: REPO, number: 1, headSha: "sha2", isOnHold: false, isOpen: true);

        PullRequestCheckState candidate = Assert.Single(this._tracker.SelectCandidates(5));
        Assert.Equal(expected: "sha2", actual: candidate.HeadSha);
        Assert.Null(candidate.ETag);
        Assert.Null(candidate.RunSignature);
    }

    [Fact]
    public void SameHeadCommitKeepsASettledPullRequestSettled()
    {
        this._tracker.RecordDetail(
            repo: REPO,
            number: 1,
            details: BuildDetails(number: 1, headSha: "sha1", runs: [Run("build", "completed", "success")]),
            isOnHold: false
        );

        this._tracker.Observe(repo: REPO, number: 1, headSha: "sha1", isOnHold: false, isOpen: true);

        Assert.Empty(this._tracker.SelectCandidates(5));
    }

    [Fact]
    public void ClosedDetailIsForgotten()
    {
        this._tracker.Observe(repo: REPO, number: 1, headSha: "sha1", isOnHold: false, isOpen: true);

        this._tracker.RecordDetail(
            repo: REPO,
            number: 1,
            details: BuildDetails(number: 1, headSha: "sha1", runs: [], status: "Closed"),
            isOnHold: false
        );

        Assert.Equal(expected: 0, actual: this._tracker.Count);
    }

    [Fact]
    public void SelectionIsBoundedAndNeverCheckedComeFirst()
    {
        this._tracker.RecordDetail(
            repo: REPO,
            number: 1,
            details: BuildDetails(number: 1, headSha: "sha1", runs: [Run("build", "in_progress", null)]),
            isOnHold: false
        );

        for (int number = 2; number <= 6; ++number)
        {
            this._tracker.Observe(
                repo: REPO,
                number: number,
                headSha: "sha" + number.ToString(CultureInfo.InvariantCulture),
                isOnHold: false,
                isOpen: true
            );
        }

        Assert.Equal(expected: [2, 3, 4], actual: this.CandidateNumbers(maxCount: 3));
        Assert.Equal(expected: [2, 3, 4, 5, 6, 1], actual: this.CandidateNumbers());
    }

    [Fact]
    public void ZeroMaximumSelectsNothing()
    {
        this._tracker.Observe(repo: REPO, number: 1, headSha: "sha1", isOnHold: false, isOpen: true);

        Assert.Empty(this._tracker.SelectCandidates(0));
    }

    [Fact]
    public void RecordCheckedKeepsTheETagForTheSameHead()
    {
        this._tracker.Observe(repo: REPO, number: 1, headSha: "sha1", isOnHold: false, isOpen: true);
        PullRequestCheckState candidate = Assert.Single(this._tracker.SelectCandidates(5));

        this._tracker.RecordChecked(checkedState: candidate, eTag: "\"etag-1\"");

        PullRequestCheckState? state = this._tracker.Get(repo: REPO, number: 1);
        Assert.NotNull(state);
        Assert.Equal(expected: "\"etag-1\"", actual: state.ETag);
        Assert.Equal(expected: MockDateTimeSources.Past.GetUtcNow(), actual: state.LastChecked);
    }

    [Fact]
    public void RecordCheckedIgnoresAnETagForAnOlderHead()
    {
        this._tracker.Observe(repo: REPO, number: 1, headSha: "sha1", isOnHold: false, isOpen: true);
        PullRequestCheckState candidate = Assert.Single(this._tracker.SelectCandidates(5));
        this._tracker.Observe(repo: REPO, number: 1, headSha: "sha2", isOnHold: false, isOpen: true);

        this._tracker.RecordChecked(checkedState: candidate, eTag: "\"etag-1\"");

        PullRequestCheckState? state = this._tracker.Get(repo: REPO, number: 1);
        Assert.NotNull(state);
        Assert.Null(state.ETag);
        Assert.Equal(expected: "sha2", actual: state.HeadSha);
    }

    [Fact]
    public void MarkStaleMakesASettledPullRequestTheFirstCandidate()
    {
        this._tracker.RecordDetail(
            repo: REPO,
            number: 1,
            details: BuildDetails(number: 1, headSha: "sha1", runs: [Run("build", "completed", "success")]),
            isOnHold: false
        );
        this._tracker.RecordDetail(
            repo: REPO,
            number: 2,
            details: BuildDetails(number: 2, headSha: "sha2", runs: [Run("build", "in_progress", null)]),
            isOnHold: false
        );

        this._tracker.MarkStale(repo: REPO, number: 1);

        Assert.Equal(expected: [1, 2], actual: this.CandidateNumbers());
    }

    [Fact]
    public void RetainOnlyForgetsPullRequestsNoLongerOpenInThatRepo()
    {
        this._tracker.Observe(repo: REPO, number: 1, headSha: "sha1", isOnHold: false, isOpen: true);
        this._tracker.Observe(repo: REPO, number: 2, headSha: "sha2", isOnHold: false, isOpen: true);
        this._tracker.Observe(repo: "owner/other", number: 3, headSha: "sha3", isOnHold: false, isOpen: true);

        this._tracker.RetainOnly(repo: REPO, openNumbers: [2]);

        Assert.Null(this._tracker.Get(repo: REPO, number: 1));
        Assert.NotNull(this._tracker.Get(repo: REPO, number: 2));
        Assert.NotNull(this._tracker.Get(repo: "owner/other", number: 3));
    }
}
