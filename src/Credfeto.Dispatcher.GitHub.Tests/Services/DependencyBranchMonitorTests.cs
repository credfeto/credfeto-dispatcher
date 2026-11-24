using System;
using System.Collections.Generic;
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

public sealed class DependencyBranchMonitorTests : TestBase
{
    private const string REPO = "owner/repo";
    private const string BRANCH = "depends/update-sdk";
    private const string OTHER_BRANCH = "dependabot/npm/left-pad-2.0.0";

    private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
    private readonly IDependencyBranchStore _store;
    private readonly TimeProvider _timeProvider;

    public DependencyBranchMonitorTests()
    {
        this._httpClientFactory = GetSubstitute<System.Net.Http.IHttpClientFactory>();
        this._store = GetSubstitute<IDependencyBranchStore>();
        this._timeProvider = MockDateTimeSources.Past;
        this.GivenDue([]);
    }

    private DependencyBranchMonitor CreateMonitor(int maxRechecks = 3)
    {
        GitHubRepoHelper helper = new(
            httpClientFactory: this._httpClientFactory,
            logger: this.GetTypedLogger<GitHubRepoHelper>()
        );

        return new DependencyBranchMonitor(
            helper: helper,
            store: this._store,
            options: Options.Create(
                new GitHubOptions
                {
                    Filter = new GitHubFilterOptions { MaxDependencyBranchRechecksPerPoll = maxRechecks },
                }
            ),
            timeProvider: this._timeProvider,
            logger: this.GetTypedLogger<DependencyBranchMonitor>()
        );
    }

