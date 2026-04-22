using System.Collections.Generic;

namespace Credfeto.Dispatcher.Discord.DataTypes;

public sealed record DiscordMessage(string Content, IReadOnlyList<DiscordEmbed> Embeds);
