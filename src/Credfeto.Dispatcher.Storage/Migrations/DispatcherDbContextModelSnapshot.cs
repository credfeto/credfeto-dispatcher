using System.Diagnostics.CodeAnalysis;
using Credfeto.Dispatcher.Storage.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Credfeto.Dispatcher.Storage.Migrations;

[DbContext(typeof(DispatcherDbContext))]
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by Entity Framework Core via reflection")]
internal sealed class DispatcherDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.7");

        modelBuilder.Entity<PollingStateEntity>(
            b =>
            {
                b.Property<string>("Key")
                 .HasMaxLength(256)
                 .HasColumnType("TEXT");
                b.Property<string>("ETag")
                 .IsRequired()
                 .HasMaxLength(1024)
                 .HasColumnType("TEXT");
                b.HasKey("Key");
                b.ToTable("PollingStates");
            }
        );
    }
}
