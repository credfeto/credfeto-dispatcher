using System;
using Credfeto.Dispatcher.GitHub.DataTypes;

namespace Credfeto.Dispatcher.Storage.Entities;

internal interface INotificationEntity
{
    WorkItemStatus Status { get; set; }

    DateTimeOffset LastUpdated { get; set; }

    DateTimeOffset? WhenClosed { get; set; }
}
