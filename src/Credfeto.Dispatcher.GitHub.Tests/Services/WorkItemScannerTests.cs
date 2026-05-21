using System;
using System.Collections.Generic;
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
    private const string OWNER = "owner";
    private const string REPO = "owner/repo";

    private const string USER_REPOS_JSON = """
        [{
          "full_name": "owner/repo",
          "archived": false,
          "disabled": false,
          "permissions": {"push": true}
        }]
        """;

    private const string READ_ONLY_REPO_JSON = """
        [{
          "full_name": "owner/repo",
          "archived": false,
          "disabled": false,
          "permissions": {"push": false}
        }]
        """;

    private const string ARCHIVED_REPO_JSON = """
        [{
          "full_name": "owner/repo",
          "archived": true,
          "disabled": false,
          "permissions": {"push": true}
        }]
        """;

    private const string OPEN_PR_JSON = """
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

    private const string DRAFT_PR_JSON = """
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

    private const string PR_WITH_URGENT_LABEL_JSON = """
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

    private const string PR_WITH_SECURITY_LABEL_JSON = """
        [{
          "number": 9,
          "title": "Security PR",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/9",
          "assignees": [],
          "labels": [{"name": "Security"}],
          "head": {"sha": "vwx234"}
        }]
        """;

    private const string PR_WITH_ON_HOLD_LABEL_JSON = """
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

    private const string PR_WITH_AI_WORK_LABEL_JSON = """
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

    private const string PR_WITH_UNRELATED_LABEL_JSON = """
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

    private const string PR_WITH_AI_WORK_SPACE_LABEL_JSON = """
        [{
          "number": 7,
          "title": "AI Work PR (space variant)",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/7",
          "assignees": [],
          "labels": [{"name": "AI Work"}],
          "head": {"sha": "stu901"}
        }]
        """;

    private const string PR_WITH_AI_WORK_LOWERCASE_LABEL_JSON = """
        [{
          "number": 8,
          "title": "AI Work PR (lowercase variant)",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/8",
          "assignees": [],
          "labels": [{"name": "ai work"}],
          "head": {"sha": "vwx234"}
        }]
        """;

    private const string PR_WITH_ON_HOLD_SPACE_LABEL_JSON = """
        [{
          "number": 9,
          "title": "On-hold PR (space variant)",
          "state": "open",
          "draft": false,
          "html_url": "https://github.com/owner/repo/pull/9",
          "assignees": [],
          "labels": [{"name": "on hold"}],
          "head": {"sha": "yza567"}
        }]
        """;

    private const string PR_PAGE1_JSON = """
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

    private const string PR_PAGE2_JSON = """
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

    private const string OPEN_ISSUE_JSON = """
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

    private const string ISSUE_WITH_LINKED_PR_JSON = """
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

    private const string TWO_REPOS_JSON = """
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

    private const string EMPTY_JSON = "[]";

    private readonly IActiveRepoTracker _activeRepoTracker;
    private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
    private readonly INotificationStateTracker _notificationStateTracker;
    private readonly IWorkItemRepository _workItemRepository;

    public WorkItemScannerTests()
    {
        this._httpClientFactory = GetSubstitute<System.Net.Http.IHttpClientFactory>();
        this._activeRepoTracker = GetSubstitute<IActiveRepoTracker>();
        this._notificationStateTracker = GetSubstitute<INotificationStateTracker>();
        this._workItemRepository = GetSubstitute<IWorkItemRepository>();
    }

    private WorkItemScanner CreateScanner(GitHubOptions? options = null)
    {
        GitHubRepoHelper helper = new(
            httpClientFactory: this._httpClientFactory,
            logger: this.GetTypedLogger<GitHubRepoHelper>()
        );

        return new WorkItemScanner(
            helper: helper,
            activeRepoTracker: this._activeRepoTracker,
            notificationStateTracker: this._notificationStateTracker,
            workItemRepository: this._workItemRepository,
            options: Options.Create(options ?? new GitHubOptions()),
            logger: this.GetTypedLogger<WorkItemScanner>()
        );
    }

    private static HttpClient CreateClient(HttpStatusCode statusCode, string? content = null, string? linkUrl = null)
    {
        FixedResponseHandler? handler = new(statusCode: statusCode, content: content, linkUrl: linkUrl);

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
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Any<PullRequestDetails>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Any<IssueDetails>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenRepoIsReadOnly_MakesNoStateUpdatesAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, READ_ONLY_REPO_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Any<PullRequestDetails>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenRepoIsArchived_MakesNoStateUpdatesAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, ARCHIVED_REPO_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Any<PullRequestDetails>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenRepoIsArchived_RemovesStoredItemsAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, ARCHIVED_REPO_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._workItemRepository.Received(1)
            .RemoveItemsForRepositoriesAsync(
                repositories: Arg.Any<IReadOnlyList<string>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenOwnerNotInAllowedOwners_MakesNoStateUpdatesAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { AllowedOwners = ["other-owner"] } };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Any<PullRequestDetails>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenRepoNotInAllowedRepos_MakesNoStateUpdatesAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { AllowedRepos = ["owner/other-repo"] } };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Any<PullRequestDetails>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenRepoIsExcluded_MakesNoStateUpdatesAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { ExcludedRepos = [REPO] } };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Any<PullRequestDetails>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithOpenPullRequest_CallsUpdateWithOpenStatusAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OPEN_PR_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Is<GitHubNotification>(n => n.Repository.FullName == REPO),
                details: Arg.Is<PullRequestDetails>(d => d.Number == 1 && d.Status == "Open"),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithDraftPullRequest_CallsUpdateWithDraftStatusAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, DRAFT_PR_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Is<GitHubNotification>(n => n.Repository.FullName == REPO),
                details: Arg.Is<PullRequestDetails>(d => d.Number == 2 && d.Status == "Draft"),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithUrgentPriorityLabel_CallsUpdateWithUrgentPriorityAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, PR_WITH_URGENT_LABEL_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Is<PullRequestDetails>(d => d.Number == 3),
                priority: WorkPriority.URGENT,
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithSecurityPriorityLabel_CallsUpdateWithSecurityPriorityAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, PR_WITH_SECURITY_LABEL_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Is<PullRequestDetails>(d => d.Number == 9),
                priority: WorkPriority.SECURITY,
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithOnHoldLabel_CallsUpdateWithIsOnHoldTrueAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, PR_WITH_ON_HOLD_LABEL_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { NoWorkFilter = ["on-hold"] } };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Is<PullRequestDetails>(d => d.Number == 4),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: true,
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithMatchingLabelFilter_CallsUpdateAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, PR_WITH_AI_WORK_LABEL_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Is<PullRequestDetails>(d => d.Number == 5),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithNonMatchingLabelFilter_DoesNotCallUpdateAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, PR_WITH_UNRELATED_LABEL_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Any<PullRequestDetails>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithSpaceVariantLabelFilter_CallsUpdateAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, PR_WITH_AI_WORK_SPACE_LABEL_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Is<PullRequestDetails>(d => d.Number == 7),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithLowercaseVariantLabelFilter_CallsUpdateAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, PR_WITH_AI_WORK_LOWERCASE_LABEL_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Is<PullRequestDetails>(d => d.Number == 8),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithSpaceVariantOnHoldLabel_CallsUpdateWithIsOnHoldTrueAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, PR_WITH_ON_HOLD_SPACE_LABEL_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { NoWorkFilter = ["on-hold"] } };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Is<PullRequestDetails>(d => d.Number == 9),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: true,
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithPullRequestApiFailure_DoesNotThrowAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.InternalServerError);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Any<PullRequestDetails>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithOpenIssue_CallsUpdateStateAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, OPEN_ISSUE_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Is<IssueDetails>(d => d.Number == 20 && d.Status == "Open"),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithIssueLinkedToPr_SkipsIssueAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, ISSUE_WITH_LINKED_PR_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Any<IssueDetails>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithIssueApiFailure_DoesNotThrowAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.InternalServerError);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Any<IssueDetails>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithPaginatedPullRequests_ProcessesBothPagesAsync()
    {
        const string page2Url = "https://api.github.com/repos/owner/repo/pulls?state=open&per_page=100&page=2";

        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prPage1Client = CreateClient(HttpStatusCode.OK, PR_PAGE1_JSON, linkUrl: page2Url);
        using HttpClient prPage2Client = CreateClient(HttpStatusCode.OK, PR_PAGE2_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prPage1Client, prPage2Client, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Is<PullRequestDetails>(d => d.Number == 10),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Any<GitHubNotification>(),
                details: Arg.Is<PullRequestDetails>(d => d.Number == 11),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithMultipleDiscoveredRepos_ScansEachRepoAsync()
    {
        const string repo2 = "owner/repo2";

        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, TWO_REPOS_JSON);
        using HttpClient prClient1 = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        using HttpClient issueClient1 = CreateClient(HttpStatusCode.OK, OPEN_ISSUE_JSON);
        using HttpClient prClient2 = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        using HttpClient issueClient2 = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub")
            .Returns(repoClient, prClient1, issueClient1, prClient2, issueClient2);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Is<GitHubNotification>(n => n.Repository.FullName == REPO),
                details: Arg.Is<IssueDetails>(d => d.Number == 20),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );

        await this
            ._notificationStateTracker.DidNotReceive()
            .UpdateStateAsync(
                notification: Arg.Is<GitHubNotification>(n => n.Repository.FullName == repo2),
                details: Arg.Any<IssueDetails>(),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenAllowedOwnerMatches_ScansRepoAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OPEN_PR_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { AllowedOwners = [OWNER] } };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._notificationStateTracker.Received(1)
            .UpdateStateAsync(
                notification: Arg.Is<GitHubNotification>(n => n.Repository.FullName == REPO),
                details: Arg.Is<PullRequestDetails>(d => d.Number == 1),
                priority: Arg.Any<WorkPriority>(),
                isOnHold: Arg.Any<bool>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenRepoDiscoveryApiFails_DoesNotCallUpdateActiveReposAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.InternalServerError);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._activeRepoTracker.DidNotReceive()
            .UpdateActiveReposAsync(
                activeRepos: Arg.Any<System.Collections.Generic.IReadOnlyList<string>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenSecondPageOfRepoDiscoveryFails_DoesNotCallUpdateActiveReposAsync()
    {
        const string page2Url = "https://api.github.com/repos?page=2";

        using HttpClient repoPage1Client = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON, linkUrl: page2Url);
        using HttpClient repoPage2Client = CreateClient(HttpStatusCode.InternalServerError);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoPage1Client, repoPage2Client);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._activeRepoTracker.DidNotReceive()
            .UpdateActiveReposAsync(
                activeRepos: Arg.Any<System.Collections.Generic.IReadOnlyList<string>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithDiscoveredRepos_CallsUpdateActiveReposWithRepoListAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._activeRepoTracker.Received(1)
            .UpdateActiveReposAsync(
                activeRepos: Arg.Is<System.Collections.Generic.IReadOnlyList<string>>(r =>
                    r.Count == 1 && r[0] == REPO
                ),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenNoReposDiscovered_DoesNotCallUpdateActiveReposAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._activeRepoTracker.DidNotReceive()
            .UpdateActiveReposAsync(
                activeRepos: Arg.Any<System.Collections.Generic.IReadOnlyList<string>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenRepoIsArchived_DoesNotCallUpdateActiveReposAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, ARCHIVED_REPO_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._activeRepoTracker.DidNotReceive()
            .UpdateActiveReposAsync(
                activeRepos: Arg.Any<System.Collections.Generic.IReadOnlyList<string>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithSuccessfulScan_CallsCloseStaleItemsAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, OPEN_PR_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, OPEN_ISSUE_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._workItemRepository.Received(1)
            .CloseStaleItemsForRepoAsync(
                repository: REPO,
                activePullRequestNumbers: Arg.Is<IReadOnlyList<int>>(l => l.Count == 1 && l[0] == 1),
                activeIssueNumbers: Arg.Is<IReadOnlyList<int>>(l => l.Count == 1 && l[0] == 20),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenPrScanFails_DoesNotCallCloseStaleItemsAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.InternalServerError);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, OPEN_ISSUE_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._workItemRepository.DidNotReceive()
            .CloseStaleItemsForRepoAsync(
                repository: Arg.Any<string>(),
                activePullRequestNumbers: Arg.Any<IReadOnlyList<int>>(),
                activeIssueNumbers: Arg.Any<IReadOnlyList<int>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WhenIssueScanFails_DoesNotCallCloseStaleItemsAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.InternalServerError);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._workItemRepository.DidNotReceive()
            .CloseStaleItemsForRepoAsync(
                repository: Arg.Any<string>(),
                activePullRequestNumbers: Arg.Any<IReadOnlyList<int>>(),
                activeIssueNumbers: Arg.Any<IReadOnlyList<int>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithEmptyRepo_CallsCloseStaleItemsWithEmptyListsAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        WorkItemScanner scanner = this.CreateScanner();

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._workItemRepository.Received(1)
            .CloseStaleItemsForRepoAsync(
                repository: REPO,
                activePullRequestNumbers: Arg.Is<IReadOnlyList<int>>(l => l.Count == 0),
                activeIssueNumbers: Arg.Is<IReadOnlyList<int>>(l => l.Count == 0),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ScanAsync_WithFilteredOutPr_StillIncludesItInActiveListForReconciliationAsync()
    {
        using HttpClient repoClient = CreateClient(HttpStatusCode.OK, USER_REPOS_JSON);
        using HttpClient prClient = CreateClient(HttpStatusCode.OK, PR_WITH_UNRELATED_LABEL_JSON);
        using HttpClient issueClient = CreateClient(HttpStatusCode.OK, EMPTY_JSON);
        this._httpClientFactory.CreateClient("GitHub").Returns(repoClient, prClient, issueClient);

        GitHubOptions options = new() { Filter = new GitHubFilterOptions { LabelFilter = ["AI-Work"] } };

        WorkItemScanner scanner = this.CreateScanner(options);

        await scanner.ScanAsync(this.CancellationToken());

        await this
            ._workItemRepository.Received(1)
            .CloseStaleItemsForRepoAsync(
                repository: REPO,
                activePullRequestNumbers: Arg.Is<IReadOnlyList<int>>(l => l.Count == 1 && l[0] == 6),
                activeIssueNumbers: Arg.Is<IReadOnlyList<int>>(l => l.Count == 0),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }
}
