using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.Configuration;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Services;
using Credfeto.Dispatcher.GitHub.Tests.Helpers;
using FunFair.Test.Common;
using FunFair.Test.Infrastructure.Mocks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class PullRequestCheckRefresherTests : TestBase
{
    private const string REPO = "owner/repo";

    private const string IN_PROGRESS_RUNS_JSON = """
        {"workflow_runs": [{
          "name": "build",
          "status": "in_progress",
          "conclusion": null,
          "html_url": "https://github.com/owner/repo/actions/runs/1",
          "head_sha": "sha1"
        }]}
        """;

    private const string COMPLETED_RUNS_JSON = """
        {"workflow_runs": [{
          "name": "build",
          "status": "completed",
          "conclusion": "failure",
          "html_url": "https://github.com/owner/repo/actions/runs/1",
          "head_sha": "sha1"
        }]}
        """;

    private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
    private readonly PullRequestCheckTracker _tracker;
    private readonly FakeWorkItemRefresher _workItemRefresher;

    public PullRequestCheckRefresherTests()
    {
        this._httpClientFactory = GetSubstitute<System.Net.Http.IHttpClientFactory>();
        this._workItemRefresher = new FakeWorkItemRefresher();
        this._tracker = new PullRequestCheckTracker(MockDateTimeSources.Past);
    }

    private PullRequestCheckRefresher CreateRefresher(int maxCheckRefreshesPerPoll = 5)
    {
        GitHubRepoHelper helper = new(
            httpClientFactory: this._httpClientFactory,
            logger: this.GetTypedLogger<GitHubRepoHelper>()
        );

        return new PullRequestCheckRefresher(
            helper: helper,
            checkTracker: this._tracker,
            workItemRefresher: this._workItemRefresher,
            options: Options.Create(
                new GitHubOptions
                {
                    Filter = new GitHubFilterOptions { MaxCheckRefreshesPerPoll = maxCheckRefreshesPerPoll },
                }
            ),
            logger: this.GetTypedLogger<PullRequestCheckRefresher>()
        );
    }

    private HttpClient UseHandler(HttpMessageHandler handler)
    {
        HttpClient httpClient = new(handler: handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };
        this._httpClientFactory.CreateClient("GitHub").Returns(httpClient);

        return httpClient;
    }

    private static SequencedResponse Response(HttpStatusCode statusCode, string? content = null, string? eTag = null)
    {
        return new SequencedResponse(StatusCode: statusCode, Content: content, ETag: eTag, LinkUrl: null);
    }

    private void GivenCheckedCandidate(int number, string headSha, string eTag)
    {
        this._tracker.Observe(repo: REPO, number: number, headSha: headSha, isOnHold: false, isOpen: true);
        PullRequestCheckState? state = this._tracker.Get(repo: REPO, number: number);
        Assert.NotNull(state);
        this._tracker.RecordChecked(checkedState: state, eTag: eTag);
    }

    private int RefreshCallCount()
    {
        return this._workItemRefresher.Refreshed.Count;
    }

    [Fact]
    public async Task RefreshAsync_WithNoCandidates_MakesNoRequestsAsync()
    {
        int? interval = await this.CreateRefresher().RefreshAsync(this.CancellationToken());

        Assert.Null(interval);
        Assert.Empty(this._httpClientFactory.ReceivedCalls());
        Assert.Equal(expected: 0, actual: this.RefreshCallCount());
    }

    [Fact]
    public async Task RefreshAsync_WhenTurnedOff_MakesNoRequestsAsync()
    {
        this._tracker.Observe(repo: REPO, number: 1, headSha: "sha1", isOnHold: false, isOpen: true);

        await this.CreateRefresher(maxCheckRefreshesPerPoll: 0).RefreshAsync(this.CancellationToken());

        Assert.Empty(this._httpClientFactory.ReceivedCalls());
    }

    [Fact]
    public async Task RefreshAsync_WithUnknownHead_RefreshesThePullRequestWithoutARunsRequestAsync()
    {
        this._tracker.MarkStale(repo: REPO, number: 7);

        await this.CreateRefresher().RefreshAsync(this.CancellationToken());

        Assert.Empty(this._httpClientFactory.ReceivedCalls());
        Assert.Equal(expected: (REPO, 7), actual: Assert.Single(this._workItemRefresher.Refreshed));
    }

    [Fact]
    public async Task RefreshAsync_WhenRunsAreNotModified_SendsTheStoredETagAndDoesNotRefreshAsync()
    {
        this.GivenCheckedCandidate(number: 1, headSha: "sha1", eTag: "\"runs-1\"");
        using SequencedResponseHandler handler = new(Response(HttpStatusCode.NotModified, eTag: "\"runs-1\""));
        using HttpClient client = this.UseHandler(handler);

        await this.CreateRefresher().RefreshAsync(this.CancellationToken());

        Assert.Equal(
            expected: "/repos/owner/repo/actions/runs?head_sha=sha1&per_page=100",
            actual: Assert.Single(handler.RequestUris)?.PathAndQuery
        );
        Assert.Equal(expected: "\"runs-1\"", actual: Assert.Single(handler.RequestIfNoneMatches));
        Assert.Equal(expected: 0, actual: this.RefreshCallCount());
    }

    [Fact]
    public async Task RefreshAsync_WhenRunsChanged_RefreshesThePullRequestAndKeepsTheNewETagAsync()
    {
        this._tracker.Observe(repo: REPO, number: 1, headSha: "sha1", isOnHold: false, isOpen: true);
        using SequencedResponseHandler handler = new(
            Response(HttpStatusCode.OK, content: COMPLETED_RUNS_JSON, eTag: "\"runs-2\"")
        );
        using HttpClient client = this.UseHandler(handler);

        await this.CreateRefresher().RefreshAsync(this.CancellationToken());

        Assert.Equal(expected: (REPO, 1), actual: Assert.Single(this._workItemRefresher.Refreshed));
        Assert.Equal(expected: "\"runs-2\"", actual: this._tracker.Get(repo: REPO, number: 1)?.ETag);
    }

    [Fact]
    public async Task RefreshAsync_WhenRunsAreUnchangedWithoutAnETagMatch_DoesNotRefreshAsync()
    {
        this._tracker.RecordDetail(
            repo: REPO,
            number: 1,
            details: BuildDetails(number: 1, headSha: "sha1", status: "in_progress"),
            isOnHold: false
        );
        using SequencedResponseHandler handler = new(
            Response(HttpStatusCode.OK, content: IN_PROGRESS_RUNS_JSON, eTag: "\"runs-3\"")
        );
        using HttpClient client = this.UseHandler(handler);

        await this.CreateRefresher().RefreshAsync(this.CancellationToken());

        Assert.Equal(expected: 0, actual: this.RefreshCallCount());
        Assert.Equal(expected: "\"runs-3\"", actual: this._tracker.Get(repo: REPO, number: 1)?.ETag);
    }

    [Fact]
    public async Task RefreshAsync_ChecksNoMoreThanTheConfiguredNumberOfPullRequestsAsync()
    {
        for (int number = 1; number <= 4; ++number)
        {
            this.GivenCheckedCandidate(
                number: number,
                headSha: "sha" + number.ToString(CultureInfo.InvariantCulture),
                eTag: "\"e\""
            );
        }

        using SequencedResponseHandler handler = new(
            Response(HttpStatusCode.NotModified),
            Response(HttpStatusCode.NotModified)
        );
        using HttpClient client = this.UseHandler(handler);

        await this.CreateRefresher(maxCheckRefreshesPerPoll: 2).RefreshAsync(this.CancellationToken());

        Assert.Equal(expected: 2, actual: handler.RequestUris.Count);
    }

    [Fact]
    public async Task RefreshAsync_WhenRateLimited_StopsTheBatchAsync()
    {
        this.GivenCheckedCandidate(number: 1, headSha: "sha1", eTag: "\"e\"");
        this.GivenCheckedCandidate(number: 2, headSha: "sha2", eTag: "\"e\"");
        using SequencedResponseHandler handler = new(Response(HttpStatusCode.Forbidden));
        using HttpClient client = this.UseHandler(handler);

        await this.CreateRefresher().RefreshAsync(this.CancellationToken());

        Assert.Single(handler.RequestUris);
        Assert.Equal(expected: 0, actual: this.RefreshCallCount());
    }

    [Fact]
    public async Task RefreshAsync_ReturnsThePollIntervalGitHubAskedForAsync()
    {
        this.GivenCheckedCandidate(number: 1, headSha: "sha1", eTag: "\"e\"");
        (HttpClient client, FixedResponseHandler handler) = HttpClientTestFactory.CreateWithHandler(
            statusCode: HttpStatusCode.NotModified,
            pollIntervalSeconds: 120
        );

        using (client)
        using (handler)
        {
            this._httpClientFactory.CreateClient("GitHub").Returns(client);

            int? interval = await this.CreateRefresher().RefreshAsync(this.CancellationToken());

            Assert.Equal(expected: 120, actual: interval);
        }
    }

    [Fact]
    public async Task RefreshAsync_WhenTheRefreshFails_DoesNotRetryThePullRequestFirstNextCycleAsync()
    {
        this._tracker.MarkStale(repo: REPO, number: 1);
        this._tracker.Observe(repo: REPO, number: 2, headSha: null, isOnHold: false, isOpen: true);
        this._workItemRefresher.Result = false;

        await this.CreateRefresher(maxCheckRefreshesPerPoll: 1).RefreshAsync(this.CancellationToken());

        PullRequestCheckState next = Assert.Single(this._tracker.SelectCandidates(1));
        Assert.Equal(expected: 2, actual: next.Number);
    }

    private static PullRequestDetails BuildDetails(int number, string headSha, string status)
    {
        return new PullRequestDetails(
            Number: number,
            Title: "PR",
            Status: "Open",
            HtmlUrl: new Uri($"https://github.com/owner/repo/pull/{number}"),
            Assignees: [],
            Labels: [],
            Body: null,
            Comments: [],
            Reviews: [],
            Runs:
            [
                new PullRequestRun(
                    Name: "build",
                    Status: status,
                    Conclusion: null,
                    Url: new Uri("https://github.com/owner/repo/actions/runs/1"),
                    IsRequired: true,
                    HeadSha: headSha
                ),
            ],
            LinkedItems: [],
            Repository: new ItemRepository(Owner: "owner", Name: "repo", Url: new Uri("https://github.com/owner/repo")),
            LastNotification: new LastNotification(Id: "n", Timestamp: DateTimeOffset.MinValue),
            Author: "dnyw4l3n13",
            CommitAuthors: [],
            HasDetail: true,
            HeadSha: headSha
        );
    }

    private sealed class FakeWorkItemRefresher : IWorkItemRefresher
    {
        private readonly List<(string Repo, int Number)> _refreshed = [];

        public bool Result { get; set; } = true;

        public IReadOnlyList<(string Repo, int Number)> Refreshed => this._refreshed;

        public ValueTask<bool> RefreshPullRequestAsync(
            GitHubNotification notification,
            CancellationToken cancellationToken
        )
        {
            return ValueTask.FromResult(false);
        }

        public ValueTask<bool> RefreshPullRequestAsync(string repo, int number, CancellationToken cancellationToken)
        {
            this._refreshed.Add((repo, number));

            return ValueTask.FromResult(this.Result);
        }

        public ValueTask<bool> RefreshIssueAsync(GitHubNotification notification, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(false);
        }
    }
}