    private void GivenDue(IReadOnlyList<DependencyBranchRecheck> due)
    {
        this._store.GetDueForRecheckAsync(
                maxCount: Arg.Any<int>(),
                checkedBefore: Arg.Any<DateTimeOffset>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(due);
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

    private IReadOnlyList<string> StoreCallNames()
    {
        return [.. this._store.ReceivedCalls().Select(static call => call.GetMethodInfo().Name)];
    }

    [Fact]
    public async Task UpdateAsync_AppliesChangesInOrderAsync()
    {
        DateTimeOffset created = this._timeProvider.GetUtcNow().AddMinutes(-2);
        DateTimeOffset deleted = this._timeProvider.GetUtcNow().AddMinutes(-1);

        await this.CreateMonitor()
            .UpdateAsync(
                changes:
                [
                    new DependencyBranchChange(Repository: REPO, Branch: BRANCH, IsDeleted: false, At: created),
                    new DependencyBranchChange(Repository: REPO, Branch: BRANCH, IsDeleted: true, At: deleted),
                ],
                cancellationToken: this.CancellationToken()
            );

        Assert.Equal(
            expected:
            [
                nameof(IDependencyBranchStore.RecordCreatedAsync),
                nameof(IDependencyBranchStore.RecordDeletedAsync),
                nameof(IDependencyBranchStore.GetDueForRecheckAsync),
                nameof(IDependencyBranchStore.PruneAsync),
            ],
            actual: this.StoreCallNames()
        );
        await this
            ._store.Received(1)
            .RecordCreatedAsync(
                repository: REPO,
                branch: BRANCH,
                createdAt: created,
                cancellationToken: Arg.Any<CancellationToken>()
            );
        await this
            ._store.Received(1)
            .RecordDeletedAsync(
                repository: REPO,
                branch: BRANCH,
                deletedAt: deleted,
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task UpdateAsync_AsksForABoundedBatchNotCheckedRecentlyAsync()
    {
        await this.CreateMonitor(maxRechecks: 3).UpdateAsync(changes: [], cancellationToken: this.CancellationToken());

        await this
            ._store.Received(1)
            .GetDueForRecheckAsync(
                maxCount: 3,
                checkedBefore: this._timeProvider.GetUtcNow() - DependencyBranchMonitor.RecheckInterval,
                cancellationToken: Arg.Any<CancellationToken>()
            );
        Assert.Empty(this._httpClientFactory.ReceivedCalls());
    }

    [Fact]
    public async Task UpdateAsync_PrunesSettledAndVeryOldBranchesAsync()
    {
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        await this.CreateMonitor().UpdateAsync(changes: [], cancellationToken: this.CancellationToken());

        await this
            ._store.Received(1)
            .PruneAsync(
                settledBefore: now - TimeSpan.FromDays(7),
                firstSeenBefore: now - TimeSpan.FromDays(30),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task UpdateAsync_WhenRecheckTurnedOff_MakesNoRequestsAsync()
    {
        this.GivenDue([new DependencyBranchRecheck(Repository: REPO, Branch: BRANCH, ETag: null)]);

        await this.CreateMonitor(maxRechecks: 0).UpdateAsync(changes: [], cancellationToken: this.CancellationToken());

        Assert.DoesNotContain(
            expected: nameof(IDependencyBranchStore.GetDueForRecheckAsync),
            collection: this.StoreCallNames()
        );
        Assert.Empty(this._httpClientFactory.ReceivedCalls());
    }

    [Fact]
    public async Task UpdateAsync_WhenTheBranchNowHasAPullRequest_ResolvesItAsync()
    {
        this.GivenDue([new DependencyBranchRecheck(Repository: REPO, Branch: BRANCH, ETag: "\"old\"")]);
        using SequencedResponseHandler handler = new(
            Response(
                statusCode: HttpStatusCode.OK,
                content: """[{"number": 77, "title": "Bump", "state": "closed", "head": {"sha": "abc", "ref": "x"}}]""",
                eTag: "\"new\""
            )
        );
        using HttpClient client = this.UseHandler(handler);

        await this.CreateMonitor().UpdateAsync(changes: [], cancellationToken: this.CancellationToken());

        Assert.Equal(
            expected: "/repos/owner/repo/pulls?state=all&head=owner:depends%2Fupdate-sdk&per_page=10",
            actual: Assert.Single(handler.RequestUris)?.PathAndQuery
        );
        Assert.Equal(expected: "\"old\"", actual: Assert.Single(handler.RequestIfNoneMatches));
        await this
            ._store.Received(1)
            .RecordPullRequestAsync(
                repository: REPO,
                branch: BRANCH,
                pullRequestNumber: 77,
                cancellationToken: Arg.Any<CancellationToken>()
            );
        await this
            ._store.Received(1)
            .RecordCheckedAsync(
                repository: REPO,
                branch: BRANCH,
                eTag: "\"new\"",
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task UpdateAsync_WhenTheBranchStillHasNoPullRequest_RecordsTheCheckOnlyAsync()
    {
        this.GivenDue([new DependencyBranchRecheck(Repository: REPO, Branch: BRANCH, ETag: null)]);
        using SequencedResponseHandler handler = new(
            Response(statusCode: HttpStatusCode.OK, content: "[]", eTag: "\"empty\"")
        );
        using HttpClient client = this.UseHandler(handler);

        await this.CreateMonitor().UpdateAsync(changes: [], cancellationToken: this.CancellationToken());

        Assert.DoesNotContain(
            expected: nameof(IDependencyBranchStore.RecordPullRequestAsync),
            collection: this.StoreCallNames()
        );
        await this
            ._store.Received(1)
            .RecordCheckedAsync(
                repository: REPO,
                branch: BRANCH,
                eTag: "\"empty\"",
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task UpdateAsync_WhenNotModified_KeepsTheStoredETagAsync()
    {
        this.GivenDue([new DependencyBranchRecheck(Repository: REPO, Branch: BRANCH, ETag: "\"old\"")]);
        using SequencedResponseHandler handler = new(Response(statusCode: HttpStatusCode.NotModified, eTag: "\"old\""));
        using HttpClient client = this.UseHandler(handler);

        await this.CreateMonitor().UpdateAsync(changes: [], cancellationToken: this.CancellationToken());

        Assert.DoesNotContain(
            expected: nameof(IDependencyBranchStore.RecordPullRequestAsync),
            collection: this.StoreCallNames()
        );
        await this
            ._store.Received(1)
            .RecordCheckedAsync(
                repository: REPO,
                branch: BRANCH,
                eTag: null,
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task UpdateAsync_WhenRateLimited_StopsAndLeavesTheBranchDueAsync()
    {
        this.GivenDue([
            new DependencyBranchRecheck(Repository: REPO, Branch: BRANCH, ETag: null),
            new DependencyBranchRecheck(Repository: REPO, Branch: OTHER_BRANCH, ETag: null),
        ]);
        using SequencedResponseHandler handler = new(Response(statusCode: HttpStatusCode.Forbidden));
        using HttpClient client = this.UseHandler(handler);

        await this.CreateMonitor().UpdateAsync(changes: [], cancellationToken: this.CancellationToken());

        Assert.Single(handler.RequestUris);
        Assert.DoesNotContain(
            expected: nameof(IDependencyBranchStore.RecordCheckedAsync),
            collection: this.StoreCallNames()
        );
    }

    [Fact]
    public async Task UpdateAsync_ChecksEachDueBranchAsync()
    {
        this.GivenDue([
            new DependencyBranchRecheck(Repository: REPO, Branch: BRANCH, ETag: null),
            new DependencyBranchRecheck(Repository: REPO, Branch: OTHER_BRANCH, ETag: null),
        ]);
        using SequencedResponseHandler handler = new(
            Response(statusCode: HttpStatusCode.OK, content: "[]"),
            Response(statusCode: HttpStatusCode.NotFound)
        );
        using HttpClient client = this.UseHandler(handler);

        await this.CreateMonitor().UpdateAsync(changes: [], cancellationToken: this.CancellationToken());

        Assert.Equal(expected: 2, actual: handler.RequestUris.Count);
        Assert.Equal(
            expected: 2,
            actual: this.StoreCallNames()
                .Count(static n =>
                    string.Equals(
                        a: n,
                        b: nameof(IDependencyBranchStore.RecordCheckedAsync),
                        comparisonType: StringComparison.Ordinal
                    )
                )
        );
    }
}
