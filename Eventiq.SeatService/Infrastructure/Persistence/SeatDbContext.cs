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
    public DbSet<SeatSection> Sections => Set<SeatSection>();
    public DbSet<SeatRow> Rows => Set<SeatRow>();
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
            e.HasIndex(x => x.ChartId).IsUnique();
            e.HasIndex(x => x.EventId);
            e.HasIndex(x => x.OrganizationId);
            e.Property(x => x.CanvasSettings).HasColumnType("jsonb");
            e.Property(x => x.Status).HasConversion<string>();
        });

        // === SeatSection ===
        modelBuilder.Entity<SeatSection>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.SeatMap)
                .WithMany(m => m.Sections)
                .HasForeignKey(x => x.SeatMapId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Geometry).HasColumnType("jsonb");
            e.Property(x => x.Style).HasColumnType("jsonb");
            e.Property(x => x.SectionType).HasConversion<string>();
        });

        // === SeatRow ===
        modelBuilder.Entity<SeatRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Section)
                .WithMany(s => s.Rows)
                .HasForeignKey(x => x.SectionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Curve).HasColumnType("jsonb");
        });

        // === Seat ===
        modelBuilder.Entity<Seat>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Row)
                .WithMany(r => r.Seats)
                .HasForeignKey(x => x.RowId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Position).HasColumnType("jsonb");
            e.Property(x => x.CustomProperties).HasColumnType("jsonb");
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.SeatType).HasConversion<string>();
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
