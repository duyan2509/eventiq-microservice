using Eventiq.EventService.Domain.Entity;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using DomainEvent = Eventiq.EventService.Domain.Entity.Event;

namespace Eventiq.EventService.Infrastructure.Persistence;

public sealed class EvtEventDbContext : DbContext
{
    public EvtEventDbContext(DbContextOptions<EvtEventDbContext> options)
        : base(options)
    {
    }

    public DbSet<DomainEvent> Events => Set<DomainEvent>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<Legend> Legends => Set<Legend>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Chart> Charts => Set<Chart>();
    public DbSet<OrgPaymentInfo> OrgPaymentInfos => Set<OrgPaymentInfo>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("event_service");

        modelBuilder.Entity<DomainEvent>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<Submission>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<Legend>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<Session>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<Chart>(e => e.HasKey(x => x.Id));

        modelBuilder.Entity<OrgPaymentInfo>(e =>
        {
            e.ToTable("org_payment_info");
            e.HasKey(x => x.OrganizationId);
            e.Property(x => x.OrganizationId).ValueGeneratedNever();
        });

        modelBuilder.Entity<Ticket>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrderId);
            e.HasIndex(x => x.SessionId);
            e.HasIndex(x => x.SeatId);
            e.Property(x => x.Price).HasPrecision(18, 2);
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

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
