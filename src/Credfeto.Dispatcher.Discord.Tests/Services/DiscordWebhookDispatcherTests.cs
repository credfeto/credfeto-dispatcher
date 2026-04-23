using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Discord.Configuration;
using Credfeto.Dispatcher.Discord.DataTypes;
using Credfeto.Dispatcher.Discord.Interfaces;
using Credfeto.Dispatcher.Discord.Services;
using FunFair.Test.Common;
using FunFair.Test.Common.Extensions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Credfeto.Dispatcher.Discord.Tests.Services;

public sealed class DiscordWebhookDispatcherTests : TestBase
{
    private static readonly Uri WebhookUrl = new("https://discord.com/api/webhooks/1234567890/test-token");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDiscordDispatcher _dispatcher;

    public DiscordWebhookDispatcherTests()
    {
        this._httpClientFactory = GetSubstitute<IHttpClientFactory>();

        DiscordOptions options = new() { WebhookUrl = WebhookUrl };
        this._dispatcher = new DiscordWebhookDispatcher(httpClientFactory: this._httpClientFactory, options: Options.Create(options), logger: this.GetTypedLogger<DiscordWebhookDispatcher>());
    }

    [Fact]
    public async Task SendAsyncDoesNotCallHttpClientWhenWebhookUrlIsNullAsync()
    {
        DiscordOptions optionsWithNoWebhook = new() { WebhookUrl = null };
        IDiscordDispatcher dispatcher = new DiscordWebhookDispatcher(httpClientFactory: this._httpClientFactory, options: Options.Create(optionsWithNoWebhook), logger: this.GetTypedLogger<DiscordWebhookDispatcher>());

        DiscordMessage message = new(Content: "Hello", Embeds: []);

        await dispatcher.SendAsync(message: message, cancellationToken: this.CancellationToken());

        _ = this._httpClientFactory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Fact]
    public async Task SendAsyncCallsDiscordHttpClientWhenWebhookUrlIsSetAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "Discord", httpStatusCode: HttpStatusCode.NoContent);

        DiscordMessage message = new(Content: "Hello", Embeds: []);

        await this._dispatcher.SendAsync(message: message, cancellationToken: this.CancellationToken());

        _ = this._httpClientFactory.Received(1).CreateClient("Discord");
    }

    [Fact]
    public async Task SendAsyncSucceedsWithContentOnlyMessageAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "Discord", httpStatusCode: HttpStatusCode.NoContent);

        DiscordMessage message = new(Content: "Hello, world!", Embeds: []);

        await this._dispatcher.SendAsync(message: message, cancellationToken: this.CancellationToken());
    }

    [Fact]
    public async Task SendAsyncSucceedsWithEmbedsAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "Discord", httpStatusCode: HttpStatusCode.NoContent);

        DiscordEmbed embed = new(Title: "Test Title", Description: "Test Description", Url: new Uri("https://example.com"), Color: 0xFF0000);
        DiscordMessage message = new(Content: "Hello", Embeds: [embed]);

        await this._dispatcher.SendAsync(message: message, cancellationToken: this.CancellationToken());
    }

    [Fact]
    public async Task SendAsyncSucceedsWithOkResponseAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "Discord", httpStatusCode: HttpStatusCode.OK);

        DiscordMessage message = new(Content: "Hello", Embeds: []);

        await this._dispatcher.SendAsync(message: message, cancellationToken: this.CancellationToken());
    }

    [Fact]
    public Task SendAsyncThrowsWhenHttpResponseIndicatesFailureAsync()
    {
        this._httpClientFactory.MockCreateClientWithResponse(clientName: "Discord", httpStatusCode: HttpStatusCode.InternalServerError);

        DiscordMessage message = new(Content: "Hello", Embeds: []);

        return Assert.ThrowsAsync<HttpRequestException>(() => this._dispatcher.SendAsync(message: message, cancellationToken: this.CancellationToken()).AsTask());
    }
}
