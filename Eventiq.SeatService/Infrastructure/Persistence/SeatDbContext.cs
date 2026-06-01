using Eventiq.SeatService.Domain.Entity;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.SeatService.Infrastructure.Persistence;

public sealed class SeatDbContext : DbContext
{
    public SeatDbContext(DbContextOptions<SeatDbContext> options)
        : base(options)
    {
    }

    public DbSet<SeatMap> SeatMaps => Set<SeatMap>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<SeatObject> Objects => Set<SeatObject>();
    public DbSet<SeatMapVersion> Versions => Set<SeatMapVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("seat_service");

        // === SeatMap ===
        modelBuilder.Entity<SeatMap>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ChartId)
                .IsUnique()
                .HasFilter("session_id IS NULL")
                .HasDatabaseName("ix_seat_maps_chart_id_template");
            e.HasIndex(x => new { x.ChartId, x.SessionId })
                .IsUnique()
                .HasFilter("session_id IS NOT NULL")
                .HasDatabaseName("ix_seat_maps_chart_session");
            e.HasIndex(x => x.EventId);
            e.HasIndex(x => x.OrganizationId);
            e.Property(x => x.CanvasSettings).HasColumnType("jsonb");
            e.Property(x => x.Status).HasConversion<string>();
        });

        // === Seat ===
        modelBuilder.Entity<Seat>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.SeatMap)
                .WithMany(m => m.Seats)
                .HasForeignKey(x => x.SeatMapId)
                .OnDelete(DeleteBehavior.Cascade);
            // Unique label per seat map (partial index excludes soft-deleted rows)
            e.HasIndex(x => new { x.SeatMapId, x.Label })
                .IsUnique()
                .HasFilter("is_deleted = false")
                .HasDatabaseName("ix_seats_seat_map_id_label");
            e.Property(x => x.Position).HasColumnType("jsonb");
            e.Property(x => x.CustomProperties).HasColumnType("jsonb");
            e.Property(x => x.Status).HasConversion<string>();

            // STORED generated columns derived from the Position JSONB — enable bbox filtering.
            // Postgres backfills these for all existing rows when the column is added.
            e.Property(x => x.PositionX)
                .HasComputedColumnSql("((position ->> 'x'))::double precision", stored: true);
            e.Property(x => x.PositionY)
                .HasComputedColumnSql("((position ->> 'y'))::double precision", stored: true);
            e.HasIndex(x => new { x.SeatMapId, x.PositionX, x.PositionY })
                .HasFilter("is_deleted = false")
                .HasDatabaseName("ix_seats_map_position");
        });

        // === SeatObject ===
        modelBuilder.Entity<SeatObject>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.SeatMap)
                .WithMany(m => m.Objects)
                .HasForeignKey(x => x.SeatMapId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Geometry).HasColumnType("jsonb");
            e.Property(x => x.Style).HasColumnType("jsonb");
            e.Property(x => x.ObjectType).HasConversion<string>();
        });

        // === SeatMapVersion ===
        modelBuilder.Entity<SeatMapVersion>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.SeatMap)
                .WithMany(m => m.Versions)
                .HasForeignKey(x => x.SeatMapId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Snapshot).HasColumnType("jsonb");
        });

        // Global soft-delete filter
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(SeatDbContext)
                    .GetMethod(
                        nameof(AddIsDeletedFilter),
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(null, new object[] { modelBuilder });
            }
        }
    }

    private static void AddIsDeletedFilter<TEntity>(ModelBuilder builder)
        where TEntity : BaseEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }
}
