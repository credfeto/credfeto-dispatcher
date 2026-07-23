using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.GitHub.Interfaces;
using Credfeto.Dispatcher.GitHub.Models;

namespace Credfeto.Dispatcher.GitHub.Services;

public sealed class PullRequestDetailFetcher : IPullRequestDetailFetcher
{
    private const string PULL_REQUEST_TYPE = "PullRequest";
    private const int MAX_BODY_LENGTH = 300;

    private const string PULL_REQUEST_QUERY = """
        query PullRequestDetails($owner: String!, $repo: String!, $number: Int!) {
          repository(owner: $owner, name: $repo) {
            pullRequest(number: $number) {
              number
              title
              state
              isDraft
              url
              body
              headRefOid
              author {
                login
              }
              baseRef {
                name
              }
              assignees(first: 10) {
                nodes {
                  login
                }
              }
              labels(first: 10) {
                nodes {
                  name
                }
              }
              comments(last: 100) {
                nodes {
                  body
                  author {
                    login
                  }
                  url
                  createdAt
                }
              }
              reviews(last: 50) {
                nodes {
                  state
                  body
                  author {
                    login
                  }
                  url
                  submittedAt
                }
              }
              commits(last: 100) {
                nodes {
                  commit {
                    author {
                      user {
                        login
                      }
                    }
                  }
                }
              }
              closingIssuesReferences(first: 10) {
                nodes {
                  number
                  labels(first: 10) {
                    nodes {
                      name
                    }
                  }
                  assignees(first: 10) {
                    nodes {
                      login
                    }
                  }
                }
              }
            }
          }
        }
        """;

    private readonly IHttpClientFactory _httpClientFactory;

    public PullRequestDetailFetcher(IHttpClientFactory httpClientFactory)
    {
        this._httpClientFactory = httpClientFactory;
    }

