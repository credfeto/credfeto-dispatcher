using System.Collections.Generic;
using Credfeto.Dispatcher.GitHub.DataTypes;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests;

public sealed class LinkedIssueTrustTests : TestBase
{
    private const string AUTHOR = "octocat";
    private const string LABEL = "ai";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public static void FindTrustedLinkedIssueReturnsNullWhenAuthorIsMissing(string? author)
    {
        LinkedItem? result = LinkedIssueTrust.FindTrustedLinkedIssue(
            author: author,
            commitAuthors: [AUTHOR],
            linkedItems: [new LinkedItem(Number: 1, Labels: [LABEL], Assignees: [AUTHOR])],
            activeLabelFilter: [LABEL]
        );

        Assert.Null(result);
    }

    [Fact]
    public static void FindTrustedLinkedIssueReturnsNullWhenThereAreNoCommitAuthors()
    {
        LinkedItem? result = LinkedIssueTrust.FindTrustedLinkedIssue(
            author: AUTHOR,
            commitAuthors: [],
            linkedItems: [new LinkedItem(Number: 1, Labels: [LABEL], Assignees: [AUTHOR])],
            activeLabelFilter: [LABEL]
        );

        Assert.Null(result);
    }

    [Fact]
    public static void ActiveLabelFilterRemovesBlankEntries()
    {
        IReadOnlyList<string> result = LinkedIssueTrust.ActiveLabelFilter([LABEL, "", "   ", "other"]);

        Assert.Equal(expected: [LABEL, "other"], actual: result);
    }

    [Fact]
    public static void ActiveLabelFilterOfOnlyBlankEntriesIsEmpty()
    {
        IReadOnlyList<string> result = LinkedIssueTrust.ActiveLabelFilter(["", " "]);

        Assert.Empty(result);
    }

    [Fact]
    public static void PassesLabelFilterWhenActiveFilterIsEmpty()
    {
        Assert.True(
            LinkedIssueTrust.PassesLabelFilter(labels: [], activeLabelFilter: []),
            userMessage: "An empty active label filter should pass every item"
        );
    }

    [Fact]
    public static void PassesLabelFilterWhenALabelMatches()
    {
        Assert.True(
            LinkedIssueTrust.PassesLabelFilter(labels: ["bug", LABEL], activeLabelFilter: [LABEL]),
            userMessage: "A label matching the active filter should pass"
        );
    }

    [Fact]
    public static void FailsLabelFilterWhenNoLabelMatches()
    {
        Assert.False(
            LinkedIssueTrust.PassesLabelFilter(labels: ["bug"], activeLabelFilter: [LABEL]),
            userMessage: "No label matching the active filter should fail"
        );
    }
}
