using Eventiq.PaymentService.Domain.Enums;

namespace Eventiq.PaymentService.Domain.Entity;

public class Order : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid OrgId { get; set; }
    public Guid SessionId { get; set; }
    public string StripeSessionId { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; set; }
    public decimal PlatformFee { get; set; }

    // Snapshots to avoid cross-service joins at read time
    public string EventName { get; set; } = string.Empty;
    public string SessionName { get; set; } = string.Empty;
    public DateTime SessionDate { get; set; }
    public DateTime? PaidAt { get; set; }
}
