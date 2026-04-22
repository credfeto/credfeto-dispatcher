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

## Setup

### 1. GitHub Personal Access Token

The dispatcher only needs **read access to your notifications**. No repository write permissions are required — it works on any repo you can see.

#### Fine-grained PAT (recommended)

Create at https://github.com/settings/tokens?type=beta

| Section | Setting | Value |
|---------|---------|-------|
| Resource owner | Your account or org | |
| Repository access | Public repositories (read-only) | Sufficient for most cases |
| **Account permissions** | **Notifications** | **Read-only** ← required |

No repository-level permissions are needed beyond the above.

#### Classic PAT (alternative)

Create at https://github.com/settings/tokens

Required scope: **`notifications`**

> The `notifications` scope grants read-only access to your notification feed. It does not grant any write access or access to repository content.

---

### 2. Discord Webhook

1. Open Discord and go to the channel you want notifications posted in
2. **Edit Channel** → **Integrations** → **Webhooks** → **New Webhook**
3. Give it a name (e.g. `dispatcher-bot`) and copy the webhook URL
4. The URL will look like: `https://discord.com/api/webhooks/123456789/xxxx...`

> ⚠️ Keep this URL secret — anyone with it can post to your channel. Never commit it.

---

### 3. Configuration

Create `appsettings-local.json` (gitignored) next to the binary, overriding the empty placeholders in `appsettings.json`:

```json
{
  "GitHub": {
    "Token": "github_pat_..."
  },
  "Discord": {
    "WebhookUrl": "https://discord.com/api/webhooks/..."
  }
}
```

Or use environment variables (double-underscore maps to nested config):

```bash
GitHub__Token=github_pat_...
Discord__WebhookUrl=https://discord.com/api/webhooks/...
```

#### Full configuration reference

```json
{
  "GitHub": {
    "Token": "",
    "PollIntervalSeconds": 60,
    "Filter": {
      "Reasons": ["review_requested", "assign", "mention", "ci_activity"],
      "LabelFilter": ["AI-Work"],
      "NoWorkFilter": ["on-hold"],
      "AllowedOwners": [],
      "ExcludedRepos": []
    }
  },
  "Discord": {
    "WebhookUrl": ""
  }
}
```

| Field | Description |
|-------|-------------|
| `GitHub.Token` | Personal access token (fine-grained with Notifications:read, or classic with `notifications` scope) |
| `GitHub.PollIntervalSeconds` | How often to poll (default: 60). Minimum is the value returned in `X-Poll-Interval` response header |
| `Filter.Reasons` | Only dispatch notifications matching these GitHub reasons |
| `Filter.LabelFilter` | Only dispatch issue/PR notifications that have at least one of these labels |
| `Filter.NoWorkFilter` | Skip notifications where the issue/PR has any of these labels (e.g. `on-hold`) |
| `Filter.AllowedOwners` | If non-empty, only dispatch notifications from these repo owners |
| `Filter.ExcludedRepos` | Skip notifications from these repos (`owner/repo` format) |
| `Discord.WebhookUrl` | Incoming webhook URL for the target channel |

---

## Running locally

```bash
dotnet run --project src/Credfeto.Dispatcher.Server
```

## Running in Docker

The container needs outbound HTTPS access to `api.github.com` and `discord.com`. Ensure the container has external network access — by default Docker bridge networking works, but if you see `No route to host` errors check that DNS resolution and outbound port 443 are not blocked.

Pass secrets as environment variables rather than mounting config files:

```bash
docker run \
  -e GitHub__Token=github_pat_... \
  -e Discord__WebhookUrl=https://discord.com/api/webhooks/... \
  credfeto/dispatcher-bot:latest
```

## Changelog

See [CHANGELOG](CHANGELOG.md) for history

## Contributors

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->
