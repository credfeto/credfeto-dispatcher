using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests;

public sealed class GitHubRepositoryNameValidatorTests : TestBase
{
    private const string MAX_LENGTH_NAME =
        "abcdefghijklmnopqrstuvwxyz0123456789abcdefghijklmnopqrstuvwxyz0123456789abcdefghijklmnopqrstuvwxyz01";

    [Theory]
    [InlineData("credfeto-dispatcher")]
    [InlineData("a")]
    [InlineData("repo.name")]
    [InlineData("repo_name")]
    [InlineData(".github")]
    [InlineData("Repo-42")]
    [InlineData(MAX_LENGTH_NAME)]
    public static void IsValidAcceptsWellFormedNames(string name)
    {
        Assert.True(
            condition: GitHubRepositoryNameValidator.IsValid(name),
            userMessage: $"'{name}' should be a valid repository name"
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a b")]
    [InlineData("a/b")]
    [InlineData("a'b")]
    [InlineData("a%b")]
    [InlineData(MAX_LENGTH_NAME + "2")]
    public static void IsValidRejectsMalformedNames(string name)
    {
        Assert.False(
            condition: GitHubRepositoryNameValidator.IsValid(name),
            userMessage: $"'{name}' should not be a valid repository name"
        );
    }
}
