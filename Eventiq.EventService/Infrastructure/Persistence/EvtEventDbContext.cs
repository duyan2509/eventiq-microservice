using Eventiq.EventService.Domain.Entity;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.EventService.Infrastructure.Persistence;

public sealed class EvtEventDbContext : DbContext
{
    public EvtEventDbContext(DbContextOptions<EvtEventDbContext> options)
        : base(options)
    {
    }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<Legend> Legends => Set<Legend>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Chart> Charts => Set<Chart>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Event>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<Submission>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<Legend>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<Session>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<Chart>(e => e.HasKey(x => x.Id));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(EvtEventDbContext)
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
