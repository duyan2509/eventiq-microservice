using Eventiq.PaymentService.Domain.Entity;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.PaymentService.Infrastructure.Persistence;

public sealed class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

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
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.PlatformFee).HasPrecision(18, 2);
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
