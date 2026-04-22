using Credfeto.Dispatcher.GitHub.Configuration;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using Xunit;

namespace Credfeto.Dispatcher.GitHub.Tests.Configuration;

public sealed class GitHubOptionsValidatorTests : TestBase
{
    private readonly GitHubOptionsValidator _validator;

    public GitHubOptionsValidatorTests()
    {
        this._validator = new GitHubOptionsValidator();
    }

    [Fact]
    public void ValidationSucceedsWithValidOptions()
    {
        GitHubOptions options = new() { Token = "valid-token", PollIntervalSeconds = 60 };

        ValidateOptionsResult result = this._validator.Validate(name: null, options: options);

        Assert.Equal(expected: ValidateOptionsResult.Success, actual: result);
    }

    [Fact]
    public void ValidationFailsWhenTokenIsEmpty()
    {
        GitHubOptions options = new() { Token = string.Empty, PollIntervalSeconds = 60 };

        ValidateOptionsResult result = this._validator.Validate(name: null, options: options);

        Assert.NotEqual(expected: ValidateOptionsResult.Success, actual: result);
    }

    [Fact]
    public void ValidationFailsWhenTokenIsWhitespace()
    {
        GitHubOptions options = new() { Token = "   ", PollIntervalSeconds = 60 };

        ValidateOptionsResult result = this._validator.Validate(name: null, options: options);

        Assert.NotEqual(expected: ValidateOptionsResult.Success, actual: result);
    }

    [Fact]
    public void ValidationFailsWhenPollIntervalIsLessThan30Seconds()
    {
        GitHubOptions options = new() { Token = "valid-token", PollIntervalSeconds = 29 };

        ValidateOptionsResult result = this._validator.Validate(name: null, options: options);

        Assert.NotEqual(expected: ValidateOptionsResult.Success, actual: result);
    }

    [Fact]
    public void ValidationSucceedsWhenPollIntervalIsExactly30Seconds()
    {
        GitHubOptions options = new() { Token = "valid-token", PollIntervalSeconds = 30 };

        ValidateOptionsResult result = this._validator.Validate(name: null, options: options);

        Assert.Equal(expected: ValidateOptionsResult.Success, actual: result);
    }

    [Fact]
    public void ValidationFailsWhenTokenIsDefaultEmpty()
    {
        GitHubOptions options = new() { PollIntervalSeconds = 60 };

        ValidateOptionsResult result = this._validator.Validate(name: null, options: options);

        Assert.NotEqual(expected: ValidateOptionsResult.Success, actual: result);
    }
}
