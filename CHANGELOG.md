# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!--
Please ADD ALL Changes to the UNRELEASED SECTION and not a specific release
-->

## [Unreleased]
### Added
- `GET /priorities` endpoint returning active work items ordered by owner priority, repo priority, type (PRs before issues), urgency, and age
- `Priorities` configuration section with `Owners` and `Repos` arrays to control ordering
- `WorkPriority` enum (Unknown, Low, Medium, High, Urgent) derived from GitHub labels
- Priority and on-hold state stored per pull request and issue in the database
- `HasLinkedPr` flag stored per issue to exclude issues that already have a linked pull request
- Kestrel HTTP (port 8080) and HTTPS (port 8081, requires `server.pfx`) endpoints replacing the generic host
- Initial C# project structure for GitHub to Discord dispatcher
- GitHub Notifications API polling with ETag caching (zero-cost when idle via 304 Not Modified)
- Notification filtering by reason, label, owner, and excluded repos
- Discord outbound webhook dispatcher
- `DebuggerDisplay` attributes on all data types and value classes
- One-type-per-file convention for all C# source files
- `LabelFilter` and `NoWorkFilter` as arrays in configuration
- `AllowedOwners` and `ExcludedRepos` filter options for repo-level filtering
- GitHubOptionsValidator to validate GitHub API token is configured and PollIntervalSeconds is at least 30
- DiscordOptionsValidator to validate Discord WebhookUrl is configured
- Standard resilience handler (retry with exponential backoff, circuit breaker, timeout) via Microsoft.Extensions.Http.Resilience on both GitHub and Discord HTTP clients
- Documentation instructions for keeping README.md configuration section up-to-date when configuration options change
- Structured `ILogger<T>` logging throughout the polling and dispatch pipeline: worker startup/shutdown, poll cycles (ETag sent, 304 Not Modified or notification count), per-notification debug entries, filter pass/drop decisions, dispatch to Discord, and Discord webhook non-success warnings
- Post startup and GitHub auth status messages to Discord on launch
- AllowedRepos filter option to restrict notifications to specific repositories only
- Pull request detail enrichment: `IPullRequestDetailFetcher` service that fetches PR title, status (Open/Draft/Closed), assignees, labels, latest comment (for `comment` reason), last CHANGES_REQUESTED review (for `review_requested` reason), and failed CI workflow run (for `ci_activity` reason)
- `DiscordEmbedField` type and `Fields` collection on `DiscordEmbed` to support Discord embed field sections
- Rich PR notification embeds in Discord with Status, Reason, Assignees, Labels, and context-specific fields
- `FixedResponseHandler` HTTP test helper and comprehensive `PullRequestDetailFetcherTests` covering all enrichment paths, status determination, body truncation, and error cases
- PR number, reason, and HTML URL included in the Discord message `Content` field for pull request notifications, making them actionable without reading the embed (e.g. `[owner/repo] PR #42 (mention) https://github.com/owner/repo/pull/42`)
- `GitHubPollingWorkerTests` covering Content field format, fallback to basic message when fetcher returns null, issue notification format, and filtered notification not dispatched
- SQLite database infrastructure using Entity Framework Core with automatic migration on startup; data folder created next to the application executable and git-ignored
- Persist GitHub notifications API ETag in database to resume polling after restart (#21)
- Track notification state (open/closed) for pull requests and issues in database to suppress repeated Discord notifications for already-closed items (#26)
- Rich issue notification embeds in Discord with Status, Reason, Assignees, Labels, and Linked PR fields; `IssueDetails` enriched with `Assignees`, `Labels`, and `LinkedPullRequestUrl` populated from the GitHub Issues API (#16)
### Fixed
- EF Core SQLite cannot translate DateTimeOffset <= comparisons when stored as TEXT; store DispatchAfter, QueuedAt, and UpdatedAt as long (UtcTicks) with INTEGER column type
- Removed unused `Mediator` runtime package reference from `Credfeto.Dispatcher.Server` — `Mediator.SourceGenerator` source generator is sufficient; no separate runtime package is needed for a simple background service
- Updated gitleaks configuration to suppress false positive secret detection caused by logging extension class name matching the GitHub token regex pattern
### Changed
- Configure HttpClient base address, User-Agent, Accept, X-GitHub-Api-Version, and Authorization headers at registration time via IHttpClientFactory rather than per request
- Renamed IGitHubNotificationPoller to INotificationPoller and implementation to NotificationPoller
- Moved internal GitHub API model types to Models namespace and removed GitHub prefix from type names
- Replaced try/finally pattern with using declaration for HttpResponseMessage disposal
- Updated X-GitHub-Api-Version header to 2026-03-10
- Simplified DiscordWebhookDispatcher.SendAsync to use PostAsync directly instead of manually building HttpRequestMessage
### Deprecated
### Removed
### Security
### Deployment Changes

<!--
Releases that have at least been deployed to staging, BUT NOT necessarily released to live.  Changes should be moved from [Unreleased] into here as they are merged into the appropriate release branch
-->
## [0.0.0] - Project created
