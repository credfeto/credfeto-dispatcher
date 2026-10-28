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

    private static PullRequestRun CreateRun(string name, string? conclusion)
    {
        return new(
            Name: name,
            Status: "completed",
            Conclusion: conclusion,
            Url: new("https://github.com/owner/repo/actions/runs/1"),
            IsRequired: true,
            HeadSha: "abc123"
        );
    }
}
