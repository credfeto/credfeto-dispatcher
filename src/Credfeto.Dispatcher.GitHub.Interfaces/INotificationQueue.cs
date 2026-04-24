using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface INotificationQueue
{
    Task EnqueueAsync(GitHubNotification notification, DateTimeOffset dispatchAfter, CancellationToken cancellationToken);

    Task RemoveIfPresentAsync(GitHubNotification notification, CancellationToken cancellationToken);

    Task<IReadOnlyList<GitHubNotification>> GetReadyItemsAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task RemoveAsync(GitHubNotification notification, CancellationToken cancellationToken);
}
