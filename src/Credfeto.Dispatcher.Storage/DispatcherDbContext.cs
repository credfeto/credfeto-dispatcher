using Microsoft.EntityFrameworkCore;

namespace Credfeto.Dispatcher.Storage;

public sealed class DispatcherDbContext : DbContext
{
    public DispatcherDbContext(DbContextOptions<DispatcherDbContext> options)
        : base(options)
    {
    }

}
