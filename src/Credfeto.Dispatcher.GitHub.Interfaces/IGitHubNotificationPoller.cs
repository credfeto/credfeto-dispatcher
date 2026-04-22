using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface IGitHubNotificationPoller
{
    ValueTask<IReadOnlyList<GitHubNotification>> PollAsync(CancellationToken cancellationToken);
}
