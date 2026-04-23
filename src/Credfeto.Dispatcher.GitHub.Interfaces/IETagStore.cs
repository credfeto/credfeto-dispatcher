using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface IETagStore
{
    ValueTask<string?> GetETagAsync(string key, CancellationToken cancellationToken);

    ValueTask SaveETagAsync(string key, string eTag, CancellationToken cancellationToken);
}
