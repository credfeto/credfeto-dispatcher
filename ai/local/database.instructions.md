# Database Instructions

[Back to index](index.md)

## Architecture Rule (MANDATORY)

**Filtering and ordering of data belongs in the database, not in application code.**

- Never filter or order result sets in C# after fetching rows from the database.
- Never fetch more rows than needed and discard the excess in code.
- Pass configuration values (owner lists, repo lists, caps, timeouts) as parameters to stored procedures and let the database do the work.
- The database can use indexes, statistics, and execution plans to optimise queries far beyond what application-level LINQ can achieve.
- When a stored procedure needs a list of values (e.g. allowed owners), pass it as a comma-separated `NVARCHAR(MAX)` parameter and use `STRING_SPLIT` inside the procedure.

## Database Engine

SQL Server, accessed via the `Credfeto.Database` source-generated client. All data access goes through stored procedures — no inline SQL in application code.

## Migrations

- Migration scripts live in `src/Credfeto.Dispatcher.Storage/migrations/` and are numbered sequentially (`001_`, `002_`, …).
- Every schema change (table, stored procedure, index) must be in a migration script.
- Use `CREATE OR ALTER PROCEDURE` for stored procedures so migrations are re-runnable.
- Never modify an existing migration — add a new one.

## Schema

### `dbo.Repos`

Tracks which repositories are currently active (discovered by the scanner).

| Column | Type | Description |
| --- | --- | --- |
| `Repository` | `NVARCHAR(450)` PK | Full repo name, e.g. `owner/repo` |
| `IsActive` | `BIT` | `1` = active (seen in last scan), `0` = inactive |
| `LastUpdated` | `DATETIMEOFFSET` | When `IsActive` was last changed |

### `dbo.PullRequests`

| Column | Type | Description |
| --- | --- | --- |
| `Repository` | `NVARCHAR(450)` PK | Full repo name |
| `Id` | `INT` PK | PR number |
| `Status` | `NVARCHAR(MAX)` | `Open`, `Draft`, or `Closed` |
| `Priority` | `INT` | Maps to `WorkPriority` enum (0=UNKNOWN … 5=SECURITY) |
| `IsOnHold` | `BIT` | Label-driven hold flag |
| `CommentCount` | `INT` | |
| `ReviewDecision` | `NVARCHAR(MAX)` | `Approved`, `ChangesRequested`, or `NULL` |
| `FailedCheckCount` | `INT` | |
| `FailedCheckNames` | `NVARCHAR(MAX)` | Comma-separated list of failed check names |
| `FailedCheckSha` | `NVARCHAR(MAX)` | HEAD SHA when checks last failed |
| `Author` | `NVARCHAR(MAX)` | GitHub login of the PR author |
| `FirstSeen` | `DATETIMEOFFSET` | When the PR was first stored |
| `LastUpdated` | `DATETIMEOFFSET` | Most recent upsert timestamp |
| `WhenClosed` | `DATETIMEOFFSET` | Set when status first becomes `Closed`; `NULL` otherwise |

### `dbo.Issues`

| Column | Type | Description |
| --- | --- | --- |
| `Repository` | `NVARCHAR(450)` PK | Full repo name |
| `Id` | `INT` PK | Issue number |
| `Status` | `NVARCHAR(MAX)` | `Open` or `Closed` |
| `Priority` | `INT` | Maps to `WorkPriority` enum |
| `IsOnHold` | `BIT` | |
| `LinkedPrNumber` | `INT` | PR number if a linked PR exists; `NULL` otherwise |
| `FirstSeen` | `DATETIMEOFFSET` | |
| `LastUpdated` | `DATETIMEOFFSET` | |
| `WhenClosed` | `DATETIMEOFFSET` | |

### `dbo.NotificationQueue`

Holds notifications that have been received but are not yet ready to dispatch (deliberate delay to avoid noise from rapid state changes).

### `dbo.PollingStates`

Stores ETags for GitHub notification polling endpoints to support conditional requests.

## Key Stored Procedures

| Procedure | Purpose |
| --- | --- |
| `PullRequests_GetActive` | Returns open/draft PRs for active repos |
| `Issues_GetActive` | Returns open issues for active repos (suppresses lower-priority issues when an open PR exists for the repo) |
| `Repos_SetActive` | MERGE to mark discovered repos active and all others inactive |
| `PullRequests_Upsert` / `Issues_Upsert` | Insert or update a single item |
| `PullRequests_CloseStale` / `Issues_CloseStale` | Close items no longer present in a scan |
| `PullRequests_RemoveForRepositories` / `Issues_RemoveForRepositories` | Hard-delete all items for given repos |
| `NotificationQueue_Upsert` / `_Delete` / `_GetReady` | Notification queue management |
| `PollingStates_GetByKey` / `_Upsert` | ETag persistence |

## Pending Refactor

`PullRequests_GetActive` and `Issues_GetActive` currently return all eligible rows and leave ordering, owner/repo filtering, the per-repo top-issue selection, the SECURITY/URGENT always-include rule, and the `MaxIssues` cap to application code. This violates the architecture rule above. See the linked GitHub issue for the planned work to push all of that logic into the stored procedures as parameters.
