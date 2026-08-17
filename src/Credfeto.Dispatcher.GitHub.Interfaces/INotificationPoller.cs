using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface INotificationPoller
{
    ValueTask<NotificationPollResult> PollAsync(CancellationToken cancellationToken);
}
