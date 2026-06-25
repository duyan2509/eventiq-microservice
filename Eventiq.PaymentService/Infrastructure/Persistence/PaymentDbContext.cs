using Eventiq.PaymentService.Domain.Entity;
using Eventiq.PaymentService.Sagas;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.PaymentService.Infrastructure.Persistence;

public sealed class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<BookingSagaState> BookingSagas => Set<BookingSagaState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("payment_service");

        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.StripeSessionId).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.SessionId);
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.SettledBy).HasConversion<string>();
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.PlatformFee).HasPrecision(18, 2);
            // Optimistic concurrency on Postgres' system xmin column: when the webhook and the
            // reconciliation job race to settle the same order, the loser gets a
            // DbUpdateConcurrencyException instead of a double-settle. xmin is a system column,
            // so this maps an existing column and generates no schema change.
            // NB: keep this obsolete API on purpose — the "recommended" IsRowVersion() maps xmin
            // as a NEW column and emits a broken AddColumn migration. UseXminAsConcurrencyToken
            // is the only form that produces the correct no-op migration for a system column.
#pragma warning disable CS0618
            e.UseXminAsConcurrencyToken();
#pragma warning restore CS0618
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.OrderId);
            e.Property(x => x.Price).HasPrecision(18, 2);
        });

        modelBuilder.Entity<WebhookEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => x.StripeEventId);  // lookup for idempotency (code-level dedupe)
            e.HasIndex(x => x.Status);          // query Failed events for tracing
        });

        modelBuilder.Entity<BookingSagaState>(e =>
        {
            e.HasKey(x => x.CorrelationId);
            e.Property(x => x.CurrentState).HasMaxLength(64);
            e.Property(x => x.SeatIdsJson).HasColumnType("jsonb");
            e.Property(x => x.SeatItemsJson).HasColumnType("jsonb");
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(PaymentDbContext)
                    .GetMethod(nameof(AddIsDeletedFilter),
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(null, [modelBuilder]);
            }
        }
    }

    private static void AddIsDeletedFilter<TEntity>(ModelBuilder builder) where TEntity : BaseEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }
}
