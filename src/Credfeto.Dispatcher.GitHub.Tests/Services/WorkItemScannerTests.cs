using System;
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
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class WorkItemScannerTests : TestBase
{
    private const string Owner = "owner";
    private const string Repo = "owner/repo";

    private const string UserReposJson = """
        [{
          "full_name": "owner/repo",
          "archived": false,
          "disabled": false,
          "permissions": {"push": true}
        }]
        """;

    private const string ReadOnlyRepoJson = """
        [{
          "full_name": "owner/repo",
          "archived": false,
          "disabled": false,
          "permissions": {"push": false}
        }]
        """;

    private const string ArchivedRepoJson = """
        [{
          "full_name": "owner/repo",
          "archived": true,
          "disabled": false,
          "permissions": {"push": true}
        }]
        """;

    private const string OpenPrJson = """
        [{
          "number": 1,
          "title": "Test PR",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/1",
          "assignees": [],
          "labels": [],
          "head": {"sha": "abc123"}
        }]
        """;

    private const string DraftPrJson = """
        [{
          "number": 2,
          "title": "Draft PR",
          "state": "open",
          "draft": true,
          "html_url": "https://github.com/owner/repo/pull/2",
          "assignees": [],
          "labels": [],
          "head": {"sha": "def456"}
        }]
        """;

    private const string PrWithUrgentLabelJson = """
        [{
          "number": 3,
          "title": "Urgent PR",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/3",
          "assignees": [],
          "labels": [{"name": "priority: urgent"}],
          "head": {"sha": "ghi789"}
        }]
        """;

    private const string PrWithOnHoldLabelJson = """
        [{
          "number": 4,
          "title": "On-hold PR",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/4",
          "assignees": [],
          "labels": [{"name": "on-hold"}],
          "head": {"sha": "jkl012"}
        }]
        """;

    private const string PrWithAiWorkLabelJson = """
        [{
          "number": 5,
          "title": "AI Work PR",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/5",
          "assignees": [],
          "labels": [{"name": "AI-Work"}],
          "head": {"sha": "mno345"}
        }]
        """;

    private const string PrWithUnrelatedLabelJson = """
        [{
          "number": 6,
          "title": "Unrelated PR",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/6",
          "assignees": [],
          "labels": [{"name": "bug"}],
          "head": {"sha": "pqr678"}
        }]
        """;

    private const string PrPage1Json = """
        [{
          "number": 10,
          "title": "PR Page 1",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/10",
          "assignees": [],
          "labels": [],
          "head": {"sha": "stu901"}
        }]
        """;

    private const string PrPage2Json = """
        [{
          "number": 11,
          "title": "PR Page 2",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/11",
          "assignees": [],
          "labels": [],
          "head": {"sha": "vwx234"}
        }]
        """;

    private const string OpenIssueJson = """
        [{
          "number": 20,
          "title": "Test Issue",
          "state": "open",
          "html_url": "https://github.com/owner/repo/issues/20",
          "assignees": [],
          "labels": [],
          "pull_request": null
        }]
        """;

    private const string IssueWithLinkedPrJson = """
        [{
          "number": 21,
          "title": "Issue with PR",
          "state": "open",
          "html_url": "https://github.com/owner/repo/issues/21",
          "assignees": [],
          "labels": [],
          "pull_request": {"html_url": "https://github.com/owner/repo/pull/21"}
        }]
        """;

    private const string TwoReposJson = """
        [
          {
            "full_name": "owner/repo",
            "archived": false,
            "disabled": false,
            "permissions": {"push": true}
          },
          {
            "full_name": "owner/repo2",
            "archived": false,
            "disabled": false,
            "permissions": {"push": true}
          }
        ]
        """;

    private const string EmptyJson = "[]";

    private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
    private readonly INotificationStateTracker _notificationStateTracker;

    public WorkItemScannerTests()
    {
        this._httpClientFactory = GetSubstitute<System.Net.Http.IHttpClientFactory>();
        this._notificationStateTracker = GetSubstitute<INotificationStateTracker>();
    }

    private WorkItemScanner CreateScanner(GitHubOptions? options = null)
    {
        return new WorkItemScanner(
            httpClientFactory: this._httpClientFactory,
            notificationStateTracker: this._notificationStateTracker,
            options: Options.Create(options ?? new GitHubOptions()),
            logger: this.GetTypedLogger<WorkItemScanner>()
        );
    }

    private static HttpClient CreateClient(
        HttpStatusCode statusCode,
        string? content = null,
        string? linkUrl = null
    )
    {
        FixedResponseHandler? handler = new(
            statusCode: statusCode,
            content: content,
            linkUrl: linkUrl
        );

        try
        {
            HttpClient client = new(handler: handler, disposeHandler: true)
            {
                BaseAddress = new Uri("https://api.github.com/"),
            };
            handler = null;

            return client;
        }
        finally
        {
            handler?.Dispose();
        }
    }

    [Fact]
    public async Task ScanAsync_WhenRepoDiscoveryReturnsEmpty_MakesNoStateUpdatesAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, EmptyJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdatePullRequestStateAsync(
                repository: Arg.Any<string>(),
                pullRequestNumber: Arg.Any<int>(),
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateIssueStateAsync(
                repository: Arg.Any<string>(),
                issueNumber: Arg.Any<int>(),
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                hasLinkedPr: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenRepoIsReadOnly_MakesNoStateUpdatesAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, ReadOnlyRepoJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdatePullRequestStateAsync(
                repository: Arg.Any<string>(),
                pullRequestNumber: Arg.Any<int>(),
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenRepoIsArchived_MakesNoStateUpdatesAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, ArchivedRepoJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdatePullRequestStateAsync(
                repository: Arg.Any<string>(),
                pullRequestNumber: Arg.Any<int>(),
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenOwnerNotInAllowedOwners_MakesNoStateUpdatesAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, UserReposJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient);

        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { AllowedOwners = ["other-owner"] },
        };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdatePullRequestStateAsync(
                repository: Arg.Any<string>(),
                pullRequestNumber: Arg.Any<int>(),
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenRepoNotInAllowedRepos_MakesNoStateUpdatesAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, UserReposJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient);

        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { AllowedRepos = ["owner/other-repo"] },
        };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdatePullRequestStateAsync(
                repository: Arg.Any<string>(),
                pullRequestNumber: Arg.Any<int>(),
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenRepoIsExcluded_MakesNoStateUpdatesAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, UserReposJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient);

        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { ExcludedRepos = [Repo] },
        };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdatePullRequestStateAsync(
                repository: Arg.Any<string>(),
                pullRequestNumber: Arg.Any<int>(),
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithOpenPullRequest_CallsUpdateWithOpenStatusAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, UserReposJson);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EmptyJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdatePullRequestStateAsync(
                repository: Repo,
                pullRequestNumber: 1,
                status: "Open",
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithDraftPullRequest_CallsUpdateWithDraftStatusAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, UserReposJson);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, DraftPrJson);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EmptyJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdatePullRequestStateAsync(
                repository: Repo,
                pullRequestNumber: 2,
                status: "Draft",
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithUrgentPriorityLabel_CallsUpdateWithUrgentPriorityAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, UserReposJson);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, PrWithUrgentLabelJson);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EmptyJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdatePullRequestStateAsync(
                repository: Repo,
                pullRequestNumber: 3,
                status: Arg.Any<string>(),
                priority: WorkPriority.Urgent,
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithOnHoldLabel_CallsUpdateWithIsOnHoldTrueAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, UserReposJson);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, PrWithOnHoldLabelJson);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EmptyJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { NoWorkFilter = ["on-hold"] },
        };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdatePullRequestStateAsync(
                repository: Repo,
                pullRequestNumber: 4,
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: true,
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithMatchingLabelFilter_CallsUpdateAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, UserReposJson);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, PrWithAiWorkLabelJson);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EmptyJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] },
        };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdatePullRequestStateAsync(
                repository: Repo,
                pullRequestNumber: 5,
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithNonMatchingLabelFilter_DoesNotCallUpdateAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, UserReposJson);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, PrWithUnrelatedLabelJson);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EmptyJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] },
        };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdatePullRequestStateAsync(
                repository: Arg.Any<string>(),
                pullRequestNumber: Arg.Any<int>(),
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithPullRequestApiFailure_DoesNotThrowAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, UserReposJson);
        using HttpClient prClient = CreateClient(HttpStatusCode.InternalServerError);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EmptyJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdatePullRequestStateAsync(
                repository: Arg.Any<string>(),
                pullRequestNumber: Arg.Any<int>(),
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithOpenIssue_CallsUpdateIssueStateAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, UserReposJson);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, EmptyJson);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, OpenIssueJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateIssueStateAsync(
                repository: Repo,
                issueNumber: 20,
                status: "Open",
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                hasLinkedPr: Arg.Is<bool>(v => !v),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithIssueLinkedToPr_SkipsIssueAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, UserReposJson);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, EmptyJson);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, IssueWithLinkedPrJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateIssueStateAsync(
                repository: Arg.Any<string>(),
                issueNumber: Arg.Any<int>(),
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                hasLinkedPr: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithIssueApiFailure_DoesNotThrowAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, UserReposJson);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, EmptyJson);
        using HttpClient issueClient = CreateClient(HttpStatusCode.InternalServerError);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateIssueStateAsync(
                repository: Arg.Any<string>(),
                issueNumber: Arg.Any<int>(),
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                hasLinkedPr: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithPaginatedPullRequests_ProcessesBothPagesAsync()
    {
        const string page2Url =
            "https://api.github.com/repos/owner/repo/pulls?state=open&per_page=100&page=2";

        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, UserReposJson);
        using HttpClient prPage1Client = CreateClient(
            HttpStatusCode.OK,
            PrPage1Json,
            linkUrl: page2Url
        );
        using HttpClient prPage2Client = CreateClient(HttpStatusCode.OK, PrPage2Json);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EmptyJson);
        this._httpClientFactory.CreateClient("GitHub")
            .Returns(repoClient, prPage1Client, prPage2Client, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdatePullRequestStateAsync(
                repository: Repo,
                pullRequestNumber: 10,
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );

        await this
            ._notificationStateTracker.Received(1)
            .UpdatePullRequestStateAsync(
                repository: Repo,
                pullRequestNumber: 11,
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithMultipleDiscoveredRepos_ScansEachRepoAsync()
    {
        const string repo2 = "owner/repo2";

        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, TwoReposJson);
        using HttpClient prClient1 = CreateClient(HttpStatusCode.OK, EmptyJson);
        using HttpClient issueClient1 = CreateClient(HttpStatusCode.OK, OpenIssueJson);
        using HttpClient prClient2 = CreateClient(HttpStatusCode.OK, EmptyJson);
        using HttpClient issueClient2 = CreateClient(HttpStatusCode.OK, EmptyJson);
        this._httpClientFactory.CreateClient("GitHub")
            .Returns(repoClient, prClient1, issueClient1, prClient2, issueClient2);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateIssueStateAsync(
                repository: Repo,
                issueNumber: 20,
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                hasLinkedPr: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateIssueStateAsync(
                repository: repo2,
                issueNumber: Arg.Any<int>(),
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                hasLinkedPr: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenAllowedOwnerMatches_ScansRepoAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, UserReposJson);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OpenPrJson);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EmptyJson);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        GitHubOptions options = new()
        {
            Filter = new GitHubFilterOptions { AllowedOwners = [Owner] },
        };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdatePullRequestStateAsync(
                repository: Repo,
                pullRequestNumber: 1,
                status: Arg.Any<string>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }
}
