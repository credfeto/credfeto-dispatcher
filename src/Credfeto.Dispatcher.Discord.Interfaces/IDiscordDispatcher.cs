using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Discord.DataTypes;

namespace Credfeto.Dispatcher.Discord.Interfaces;

public interface IDiscordDispatcher
{
    ValueTask SendAsync(DiscordMessage message, CancellationToken cancellationToken);
}
