using Eventiq.PaymentService.Domain.Entity;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.PaymentService.Infrastructure.Persistence;

public sealed class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();

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
