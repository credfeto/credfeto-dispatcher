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
      "ExcludedRepos": [],
      "BoostedRepos": [],
      "MaxIssues": 10
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
| `GitHub:Filter:BoostedRepos` | string[] | Ordered `owner/repo` list. Matching items sort to the front of their band (Security/Urgent PRs, other PRs, Security/Urgent issues, other issues) in `/priorities`, in list order. Ordering only; does not bypass `AllowedOwners`/`AllowedRepos`/`ExcludedRepos`. |
| `GitHub:Filter:MaxIssues` | integer | Maximum number of regular (below Urgent) issues each owner gets in `/priorities`, at most one per repository. The cap applies per owner, so one owner cannot take every slot from the others. Urgent and Security issues are never capped. `0` or less means no cap. Defaults to `10`. |

When `DatabaseConfiguration:Provider` is `InMemory`, the in-memory store is periodically persisted to a JSON snapshot on disk and reloaded at startup, so a container restart doesn't mean starting from zero:

```json
{
  "Snapshot": {
    "DirectoryPath": "data",
    "FileName": "dispatcher-store-snapshot.json",
    "IntervalSeconds": 30
  }
}
```

| Setting | Type | Description |
| --- | --- | --- |
| `Snapshot:DirectoryPath` | string | Directory the snapshot file is written to and read from. If blank, defaults to `data` under the current working directory. Only relevant under `DatabaseConfiguration:Provider: InMemory`; ignored otherwise. Needs a persistent volume mounted at this path to survive anything beyond an in-process crash-restart. |
| `Snapshot:FileName` | string | Snapshot file name within `Snapshot:DirectoryPath`. Defaults to `dispatcher-store-snapshot.json`. |
| `Snapshot:IntervalSeconds` | integer | How often the snapshot writer checks for changes and, if the store has changed since the last write, saves a new snapshot. Defaults to `30`. A non-positive value falls back to `30`. |

## API

### `GET /priorities`

Returns the current prioritised work item list with freshness metadata.

Optional query parameter:

| Parameter | Description |
| --- | --- |
| `user` | GitHub login to filter for. Keeps items with no assignees, items assigned to that login (matched case-insensitively) and bot PRs adopted through `GitHub:Filter:PullRequests:AdoptionRules`, whatever their assignees. Only each item's own assignees count; a linked PR's assignees are ignored. Omit it to get every item. A value that is not a valid GitHub login returns `400 Bad Request`. |

For example, `GET /priorities?user=octocat`.

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
| `priorities` | array | Ordered work items, banded Security/Urgent PRs, other PRs, Security/Urgent issues, other issues, then within each band by boosted repo, owner, repo, priority, age |

### `POST /mcp`

