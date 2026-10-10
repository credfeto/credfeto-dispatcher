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
            labelFilter: [LABEL]
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
            labelFilter: [LABEL]
        );

        Assert.Null(result);
    }
}
