using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Credfeto.Dispatcher.Storage;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DispatcherDbContext>
{
    public DispatcherDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<DispatcherDbContext> options = new DbContextOptionsBuilder<DispatcherDbContext>()
            .UseSqlServer("Server=localhost;Database=dispatcher;Trusted_Connection=true;")
            .Options;

        return new DispatcherDbContext(options);
    }
}
