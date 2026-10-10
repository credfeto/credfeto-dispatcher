using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests;

public sealed class GitHubLoginValidatorTests : TestBase
{
    private const string MAX_LENGTH_LOGIN = "abcdefghijklmnopqrstuvwxyz0123456789abc";

    [Theory]
    [InlineData("octocat")]
    [InlineData("a")]
    [InlineData("a-b")]
    [InlineData("Octo-Cat-42")]
    [InlineData(MAX_LENGTH_LOGIN)]
    public static void IsValidAcceptsWellFormedLogins(string login)
    {
        Assert.True(condition: GitHubLoginValidator.IsValid(login), userMessage: $"'{login}' should be a valid login");
    }

    [Theory]
    [InlineData("")]
    [InlineData("-a")]
    [InlineData("a-")]
    [InlineData("a--b")]
    [InlineData("a_b")]
    [InlineData("a b")]
    [InlineData("a/b")]
    [InlineData(MAX_LENGTH_LOGIN + "d")]
    public static void IsValidRejectsMalformedLogins(string login)
    {
        Assert.False(
            condition: GitHubLoginValidator.IsValid(login),
            userMessage: $"'{login}' should not be a valid login"
        );
    }
}
