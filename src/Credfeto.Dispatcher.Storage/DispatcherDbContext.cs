using System;
using System.Diagnostics.CodeAnalysis;
using Credfeto.Dispatcher.GitHub.DataTypes;
using Credfeto.Dispatcher.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace Credfeto.Dispatcher.Storage;

public sealed class DispatcherDbContext : DbContext
{
    [UnconditionalSuppressMessage(
        category: "Trimming",
        checkId: "IL2026",
        Justification = "Model is configured via explicit fluent API in OnModelCreating; no reflection-based entity discovery is used"
    )]
    public DispatcherDbContext(DbContextOptions<DispatcherDbContext> options)
        : base(options) { }

    [SuppressMessage(
        category: "Nullable.Extended.Analyzer",
        checkId: "NX0004:NullForgivingOperator",
        Justification = "Populated by Entity Framework Core"
    )]
    public DbSet<PollingStateEntity> PollingStates { get; init; } = default!;

    public DbSet<PullRequestEntity> PullRequests => this.Set<PullRequestEntity>();

    public DbSet<IssueEntity> Issues => this.Set<IssueEntity>();

    public DbSet<NotificationQueueEntity> NotificationQueue => this.Set<NotificationQueueEntity>();

    [UnconditionalSuppressMessage(
        category: "Trimming",
        checkId: "IL2026",
        Justification = "EF Core fluent API lambda expressions compile to expression trees that internally use Expression.New(ConstructorInfo,...); the models and properties accessed are preserved by this explicit fluent configuration"
    )]
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PollingStateEntity>().Property(e => e.Key).HasMaxLength(256);
        modelBuilder.Entity<PollingStateEntity>().Property(e => e.ETag).HasMaxLength(1024);

        modelBuilder.Entity<PullRequestEntity>().HasKey(e => new { e.Repository, e.Id });

        modelBuilder
            .Entity<PullRequestEntity>()
            .Property(e => e.Priority)
            .HasConversion(v => (int)v, v => (WorkPriority)v)
            .HasColumnType("INTEGER");

        modelBuilder.Entity<PullRequestEntity>().Property(e => e.IsOnHold).HasColumnType("INTEGER");

        modelBuilder.Entity<IssueEntity>().HasKey(e => new { e.Repository, e.Id });

        modelBuilder
            .Entity<IssueEntity>()
            .Property(e => e.Priority)
            .HasConversion(v => (int)v, v => (WorkPriority)v)
            .HasColumnType("INTEGER");

        modelBuilder.Entity<IssueEntity>().Property(e => e.IsOnHold).HasColumnType("INTEGER");

        modelBuilder.Entity<IssueEntity>().Property(e => e.HasLinkedPr).HasColumnType("INTEGER");

        modelBuilder.Entity<NotificationQueueEntity>().HasKey(e => e.SubjectUrl);

        modelBuilder
            .Entity<NotificationQueueEntity>()
            .Property(e => e.SubjectUrl)
            .HasConversion(v => v.AbsoluteUri, v => new Uri(v, UriKind.Absolute));

        modelBuilder
            .Entity<NotificationQueueEntity>()
            .Property(e => e.RepositoryUrl)
            .HasConversion(v => v.AbsoluteUri, v => new Uri(v, UriKind.Absolute));

        modelBuilder
            .Entity<NotificationQueueEntity>()
            .Property(e => e.DispatchAfter)
            .HasConversion(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero))
            .HasColumnType("INTEGER");

        modelBuilder
            .Entity<NotificationQueueEntity>()
            .Property(e => e.QueuedAt)
            .HasConversion(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero))
            .HasColumnType("INTEGER");

        modelBuilder
            .Entity<NotificationQueueEntity>()
            .Property(e => e.UpdatedAt)
            .HasConversion(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero))
            .HasColumnType("INTEGER");

        modelBuilder.Entity<NotificationQueueEntity>().HasIndex(e => e.DispatchAfter);
    }
}
