using Eventiq.SeatService.Domain.Enum;

namespace Eventiq.SeatService.Domain.Entity;

public class Seat : BaseEntity
{
    public Guid SeatMapId { get; set; }
    public virtual SeatMap SeatMap { get; set; } = null!;

    public string Label { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public SeatStatus Status { get; set; } = SeatStatus.Available;
    /// <summary>
    /// Numeric type: 1–4. Organizer defines meaning (e.g. 1 = standard, 2 = VIP).
    /// </summary>
    public int SeatType { get; set; } = 1;

    /// <summary>
    /// JSONB: { x, y } canvas coordinates
    /// </summary>
    public string? Position { get; set; }

    /// <summary>
    /// STORED generated columns derived from <see cref="Position"/> JSONB.
    /// Read-only — populated by Postgres so seats can be filtered by bounding box.
    /// </summary>
    public double? PositionX { get; private set; }
    public double? PositionY { get; private set; }

    public Guid? LegendId { get; set; }
    public string? CustomProperties { get; set; }

    public Guid? HeldBy { get; set; }
    public DateTime? HeldUntil { get; set; }

    // Per-property versioning for collaborative conflict detection.
    // Geometry covers position/label/seatType; style covers legendId.
    // Two concurrent writes on different groups never conflict.
    public int GeometryVersion { get; set; } = 1;
    public int StyleVersion { get; set; } = 1;

    public void IncrementGeometryVersion() => GeometryVersion++;
    public void IncrementStyleVersion() => StyleVersion++;

    public void Hold(Guid userId, TimeSpan duration)
    {
        Status = SeatStatus.Holding;
        HeldBy = userId;
        HeldUntil = DateTime.UtcNow.Add(duration);
        MarkUpdated();
    }

    public void Release()
    {
        Status = SeatStatus.Available;
        HeldBy = null;
        HeldUntil = null;
        MarkUpdated();
    }

    public void Sell()
    {
        Status = SeatStatus.Sold;
        HeldBy = null;
        HeldUntil = null;
        MarkUpdated();
    }
}
