using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Dispatcher.GitHub.Interfaces;

public interface IWorkItemScanner
{
    Task ScanAsync(CancellationToken cancellationToken);
}
