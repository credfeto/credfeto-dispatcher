using System;
using System.Collections.Generic;
using Credfeto.Dispatcher.GitHub.DataTypes;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.Storage.Tests;

public sealed class NotificationDetailMappingTests : TestBase
{
    private static readonly string[] NoAuthors = [];
    private static readonly string[] OneAuthor = ["octocat"];
    private static readonly string[] DuplicateAuthors = ["octocat", "OctoCat", "someone-else"];

    public static TheoryData<IReadOnlyList<string>, string?> BuildCommitAuthorsCases() =>
        new()
        {
            { NoAuthors, null },
            { OneAuthor, "octocat" },
            { DuplicateAuthors, "octocat,someone-else" },
        };

    [Theory]
    [MemberData(nameof(BuildCommitAuthorsCases))]
    public void BuildCommitAuthors_ReturnsNullForUnknownAndCsvOtherwise(
        IReadOnlyList<string> commitAuthors,
        string? expected
    )
    {
        string? result = NotificationDetailMapping.BuildCommitAuthors(commitAuthors);

        Assert.Equal(expected: expected, actual: result);
    }

    [Fact]
    public void BuildFailedCheckNames_ReturnsNullWhenNoChecksFailed()
    {
        PullRequestRun[] runs =
        [
            CreateRun(name: "build", conclusion: "success"),
            CreateRun(name: "lint", conclusion: null),
        ];

        Assert.Null(NotificationDetailMapping.BuildFailedCheckNames(runs));
    }

    [Fact]
    public void BuildFailedCheckNames_SerialisesFailedNamesAsJsonArray()
    {
        PullRequestRun[] runs =
        [
            CreateRun(name: "build (ubuntu-latest, net8.0)", conclusion: "failure"),
            CreateRun(name: "lint", conclusion: "success"),
            CreateRun(name: "test", conclusion: "timed_out"),
        ];

        string? result = NotificationDetailMapping.BuildFailedCheckNames(runs);

        Assert.Equal(expected: "[\"build (ubuntu-latest, net8.0)\",\"test\"]", actual: result);
    }

    [Fact]
    public void KnownCommitAuthors_ReturnsNullForEmptyList()
    {
        Assert.Null(NotificationDetailMapping.KnownCommitAuthors(NoAuthors));
    }

    [Fact]
    public void KnownCommitAuthors_ReturnsListWhenNotEmpty()
    {
        Assert.Same(expected: OneAuthor, actual: NotificationDetailMapping.KnownCommitAuthors(OneAuthor));
    }

    [Fact]
    public void ExtractPrNumber_ReturnsNullWhenNoUrl()
    {
        Assert.Null(NotificationDetailMapping.ExtractPrNumber(null));
    }

    [Theory]
    [InlineData("https://api.github.com/repos/owner/repo/pulls/42", 42)]
    [InlineData("https://api.github.com/repos/owner/repo/pulls/42/", 42)]
    public static void ExtractPrNumber_ReturnsTheTrailingNumber(string url, int expected)
    {
        Assert.Equal(expected: expected, actual: NotificationDetailMapping.ExtractPrNumber(new Uri(url)));
    }

    [Fact]
    public void ExtractPrNumber_ReturnsNullWhenTheLastSegmentIsNotANumber()
    {
        Assert.Null(
            NotificationDetailMapping.ExtractPrNumber(new Uri("https://api.github.com/repos/owner/repo/pulls/latest"))
        );
    }

    [Fact]
    public void ComputeReviewDecision_ChangesRequestedTakesPrecedenceOverApproval()
    {
        PullRequestReview[] reviews = [CreateReview(state: "APPROVED"), CreateReview(state: "changes_requested")];

        Assert.Equal(expected: "ChangesRequested", actual: NotificationDetailMapping.ComputeReviewDecision(reviews));
    }

    [Fact]
    public void ComputeReviewDecision_ReturnsApprovedWhenOnlyApproved()
    {
        PullRequestReview[] reviews = [CreateReview(state: "COMMENTED"), CreateReview(state: "approved")];

        Assert.Equal(expected: "Approved", actual: NotificationDetailMapping.ComputeReviewDecision(reviews));
    }

    [Fact]
    public void ComputeReviewDecision_ReturnsNullWhenNeitherApprovedNorChangesRequested()
    {
        PullRequestReview[] reviews = [CreateReview(state: "COMMENTED")];

        Assert.Null(NotificationDetailMapping.ComputeReviewDecision(reviews));
    }

    [Fact]
    public void BuildFailedCheckSha_ReturnsNullWhenNoChecksFailed()
    {
        PullRequestRun[] runs = [CreateRun(name: "build", conclusion: "success")];

        Assert.Null(NotificationDetailMapping.BuildFailedCheckSha(runs));
    }

    [Fact]
    public void BuildFailedCheckSha_ReturnsTheShaOfTheFirstFailedCheck()
    {
        PullRequestRun[] runs =
        [
            CreateRun(name: "build", conclusion: "success", headSha: "aaa111"),
            CreateRun(name: "test", conclusion: "failure", headSha: "def456"),
            CreateRun(name: "lint", conclusion: "error", headSha: "DEF456"),
        ];

        Assert.Equal(expected: "def456", actual: NotificationDetailMapping.BuildFailedCheckSha(runs));
    }

    private static PullRequestReview CreateReview(string state)
    {
        return new(
            Author: "reviewer",
            State: state,
            Body: null,
            Url: new("https://github.com/owner/repo/pull/1#pullrequestreview-1"),
            SubmittedAt: DateTimeOffset.UnixEpoch
        );
    }

    private static PullRequestRun CreateRun(string name, string? conclusion, string headSha = "abc123")
    {
        return new(
            Name: name,
            Status: "completed",
            Conclusion: conclusion,
            Url: new("https://github.com/owner/repo/actions/runs/1"),
            IsRequired: true,
            HeadSha: headSha
        );
    }
}
