using Eventiq.PaymentService.Domain.Entity;
using Eventiq.PaymentService.Domain.Enums;

namespace Eventiq.PaymentService.Application.Service.Interface;

public interface IOrderSettlementService
{
    /// <summary>
    /// Settles a Pending order as Paid: marks it Paid, publishes <c>PaymentCompleted</c> to the
    /// outbox, and persists both atomically. Shared by the Stripe webhook and the background
    /// reconciliation job so both reach the same outcome through one code path.
    /// </summary>
    /// <remarks>
    /// The order must be tracked by the same scoped <c>PaymentDbContext</c> and have its
    /// <c>Items</c> loaded. No-ops (returns false) if the order is not Pending or if a concurrent
    /// transaction settled it first (<see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>).
    /// </remarks>
    /// <param name="source">Which path is settling the order — recorded on the order for tracing/demo.</param>
    /// <returns>true if this call transitioned the order to Paid; false otherwise.</returns>
    Task<bool> SettlePaidAsync(Order order, SettlementSource source);
}
