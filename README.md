# credfeto-dispatcher

GitHub notification dispatcher — polls the GitHub Notifications API and keeps the local work-item state up to date.

[![Build Status](https://github.com/credfeto/credfeto-dispatcher/actions/workflows/build-and-publish-pre-release.yml/badge.svg)](https://github.com/credfeto/credfeto-dispatcher/actions/workflows/build-and-publish-pre-release.yml)
[![Last Release](https://github.com/credfeto/credfeto-dispatcher/actions/workflows/build-and-publish-release.yml/badge.svg)](https://github.com/credfeto/credfeto-dispatcher/actions/workflows/build-and-publish-release.yml)

## Vision

`credfeto-dispatcher` monitors GitHub notifications without requiring webhook access or write permissions on any watched repository. It works by:

1. **Polling** the GitHub Notifications API every 60 seconds using ETag caching (304 Not Modified when nothing changes — effectively zero cost when idle)
2. **Filtering** notifications for repositories and owners that should be tracked
3. **Updating** the local database state for pull requests and issues so the prioritised work-item API stays current

Because it uses only the GitHub REST Notifications API with a personal access token, it works on **any repo** — repos you own, repos you contribute to, and repos you only watch — without needing to configure webhooks or GitHub Actions on those repos.

## Architecture

```text
Credfeto.Dispatcher.Server            - .NET Generic Host entry point
Credfeto.Dispatcher.GitHub            - GitHub Notifications API polling + work-item state updates
Credfeto.Dispatcher.GitHub.Interfaces - GitHub service interfaces
Credfeto.Dispatcher.GitHub.DataTypes  - Notification, NotificationSubject, Repository
Credfeto.Dispatcher.Storage           - SQL storage, migrations, and state tracking
Credfeto.Dispatcher.Shared            - Shared base types and utilities
```

## Configuration

Set the following in `appsettings.json` or environment variables:

```json
{
  "GitHub": {
    "Token": "[GitHub personal access token with notifications scope]",
    "ApiBaseUrl": "https://api.github.com/",
    "PollIntervalSeconds": 60,
    "Filter": {
      "LabelFilter": ["AI-Work"],
      "NoWorkFilter": ["on-hold"],
      "AllowedOwners": [],
      "AllowedRepos": [],
      "ExcludedRepos": []
    }
  }
}
```

| Setting | Type | Description |
| --- | --- | --- |
| `GitHub:Token` | string | GitHub personal access token used to poll notifications. |
| `GitHub:ApiBaseUrl` | string | Base URL for the GitHub API. Defaults to `https://api.github.com/`. Set to a proxy URL to route API calls through an intermediary. Must be a valid absolute URI. |
| `GitHub:PollIntervalSeconds` | integer | Poll interval for the notifications API. |
| `GitHub:Filter:LabelFilter` | string[] | Labels used by prioritisation logic. |
| `GitHub:Filter:NoWorkFilter` | string[] | Labels that mark a work item as on hold. |
| `GitHub:Filter:AllowedOwners` | string[] | Optional owner allow-list for notifications to process. |
| `GitHub:Filter:AllowedRepos` | string[] | Optional repository allow-list for notifications to process. |
| `GitHub:Filter:ExcludedRepos` | string[] | Repository deny-list that is always ignored. |

## API

### `GET /priorities`

Returns the current prioritised work item list with freshness metadata.

```json
{
  "as_of": "2026-05-13T14:32:18Z",
  "lag_seconds": 47,
  "priorities": [
    {
      "repository": "owner/repo",
      "id": 123,
      "itemType": "PullRequest",
      "priority": "Urgent",
      "firstSeen": "2026-05-10T09:00:00Z",
      "lastUpdated": "2026-05-13T14:31:31Z",
      "status": "Open",
      "whenClosed": null,
      "isOnHold": false,
      "linkedPrNumbers": [],
      "commentCount": 4,
      "reviewDecision": "Approved",
      "failedCheckCount": 0,
      "failedCheckNames": [],
      "failedCheckSha": null,
      "author": "dependabot[bot]"
    }
  ]
}
```

| Field | Type | Description |
| --- | --- | --- |
| `as_of` | ISO-8601 UTC | Timestamp of the most recent ingest that contributed to this snapshot |
| `lag_seconds` | integer | `now − as_of` in whole seconds; higher values indicate a staler snapshot |
| `priorities` | array | Ordered work items (PRs before issues; by owner, repo, priority, age) |

### `GET /ping`

Lightweight health check — returns `{"value":"Pong!"}` without touching the database.

## Running locally

1. Create a GitHub personal access token with `notifications` scope at [github.com/settings/tokens](https://github.com/settings/tokens)
2. Configure `appsettings-local.json` (gitignored) with your token and database connection string
3. Run `dotnet run --project src/Credfeto.Dispatcher.Server`

## Changelog

See [CHANGELOG](CHANGELOG.md) for history

## Contributors

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->
