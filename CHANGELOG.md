# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!--
Please ADD ALL Changes to the UNRELEASED SECTION and not a specific release
-->

## [Unreleased]
### Security
### Added
- Enable Native AOT publishing for the server project
- Work item scanner background service that polls configured GitHub repositories for open pull requests and issues, updating notification state with priority, hold status, and linked PR information
- Work item scanner auto-discovers GitHub repositories with write access via the API — no longer requires explicit Repos configuration. Uses AllowedOwners, AllowedRepos, and ExcludedRepos filters; empty filters mean scan all accessible repos.
- Enable PublishTrimmed for release builds, convert MaxLength data annotations to fluent API, and update options properties to use set accessors for trim-compatible configuration binding
- Store whether a pull request branch is up-to-date with its base in the database, exposed via WorkItem and the priorities endpoint so consumers can decide whether to rebase
- LastUpdated field included in /priorities API response work items
- smoke-test script (scripts/smoke-test.sh) and Dockerfile build-time gate to verify the trimmed binary starts and /priorities returns HTTP 200 before the image is finalized
- Fuzzy case-insensitive label matching for LabelFilter and NoWorkFilter
- Expanded PullRequestDetails with full comment, review, run, and linked-item lists, PR body, and up-to-date status for issue #36
- ItemRepository and LastNotification context to PullRequestDetails and IssueDetails for issue #35
- Order priorities by owner (alphabetically or configured order), then type (PRs first), then repository (alphabetically or configured order), then issue priority (Urgent > High > Medium > Low > untagged)
- Return all work item details (status, whenClosed, isOnHold, hasLinkedPr) from the /priorities endpoint
- Persist and return comment count, review decision, and failed CI check details for pull requests
- Include stuck dependabot PRs in /priorities endpoint at Security priority after configurable timeout (default 3 hours)
- Polling for modified issues that contain mentions of the configured user
- Freshness metadata (as_of, lag_seconds) on /priorities response so consumers can detect stale snapshots
- Poll GitHub Events API per-repo and per-owner feeds to keep work-item state fresher between full scans (enabled via Filter.PollEvents: true)
- Default PollIssueEdits and PollEvents to true so event polling is active without explicit configuration
- Added Credfeto.Dispatcher.Storage.Database project using MSBuild.Sdk.SqlProj to generate a DACPAC from the schema files
- Fixed migration script 001_InitialCreate to use IF NOT EXISTS guards so it can run against databases that already have EF Core-created tables
- Added X-Version response header middleware to expose server version on all HTTP responses
- Configurable rules for upgrading bot-authored pull requests to a baseline and/or security priority based on author login, branch prefix, and inactivity timeout
- DefaultPriority property on BotPrRule assigns a baseline priority on first rule match, with escalation to a higher priority after the configured timeout
- Add X-Server response header containing the hostname of the machine serving the request
### Fixed
- EF Core change-tracking comparers trimmed away at publish time causing MissingMethodException at startup; preserve EF Core and Ben.Demystifier assemblies as trimmer roots
- preserve EF Core migration types as trimmer roots to prevent missing-table errors at runtime on trimmed binaries
- suppress IL2026 trim analysis errors in EF Core migration Up methods and model snapshot that use composite key expression trees (Expression.New via HasKey/PrimaryKey lambdas)
- Debug logging added for repo exclusion reasons in WorkItemScanner discovery to aid production diagnosis
- Use EphemeralKeySet when loading HTTPS certificate to fix Docker container startup failure
- Priorities endpoint: exclude PRs and issues from repos that are deleted, archived, or no longer accessible to the token
- Priorities endpoint: only exclude issues where the linked PR is currently open (not when the linked PR is closed)
- Filter out PRs and issues from archived or disabled repositories from /priorities output
- Urgent and Security priority issues are no longer suppressed when a repo has an open or draft PR
- Closed issues and merged PRs no longer appear as open in /priorities (closes #105)
- Replaced SQLite-specific EF Core migrations with a SQL Server InitialCreate migration; removed dead Provider property from DatabaseConfiguration
- Stop crashing with ArgumentNullException when the GitHub Events API returns a pull request or issue event with a null html_url
- Create SQL Server database catalog on startup if it does not already exist before running migrations
- Filter issues with open linked PRs from priorities endpoint
- Remove work items for repos that become inaccessible during scan
- Fixed SQLFLUFF RF04 lint violations - quoted [Target] and [Source] aliases in MERGE stored procedure SQL
- Docker smoke test now skips database migrations when no connection string is configured, allowing the trim-failure check to pass without a SQL Server
- Rewrote stored procedures to use CTEs instead of functions in filter predicates to satisfy non-sargable SQL lint rule
- LabelFilter now ignores empty/whitespace entries, so a config override of [""] correctly disables label filtering
- DateTimeOffset columns now extracted correctly from database results — updated Credfeto.Database.Source.Generation to 1.2.209.2134
- /priorities endpoint now returns a structured error response rather than a silent HTTP 500 with empty body when an exception occurs
- Repo discovery partial failure (nil page from GitHub API) no longer updates the active repo list with incomplete data
- Fixed DACPAC not generated on Linux by renaming SQL project from .sqlproj to .csproj
- Fixed SQL analyzer violations in stored procedures: NOT IN → NOT EXISTS, CASE expressions missing ELSE clause, single-character table aliases
- Added .tsqllintignore to exclude generated build artefacts in bin/obj directories from SQL linting
- SQL code analysis violations in stored procedures: replaced IN predicate with inequality comparison and added explicit ELSE NULL to CASE expressions
- Suppress IL2104 trim warnings for Microsoft.Data.SqlClient and System.Configuration.ConfigurationManager by preserving them as trimmer roots
- Urgent and security priority issues now always appear in the work item list, bypassing the maxIssues cap that was preventing them from showing when lower-priority repos filled the available slots first
- Closed issues and PRs now removed from the work queue within one poll cycle when the notification has a reason not in the Reasons filter (e.g. 'subscribed'). Previously these were dropped silently and could remain visible for up to the full scan interval.
- Link issues to open pull requests from PR-linked references so /priorities returns the PR instead of the linked issue
- Notification poller stuck on empty ETag now forces a fresh poll; scan interval reduced to 30 minutes; DB timestamp columns added to PollingStates, PullRequests, and Issues; stored procedures now own their own timestamps via GETUTCDATE()
- Issues_LinkPullRequest stored procedure now computes @now internally, fixing runtime error when linking issues to pull requests
- Suppress Polly HTTP resilience telemetry noise and fix Task.Delay unhandled cancellation in background services during shutdown
- Handle missing URL in RepoEventPoller notifications without throwing ArgumentNullException
- Adopted bot PRs now bypass the label filter when an adoption rule matches, so they are surfaced for adoption regardless of their labels
### Changed
- Dependencies - Updated Credfeto.Version.Information.Generator to 1.0.124.1183
- Dependencies - Updated FunFair.CodeAnalysis to 7.1.41.1934
- Dependencies - Updated Meziantou.Analyzer to 3.0.58
- Dependencies - Updated SonarAnalyzer.CSharp to 10.25.0.139117
- Dependencies - Updated Credfeto.Date to 1.1.151.1695
- Dependencies - Updated Credfeto.Random to 1.0.150.1663
- Dependencies - Updated Credfeto.Services.Startup to 1.1.145.1592
- Dependencies - Updated FunFair.Test.Common to 6.2.22.2198
- Dependencies - Updated FunFair.Test.Source.Generator to 6.2.22.2198
- Migrated NotificationStateTracker and GitHubPollingWorker from deprecated ICurrentTimeSource to System.TimeProvider
- Replaced multiple REST API calls in PullRequestDetailFetcher with a single GitHub GraphQL query, reducing network overhead and eliminating the dependency on the mergeable_state field
- Simplified INotificationStateTracker API to accept GitHubNotification and PullRequestDetails/IssueDetails objects instead of individual scalar parameters
- INotificationStateTracker: changed Task/Task<bool> return types to ValueTask/ValueTask<bool>, renamed methods to overloads
- Structured WorkItem fields: LinkedPrNumbers as ImmutableArray<int>, ReviewDecision as enum, FailedCheckNames as ImmutableArray<string>, added FailedCheckSha
- Security label now has higher priority than Urgent in issue priority ordering
- Priorities endpoint: suppress issues from repos with open PRs, cap to 1 issue per repo, and apply configurable MaxIssues limit
- Replaced Entity Framework Core with Credfeto.Database.SourceGenerator and DbUp for all database access in storage layer
- Added 'blocked' label to default NoWorkFilter so blocked items are excluded from work queues
- Consolidated GitHub filtering configuration: removed PrioritiesOptions class; MaxIssues and StuckDependabotTimeoutHours are now configured under GitHub:Filter
### Deprecated
### Removed
- Removed IsUpToDate field from WorkItem and PullRequestDetails as it was never populated in production
- Removed Docker smoke test from Dockerfile as it had too many gaps to be reliable
- Removed tracked .idea IDE metadata files now covered by .gitignore
- Removed Discord integration and the notification queue; matching GitHub notifications now update stored work-item state directly
- Serilog.Enrichers.Demystifier package as it is not AOT-compatible
- Ben.Demystifier dependency (transitive via Serilog.Enrichers.Demystifier) as it uses reflection and is not AOT-compatible
### Deployment Changes
- Removed Priorities configuration section; StuckDependabotTimeoutHours is now set under GitHub:Filter:StuckDependabotTimeoutHours and MaxIssues under GitHub:Filter:MaxIssues
<!--
Releases that have at least been deployed to staging, BUT NOT necessarily released to live.  Changes should be moved from [Unreleased] into here as they are merged into the appropriate release branch
-->
## [0.0.1] - 2026-05-01
### Added
- `GET /ping` lightweight health check endpoint returning `{"value":"Pong!"}` without touching the database
- `docker-compose.yml` with port mappings for HTTP (8080) and HTTPS (8081)
- `EXPOSE 8081` added to Dockerfile for the HTTPS Kestrel endpoint
- `.http` test file for all server endpoints (localhost:8080)
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
- Dependencies - Updated Credfeto.Enumeration to 1.2.141.1822
- Dependencies - Updated Credfeto.Version.Information.Generator to 1.0.123.1157
- Dependencies - Updated FunFair.CodeAnalysis to 7.1.39.1841
- Dependencies - Updated Meziantou.Analyzer to 3.0.54
- Dependencies - Updated SonarAnalyzer.CSharp to 10.24.0.138807
- Dependencies - Updated Credfeto.Random to 1.0.149.1635
- Dependencies - Updated Credfeto.Services.Startup to 1.1.144.1565
- Dependencies - Updated Microsoft.Extensions to 10.0.7

## [0.0.0] - Project created