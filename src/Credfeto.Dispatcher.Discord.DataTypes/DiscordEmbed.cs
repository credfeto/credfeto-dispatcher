using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.Discord.DataTypes;

[DebuggerDisplay("{Title}: {Url}")]
public sealed record DiscordEmbed(string Title, string Description, Uri Url, int Color);
