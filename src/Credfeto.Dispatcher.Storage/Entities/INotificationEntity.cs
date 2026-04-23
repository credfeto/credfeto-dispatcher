using System;

namespace Credfeto.Dispatcher.Storage.Entities;

internal interface INotificationEntity
{
    string Status { get; set; }

    DateTimeOffset LastUpdated { get; set; }

    DateTimeOffset? WhenClosed { get; set; }
}
