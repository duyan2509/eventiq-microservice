namespace Eventiq.EventService.Domain.Entity;

public class Ticket : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid SessionId { get; set; }
    public Guid SeatId { get; set; }
    public string SeatLabel { get; set; } = string.Empty;
    public string LegendName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string QRCode { get; set; } = string.Empty;
    public bool IsCheckedIn { get; set; } = false;
    public DateTime? CheckedInAt { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}
