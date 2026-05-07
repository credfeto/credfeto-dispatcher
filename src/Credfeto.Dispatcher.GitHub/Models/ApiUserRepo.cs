using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dispatcher.GitHub.Models;

[DebuggerDisplay("{FullName}: Archived={Archived}, Push={Permissions?.Push}")]
internal sealed record ApiUserRepo(
    [property: JsonPropertyName("full_name")] string FullName,
    [property: JsonPropertyName("archived")] bool Archived,
    [property: JsonPropertyName("disabled")] bool Disabled,
    [property: JsonPropertyName("permissions")] ApiRepoPermissions? Permissions
);
