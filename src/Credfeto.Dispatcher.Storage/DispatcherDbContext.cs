using System.Diagnostics.CodeAnalysis;
using Credfeto.Dispatcher.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace Credfeto.Dispatcher.Storage;

public sealed class DispatcherDbContext : DbContext
{
    public DispatcherDbContext(DbContextOptions<DispatcherDbContext> options)
        : base(options)
    {
    }

    [SuppressMessage(category: "Nullable.Extended.Analyzer", checkId: "NX0004:NullForgivingOperator", Justification = "Populated by Entity Framework Core")]
    public DbSet<PollingStateEntity> PollingStates { get; init; } = default!;
}
