using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.Services;

[DebuggerDisplay(value: "{Description}: {Url}")]
internal readonly record struct RepoEventFeed(
    string Url,
    string LastIdKey,
    string SeenIdsKey,
    string ETagKey,
    string Description
);
