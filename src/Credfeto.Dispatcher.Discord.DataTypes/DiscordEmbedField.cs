using System.Diagnostics;

namespace Credfeto.Dispatcher.Discord.DataTypes;

[DebuggerDisplay("{Name}: {Value}")]
public sealed record DiscordEmbedField(string Name, string Value, bool Inline);
