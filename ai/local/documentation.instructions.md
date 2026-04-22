# Documentation Instructions

[Back to index](index.md)

## README.md Configuration Documentation

- Whenever configuration options are added, removed, or modified (in `appsettings.json`, options classes, or environment variable names), the **Configuration** section in `README.md` MUST be updated to reflect the changes.
- Configuration documentation in `README.md` must always match the actual options classes (`GitHubOptions`, `DiscordOptions`, etc.) and their validation rules.
- When adding new configuration keys, document their type, purpose, and any validation constraints (e.g., minimum values, required vs optional).
- When removing configuration keys, remove them from the README.md documentation.
- When renaming configuration keys, update README.md to use the new names.
- Never leave README.md describing configuration options that no longer exist.