A [Model Context Protocol](https://modelcontextprotocol.io/) server using the Streamable HTTP transport, so AI agents can
query the dispatcher's state as tools. It runs in stateless mode: every request stands alone, no session is kept and
`tools/list` or `tools/call` can be sent without a prior `initialize`. Like `/priorities`, it needs no authentication
and is read-only.

Requests are JSON-RPC 2.0 `POST`s with `Content-Type: application/json` and must send
`Accept: application/json, text/event-stream`; a request without both media types is rejected. The response is a
Server-Sent Events stream carrying the JSON-RPC result. Each tool returns its result as a single JSON text content
block.

| Tool | Parameters | Description |
| --- | --- | --- |
| `priorities` | `user` (optional) | The same prioritised work item list as `GET /priorities`, with the same `user` filtering. |
| `db` | `owner`, `repo`, `user` (all optional) | Every pull request and issue stored, including closed and on-hold items and items in repositories no longer being watched, ordered by repository then number. There is no paging: the whole result is returned in one response. |

`db` parameters:

| Parameter | Description |
| --- | --- |
| `owner` | Repository owner (the part of `owner/repo` before the `/`). Exact, case-insensitive match. |
| `repo` | Repository name (the part of `owner/repo` after the `/`). Exact, case-insensitive match. |
| `user` | GitHub login. Keeps items with no assignees, items assigned to that login (matched case-insensitively) and adopted bot PRs, as `GET /priorities?user=` does. |

An invalid `owner`, `repo` or `user` value returns a tool error rather than a result.

Example `db` result:

```json
{
  "pullRequests": [
    {
      "owner": "owner",
      "repo": "repo",
      "repository": "owner/repo",
      "number": 123,
      "status": "Open",
      "priority": 4,
      "isOnHold": false,
      "firstSeen": "2026-05-10T09:00:00Z",
      "lastUpdated": "2026-05-13T14:31:31Z",
      "whenClosed": null,
      "assignees": ["octocat"],
      "commentCount": 4,
      "reviewDecision": 2,
      "failedCheckCount": 0,
      "failedCheckNames": [],
      "failedCheckSha": null,
      "author": "dependabot[bot]",
      "isAdopted": false
    }
  ],
  "issues": [
    {
      "owner": "owner",
      "repo": "repo",
      "repository": "owner/repo",
      "number": 120,
      "status": "Closed",
      "priority": 2,
      "isOnHold": true,
      "firstSeen": "2026-05-01T09:00:00Z",
      "lastUpdated": "2026-05-12T10:00:00Z",
      "whenClosed": "2026-05-12T10:00:00Z",
      "assignees": [],
      "linkedPrNumber": 123
    }
  ],
  "asOf": "2026-05-13T14:31:31Z"
}
```

`asOf` is the latest `lastUpdated` of any item returned, or the current time when nothing matches. `priority` and
`reviewDecision` are the numeric values of the `WorkPriority` and `ReviewDecisionState` enums.

### `GET /ping`

Lightweight health check — returns `{"value":"Pong!"}` without touching the database.

## Running locally

1. Create a GitHub personal access token with `notifications` scope at [github.com/settings/tokens](https://github.com/settings/tokens)
2. Configure `appsettings-local.json` (gitignored) with your token and database connection string
3. Run `dotnet run --project src/Credfeto.Dispatcher.Server`

### Development HTTPS certificate

The server always listens for HTTP on port 8080. It also listens for HTTPS on port 8081, but only when a passwordless
`server.pfx` sits in the same directory as the `appsettings.json` it loads (the build or publish output directory).
No certificate is committed to the repository; generate a local, self-signed one with:

```sh
scripts/generate-dev-cert.sh
```

This needs OpenSSL 1.1.1 or later. It writes `src/Credfeto.Dispatcher.Server/server.pfx` with `CN=defi.local` and
subject alternative names `localhost` and `127.0.0.1`, valid for 365 days. It refuses to overwrite an existing
`server.pfx`; pass `--force` (or `-f`) to regenerate it. `*.pfx` files are git-ignored, so the generated certificate
is never committed. Copy it next to `appsettings.json` in the output directory to enable HTTPS.

### Post-merge step: purge the old certificate from history (humans only)

A `server.pfx` with its private key used to be committed at `src/Credfeto.Dispatcher.Server/server.pfx`. Deleting it
does not remove it from git history, so after this change merges a repository administrator must purge it:

1. Make a fresh mirror clone: `git clone --mirror git@github.com:credfeto/credfeto-dispatcher.git`
2. In the mirror, run `git filter-repo --path src/Credfeto.Dispatcher.Server/server.pfx --invert-paths`. This rewrites
   every branch, including `main` and `release/0.0.1`.
3. Force-push the rewritten history with `--force-with-lease`. This needs admin rights to bypass branch protection.
4. Rebase every open pull request branch onto the rewritten `main`, and re-clone any existing clones rather than
   pulling into them.

The old private key is public. Anything that trusted that `CN=defi.local` certificate must stop trusting it.

## Changelog

See [CHANGELOG](CHANGELOG.md) for history

## Contributors

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->
