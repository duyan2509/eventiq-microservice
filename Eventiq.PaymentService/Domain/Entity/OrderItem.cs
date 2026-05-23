namespace Eventiq.PaymentService.Domain.Entity;

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid SeatId { get; set; }
    public string SeatLabel { get; set; } = string.Empty;
    public string LegendName { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public Order Order { get; set; } = null!;
}