    public async ValueTask<PullRequestDetails?> FetchAsync(
        GitHubNotification notification,
        CancellationToken cancellationToken
    )
    {
        if (
            !string.Equals(
                a: notification.Subject.Type,
                b: PULL_REQUEST_TYPE,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return null;
        }

        if (notification.Subject.Url is null)
        {
            return null;
        }

        GraphQlPullRequestData? pr = await this.FetchPullRequestViaGraphQlAsync(
            subjectUrl: notification.Subject.Url,
            cancellationToken: cancellationToken
        );

        if (pr is null)
        {
            return null;
        }

        string repoFullName = notification.Repository.FullName;
        string baseBranchName = pr.BaseRef?.Name ?? string.Empty;

        IReadOnlyList<PullRequestRun> runs = await this.FetchRunsAsync(
            repoFullName: repoFullName,
            headSha: pr.HeadRefOid,
            baseBranchName: baseBranchName,
            cancellationToken: cancellationToken
        );

        return BuildPullRequestDetails(notification: notification, pr: pr, repoFullName: repoFullName, runs: runs);
    }

    private static PullRequestDetails BuildPullRequestDetails(
        GitHubNotification notification,
        GraphQlPullRequestData pr,
        string repoFullName,
        IReadOnlyList<PullRequestRun> runs
    )
    {
        string[] repoParts = repoFullName.Split('/');
        ItemRepository repository = new(Owner: repoParts[0], Name: repoParts[1], Url: notification.Repository.Url);
        LastNotification lastNotification = new(Id: notification.Id, Timestamp: notification.UpdatedAt);

        return new PullRequestDetails(
            Number: pr.Number,
            Title: pr.Title,
            Status: DetermineStatus(pr),
            HtmlUrl: new Uri(pr.Url),
            Assignees: [.. pr.Assignees?.Nodes?.Select(u => u.Login) ?? []],
            Labels: [.. pr.Labels?.Nodes?.Select(l => l.Name) ?? []],
            Body: pr.Body,
            Comments: ExtractComments(pr),
            Reviews: ExtractReviews(pr),
            Runs: runs,
            LinkedItems: ExtractLinkedItems(pr),
            Repository: repository,
            LastNotification: lastNotification,
            Author: pr.Author?.Login,
            CommitAuthors: ExtractCommitAuthors(pr)
        );
    }

    private async ValueTask<GraphQlPullRequestData?> FetchPullRequestViaGraphQlAsync(
        Uri subjectUrl,
        CancellationToken cancellationToken
    )
    {
        (string owner, string repo, int number) = ParsePullRequestUrl(subjectUrl);

        GraphQlRequest request = new(
            Query: PULL_REQUEST_QUERY,
            Variables: new GraphQlVariables(Owner: owner, Repo: repo, Number: number)
        );

        HttpClient httpClient = this._httpClientFactory.CreateClient("GitHub");

        using HttpRequestMessage message = new(method: HttpMethod.Post, requestUri: "graphql")
        {
            Content = JsonContent.Create(
                inputValue: request,
                jsonTypeInfo: NotificationSerializerContext.Default.GraphQlRequest
            ),
        };

        using HttpResponseMessage response = await httpClient.SendAsync(
            request: message,
            cancellationToken: cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        GraphQlResponse? gqlResponse = JsonSerializer.Deserialize(
            json: json,
            jsonTypeInfo: NotificationSerializerContext.Default.GraphQlResponse
        );

        return gqlResponse?.Data?.Repository?.PullRequest;
    }

    private async ValueTask<IReadOnlyList<PullRequestRun>> FetchRunsAsync(
        string repoFullName,
        string headSha,
        string baseBranchName,
        CancellationToken cancellationToken
    )
    {
        string runsUrl = $"repos/{repoFullName}/actions/runs?head_sha={headSha}&per_page=100";
        ApiWorkflowRunsResponse? runsResponse = await this.GetAsync<ApiWorkflowRunsResponse>(
            url: runsUrl,
            jsonTypeInfo: NotificationSerializerContext.Default.ApiWorkflowRunsResponse,
            cancellationToken: cancellationToken
        );

        if (runsResponse?.WorkflowRuns is not { Count: > 0 })
        {
            return [];
        }

        HashSet<string> requiredCheckNames = await this.FetchRequiredCheckNamesAsync(
            repoFullName: repoFullName,
            branchName: baseBranchName,
            cancellationToken: cancellationToken
        );

        return
        [
            .. runsResponse.WorkflowRuns.Select(r => new PullRequestRun(
                Name: r.Name,
                Status: r.Status,
                Conclusion: r.Conclusion,
                Url: new Uri(r.HtmlUrl),
                IsRequired: requiredCheckNames.Contains(r.Name),
                HeadSha: r.HeadSha
            )),
        ];
    }

    private async ValueTask<HashSet<string>> FetchRequiredCheckNamesAsync(
        string repoFullName,
        string branchName,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(branchName))
        {
            return [];
        }

        string url =
            $"repos/{repoFullName}/branches/{Uri.EscapeDataString(branchName)}/protection/required_status_checks";
        ApiRequiredStatusChecks? checks = await this.GetAsync<ApiRequiredStatusChecks>(
            url: url,
            jsonTypeInfo: NotificationSerializerContext.Default.ApiRequiredStatusChecks,
            cancellationToken: cancellationToken
        );

        if (checks?.Checks is null)
        {
            return [];
        }

        return [.. checks.Checks.Select(c => c.Context)];
    }

    private static IReadOnlyList<PullRequestComment> ExtractComments(GraphQlPullRequestData pr)
    {
        if (pr.Comments?.Nodes is null)
        {
            return [];
        }

        return
        [
            .. pr.Comments.Nodes.Select(c => new PullRequestComment(
                Author: c.Author?.Login ?? string.Empty,
                Body: TruncateBody(c.Body),
                Url: new Uri(c.Url),
                CreatedAt: c.CreatedAt
            )),
        ];
    }

    private static IReadOnlyList<PullRequestReview> ExtractReviews(GraphQlPullRequestData pr)
    {
        if (pr.Reviews?.Nodes is null)
        {
            return [];
        }

        return
        [
            .. pr.Reviews.Nodes.Select(r => new PullRequestReview(
                Author: r.Author?.Login ?? string.Empty,
                State: r.State,
                Body: r.Body is not null ? TruncateBody(r.Body) : null,
                Url: new Uri(r.Url),
                SubmittedAt: r.SubmittedAt
            )),
        ];
    }

    private static IReadOnlyList<LinkedItem> ExtractLinkedItems(GraphQlPullRequestData pr)
    {
        if (pr.ClosingIssuesReferences?.Nodes is not { Count: > 0 } nodes)
        {
            return [];
        }

        return
        [
            .. nodes.Select(node => new LinkedItem(
                Number: node.Number,
                Labels: [.. node.Labels?.Nodes?.Select(l => l.Name) ?? []],
                Assignees: [.. node.Assignees?.Nodes?.Select(a => a.Login) ?? []]
            )),
        ];
    }

    private static IReadOnlyList<string> ExtractCommitAuthors(GraphQlPullRequestData pr)
    {
        if (pr.Commits?.Nodes is not { Count: > 0 } nodes)
        {
            return [];
        }

        return
        [
            .. nodes
                .Select(n => n.Commit?.Author?.User?.Login)
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static string DetermineStatus(GraphQlPullRequestData pr)
    {
        if (
            string.Equals(a: pr.State, b: "CLOSED", comparisonType: StringComparison.OrdinalIgnoreCase)
            || string.Equals(a: pr.State, b: "MERGED", comparisonType: StringComparison.OrdinalIgnoreCase)
        )
        {
            return "Closed";
        }

        return pr.IsDraft ? "Draft" : "Open";
    }

    private static (string owner, string repo, int number) ParsePullRequestUrl(Uri url)
    {
        string[] segments = url.AbsolutePath.Split('/');

        return (segments[2], segments[3], int.Parse(segments[5], CultureInfo.InvariantCulture));
    }

    private static string TruncateBody(string body)
    {
        return body.Length <= MAX_BODY_LENGTH ? body : body[..MAX_BODY_LENGTH] + "…";
    }

    private async ValueTask<T?> GetAsync<T>(
        string url,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken
    )
        where T : class
    {
        HttpClient httpClient = this._httpClientFactory.CreateClient("GitHub");

        using HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: url);
        using HttpResponseMessage response = await httpClient.SendAsync(
            request: request,
            cancellationToken: cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonSerializer.Deserialize(json: json, jsonTypeInfo: jsonTypeInfo);
    }
}
