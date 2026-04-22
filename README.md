# credfeto-dispatcher

GitHub notification dispatcher — polls the GitHub Notifications API and routes relevant events to Discord.

[![Build Status](https://github.com/credfeto/credfeto-dispatcher/actions/workflows/build-and-publish-pre-release.yml/badge.svg)](https://github.com/credfeto/credfeto-dispatcher/actions/workflows/build-and-publish-pre-release.yml)
[![Last Release](https://github.com/credfeto/credfeto-dispatcher/actions/workflows/build-and-publish-release.yml/badge.svg)](https://github.com/credfeto/credfeto-dispatcher/actions/workflows/build-and-publish-release.yml)

## Vision

`credfeto-dispatcher` bridges GitHub and Discord without requiring webhook access or write permissions on any watched repository. It works by:

1. **Polling** the GitHub Notifications API every 60 seconds using ETag caching (304 Not Modified when nothing changes — effectively zero cost when idle)
2. **Filtering** notifications for events that need attention: review requests, CI failures on owned PRs, `AI-Work` labelled issues, Copilot review completions
3. **Dispatching** formatted messages to a configured Discord channel via an incoming webhook or bot

Because it uses only the GitHub REST Notifications API with a personal access token, it works on **any repo** — repos you own, repos you contribute to, and repos you only watch — without needing to configure webhooks or GitHub Actions on those repos.

## Architecture

```
Credfeto.Dispatcher.Server          - .NET Generic Host entry point
Credfeto.Dispatcher.GitHub          - GitHub Notifications API polling + ETag state
Credfeto.Dispatcher.GitHub.Interfaces - IGitHubNotificationPoller, INotificationFilter
Credfeto.Dispatcher.GitHub.DataTypes  - Notification, NotificationSubject, Repository
Credfeto.Dispatcher.Discord         - Discord webhook/bot outbound client
Credfeto.Dispatcher.Discord.Interfaces - IDiscordDispatcher
Credfeto.Dispatcher.Discord.DataTypes  - DiscordMessage, DiscordEmbed
Credfeto.Dispatcher.Shared          - Shared base types and utilities
```

## Configuration

Set the following in `appsettings.json` or environment variables:

```json
{
  "GitHub": {
    "Token": "[GitHub personal access token with notifications:read scope]",
    "PollIntervalSeconds": 60,
    "Filter": {
      "Reasons": ["review_requested", "assign", "mention", "ci_activity"],
      "LabelFilter": "AI-Work"
    }
  },
  "Discord": {
    "WebhookUrl": "[Discord incoming webhook URL]",
    "NotificationsChannelWebhookUrl": "[Optional: separate channel for raw notifications]"
  }
}
```

## Running locally

1. Create a GitHub personal access token with `notifications` scope at https://github.com/settings/tokens
2. Create a Discord incoming webhook in your server's channel settings (Edit Channel → Integrations → Webhooks)
3. Configure `appsettings-local.json` (gitignored) with your tokens
4. Run `dotnet run --project src/Credfeto.Dispatcher.Server`

## Changelog

See [CHANGELOG](CHANGELOG.md) for history

## Contributors

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->
