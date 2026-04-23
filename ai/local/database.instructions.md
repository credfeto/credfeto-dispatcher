# Database Instructions

[Back to index](index.md)

## Notification State Tracking Tables

The application uses SQLite via Entity Framework Core to track the state of GitHub PR and Issue notifications. This prevents spamming Discord with repeated notifications about already-closed items.

### PullRequests Table

Tracks the state of each GitHub Pull Request that has been processed.

| Column | Type | Nullable | Description |
|---|---|---|---|
| Repository | TEXT | No | Full repository name (e.g. `owner/repo`). Part of primary key. |
| Id | INTEGER | No | Pull request number. Part of primary key. |
| Status | TEXT | No | Current status: `Open`, `Draft`, or `Closed`. |
| FirstSeen | TEXT | No | UTC timestamp when the PR was first processed. |
| LastUpdated | TEXT | No | UTC timestamp of the most recent state update. Same as `FirstSeen` on creation. |
| WhenClosed | TEXT | Yes | UTC timestamp when the PR was first recorded as closed. Null if open/draft. |

**Primary Key**: `(Repository, Id)`

### Issues Table

Tracks the state of each GitHub Issue that has been processed.

| Column | Type | Nullable | Description |
|---|---|---|---|
| Repository | TEXT | No | Full repository name (e.g. `owner/repo`). Part of primary key. |
| Id | INTEGER | No | Issue number. Part of primary key. |
| Status | TEXT | No | Current status: `Open` or `Closed`. |
| FirstSeen | TEXT | No | UTC timestamp when the issue was first processed. |
| LastUpdated | TEXT | No | UTC timestamp of the most recent state update. Same as `FirstSeen` on creation. |
| WhenClosed | TEXT | Yes | UTC timestamp when the issue was first recorded as closed. Null if open. |

**Primary Key**: `(Repository, Id)`

## Adding Future Trackable Object Types

When a new GitHub notification subject type needs state tracking (e.g. Release, Discussion, Commit), follow this pattern:

1. **Create an entity class** in `src/Credfeto.Dispatcher.Storage/Entities/` following the same schema as `PullRequestEntity` and `IssueEntity`.
2. **Add a `DbSet<T>` property** to `DispatcherDbContext` and configure the composite key in `OnModelCreating`.
3. **Add a migration** in `src/Credfeto.Dispatcher.Storage/Migrations/` to create the new table.
4. **Update the model snapshot** (`DispatcherDbContextModelSnapshot.cs`) to include the new entity.
5. **Add methods to `INotificationStateTracker`** for `ShouldSkipXxxAsync` and `UpdateXxxStateAsync`.
6. **Implement the new methods** in `NotificationStateTracker`.
7. **Create a detail fetcher** (implementing `IXxxDetailFetcher`) to retrieve the current status of the item.
8. **Update `GitHubPollingWorker`** to use the new fetcher and state tracker methods for the new type.
9. **Update this file** and `index.md` to document the new table.

## State Tracking Logic

- Before dispatching a Discord notification, check if the item is already recorded as **Closed** in the database AND is currently still **Closed** — if so, skip the notification.
- Always update the database state after deciding whether to dispatch.
- If an item is reopened (status changes from `Closed` to `Open`), the `WhenClosed` column is set to `null` and the item becomes eligible for future close notifications again.
