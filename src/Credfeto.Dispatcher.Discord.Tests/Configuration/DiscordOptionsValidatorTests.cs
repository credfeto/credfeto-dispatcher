using System;
using Credfeto.Dispatcher.Discord.Configuration;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using Xunit;

namespace Credfeto.Dispatcher.Discord.Tests.Configuration;

public sealed class DiscordOptionsValidatorTests : TestBase
{
    private readonly DiscordOptionsValidator _validator;

    public DiscordOptionsValidatorTests()
    {
        this._validator = new DiscordOptionsValidator();
    }

    [Fact]
    public void ValidationSucceedsWhenWebhookUrlIsSet()
    {
        DiscordOptions options = new() { WebhookUrl = new Uri("https://discord.com/api/webhooks/123/token") };

        ValidateOptionsResult result = this._validator.Validate(name: null, options: options);

        Assert.Equal(expected: ValidateOptionsResult.Success, actual: result);
    }

    [Fact]
    public void ValidationFailsWhenWebhookUrlIsNull()
    {
        DiscordOptions options = new();

        ValidateOptionsResult result = this._validator.Validate(name: null, options: options);

        Assert.NotEqual(expected: ValidateOptionsResult.Success, actual: result);
    }
}
