using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Dispatcher.Discord.DataTypes;

[DebuggerDisplay("{Title}: {Url}")]
public sealed record DiscordEmbed(string Title, string Description, Uri Url, int Color, IReadOnlyList<DiscordEmbedField>? Fields = null);
