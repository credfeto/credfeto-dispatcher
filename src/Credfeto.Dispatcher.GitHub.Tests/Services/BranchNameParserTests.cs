using Credfeto.Dispatcher.GitHub.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Services;

public sealed class BranchNameParserTests : TestBase
{
    [Theory]
    [InlineData("fix/292-feed-continuous-refresh", 292)]
    [InlineData("feat/6-add-thing", 6)]
    [InlineData("292-quick-fix", 292)]
    [InlineData("292", 292)]
    [InlineData("issue-292-thing", 292)]
    [InlineData("Issue_292", 292)]
    [InlineData("fix/issue-17", 17)]
    [InlineData("users/mark/41_tidy", 41)]
    [InlineData("issues/88", 88)]
    public static void TryGetIssueNumber_WithANumberedBranch_FindsTheNumber(string branch, int expected)
    {
        bool found = BranchNameParser.TryGetIssueNumber(branch: branch, number: out int number);

        Assert.True(found, userMessage: "Expected an issue number");
        Assert.Equal(expected: expected, actual: number);
    }

    [Theory]
    [InlineData("main")]
    [InlineData("feature/add-thing")]
    [InlineData("feature/v2-thing")]
    [InlineData("feature/2fa-support")]
    [InlineData("release/1.2.3")]
    [InlineData("fix/0-thing")]
    [InlineData("fix/012-thing")]
    [InlineData("fix/12345678-too-long")]
    [InlineData("fix/thing-292")]
    [InlineData("release/1.2/42-thing")]
    [InlineData("issue-abc")]
    [InlineData("")]
    public static void TryGetIssueNumber_WithoutAClearIssueNumber_FindsNothing(string branch)
    {
        bool found = BranchNameParser.TryGetIssueNumber(branch: branch, number: out int number);

        Assert.False(found, userMessage: "Expected no issue number");
        Assert.Equal(expected: 0, actual: number);
    }

    [Theory]
    [InlineData("depends/update-sdk", true)]
    [InlineData("dependabot/nuget/src/Foo-1.2.3", true)]
    [InlineData("Dependabot/github_actions/x", true)]
    [InlineData("fix/292-thing", false)]
    [InlineData("dependencies/x", false)]
    [InlineData("feature/depends/x", false)]
    public static void IsDependencyBranch_MatchesOnlyTheDependencyPrefixes(string branch, bool expected)
    {
        Assert.Equal(expected: expected, actual: BranchNameParser.IsDependencyBranch(branch));
    }
}
