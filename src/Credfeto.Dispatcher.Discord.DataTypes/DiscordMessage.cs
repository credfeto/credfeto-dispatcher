using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Dispatcher.Discord.DataTypes;

[DebuggerDisplay("{Content} ({Embeds.Count} embeds)")]
public sealed record DiscordMessage(string Content, IReadOnlyList<DiscordEmbed> Embeds);
