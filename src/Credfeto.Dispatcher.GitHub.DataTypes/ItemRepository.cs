using System;
using System.Diagnostics;

namespace Credfeto.Dispatcher.GitHub.DataTypes;

[DebuggerDisplay("{Owner}/{Name}: {Url}")]
public sealed record ItemRepository(string Owner, string Name, Uri Url)
{
    public string FullName => $"{this.Owner}/{this.Name}";

    public static ItemRepository FromNotification(GitHubNotification notification)
    {
        string[] parts = notification.Repository.FullName.Split('/');

        return new ItemRepository(Owner: parts[0], Name: parts[1], Url: notification.Repository.Url);
    }
}
