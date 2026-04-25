using System;
using System.Diagnostics.CodeAnalysis;
using Credfeto.Dispatcher.Storage.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Credfeto.Dispatcher.Storage.Migrations;

[DbContext(typeof(DispatcherDbContext))]
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by Entity Framework Core via reflection")]
[SuppressMessage("Philips.CodeAnalysis.DuplicateCodeAnalyzer", "PH2071:Duplicate shape found", Justification = "Two entities with identical property schemas — refactoring is not applicable to EF Core model snapshot structure.")]
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

        modelBuilder.Entity<PullRequestEntity>(b =>
        {
            b.HasKey(e => new { e.Repository, e.Id });
            b.ToTable("PullRequests");
            b.Property(e => e.Repository).HasColumnType("TEXT");
            b.Property(e => e.Id).HasColumnType("INTEGER");
            b.Property(e => e.Status).IsRequired().HasColumnType("TEXT");
            b.Property(e => e.FirstSeen).HasColumnType("TEXT");
            b.Property(e => e.LastUpdated).HasColumnType("TEXT");
            b.Property(e => e.WhenClosed).HasColumnType("TEXT");
        });

        modelBuilder.Entity<IssueEntity>(b =>
        {
            b.HasKey(e => new { e.Repository, e.Id });
            b.ToTable("Issues");
            b.Property(e => e.Repository).HasColumnType("TEXT");
            b.Property(e => e.Id).HasColumnType("INTEGER");
            b.Property(e => e.Status).IsRequired().HasColumnType("TEXT");
            b.Property(e => e.FirstSeen).HasColumnType("TEXT");
            b.Property(e => e.LastUpdated).HasColumnType("TEXT");
            b.Property(e => e.WhenClosed).HasColumnType("TEXT");
        });

        ConfigureNotificationQueueEntity(modelBuilder);
    }

    private static void ConfigureNotificationQueueEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationQueueEntity>(b =>
        {
            b.HasKey(e => e.SubjectUrl);
            b.ToTable("NotificationQueue");
            b.HasIndex(e => e.DispatchAfter).HasDatabaseName("IX_NotificationQueue_DispatchAfter");
            b.Property(e => e.SubjectUrl)
             .HasColumnType("TEXT")
             .HasConversion(new ValueConverter<Uri, string>(v => v.AbsoluteUri, v => new Uri(v, UriKind.Absolute)));
            b.Property(e => e.NotificationId).IsRequired().HasColumnType("TEXT");
            b.Property(e => e.Repository).IsRequired().HasColumnType("TEXT");
            b.Property(e => e.RepositoryUrl)
             .IsRequired()
             .HasColumnType("TEXT")
             .HasConversion(new ValueConverter<Uri, string>(v => v.AbsoluteUri, v => new Uri(v, UriKind.Absolute)));
            b.Property(e => e.SubjectType).IsRequired().HasColumnType("TEXT");
            b.Property(e => e.SubjectTitle).IsRequired().HasColumnType("TEXT");
            b.Property(e => e.Reason).IsRequired().HasColumnType("TEXT");
            b.Property(e => e.UpdatedAt)
             .HasColumnType("INTEGER")
             .HasConversion(new ValueConverter<DateTimeOffset, long>(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero)));
            b.Property(e => e.QueuedAt)
             .HasColumnType("INTEGER")
             .HasConversion(new ValueConverter<DateTimeOffset, long>(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero)));
            b.Property(e => e.DispatchAfter)
             .HasColumnType("INTEGER")
             .HasConversion(new ValueConverter<DateTimeOffset, long>(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero)));
        });
    }
}