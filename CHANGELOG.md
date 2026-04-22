# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!--
Please ADD ALL Changes to the UNRELEASED SECTION and not a specific release
-->

## [Unreleased]
### Added
- Initial C# project structure for GitHub to Discord dispatcher
- GitHub Notifications API polling with ETag caching (zero-cost when idle via 304 Not Modified)
- Notification filtering by reason, label, owner, and excluded repos
- Discord outbound webhook dispatcher
- `DebuggerDisplay` attributes on all data types and value classes
- One-type-per-file convention for all C# source files
- `LabelFilter` and `NoWorkFilter` as arrays in configuration
- `AllowedOwners` and `ExcludedRepos` filter options for repo-level filtering
- Structured `ILogger<T>` logging throughout the polling and dispatch pipeline: worker startup/shutdown, poll cycles (ETag sent, 304 Not Modified or notification count), per-notification debug entries, filter pass/drop decisions, dispatch to Discord, and Discord webhook non-success warnings
### Fixed
- Removed unused `Mediator` runtime package reference from `Credfeto.Dispatcher.Server` — `Mediator.SourceGenerator` source generator is sufficient; no separate runtime package is needed for a simple background service
### Changed
### Deprecated
### Removed
### Security
### Deployment Changes

<!--
Releases that have at least been deployed to staging, BUT NOT necessarily released to live.  Changes should be moved from [Unreleased] into here as they are merged into the appropriate release branch
-->
## [0.0.0] - Project created