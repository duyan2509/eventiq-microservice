using Eventiq.SeatService.Domain.Enum;

namespace Eventiq.SeatService.Domain.Entity;

public class Seat : BaseEntity
{
    public Guid RowId { get; set; }
    public virtual SeatRow Row { get; set; } = null!;
    
    public string Label { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public SeatStatus Status { get; set; } = SeatStatus.Available;
    public SeatType SeatType { get; set; } = SeatType.Regular;
    
    /// <summary>
    /// JSONB: { x, y } relative to row
    /// </summary>
    public string? Position { get; set; }
    
    /// <summary>
    /// Nullable override legend, else inherit from parent Section
    /// </summary>
    public Guid? LegendId { get; set; }
    
    /// <summary>
    /// JSONB: extra metadata (e.g. obstructed view, premium, etc.)
    /// </summary>
    public string? CustomProperties { get; set; }
}
