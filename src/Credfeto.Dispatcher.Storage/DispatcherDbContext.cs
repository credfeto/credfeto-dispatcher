using System;
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

    public DbSet<PullRequestEntity> PullRequests => this.Set<PullRequestEntity>();

    public DbSet<IssueEntity> Issues => this.Set<IssueEntity>();

    public DbSet<NotificationQueueEntity> NotificationQueue => this.Set<NotificationQueueEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PullRequestEntity>()
                    .HasKey(e => new { e.Repository, e.Id });

        modelBuilder.Entity<IssueEntity>()
                    .HasKey(e => new { e.Repository, e.Id });

        modelBuilder.Entity<NotificationQueueEntity>()
                    .HasKey(e => e.SubjectUrl);

        modelBuilder.Entity<NotificationQueueEntity>()
                    .Property(e => e.SubjectUrl)
                    .HasConversion(v => v.AbsoluteUri, v => new Uri(v, UriKind.Absolute));

        modelBuilder.Entity<NotificationQueueEntity>()
                    .Property(e => e.RepositoryUrl)
                    .HasConversion(v => v.AbsoluteUri, v => new Uri(v, UriKind.Absolute));

        modelBuilder.Entity<NotificationQueueEntity>()
                    .HasIndex(e => e.DispatchAfter);
    }
}
