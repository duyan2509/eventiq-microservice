namespace Eventiq.SeatService.Domain.Entity;

public class SeatRow : BaseEntity
{
    public Guid SectionId { get; set; }
    public virtual SeatSection Section { get; set; } = null!;
    
    public string Label { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    
    /// <summary>
    /// JSONB: curvature settings for arc layouts
    /// </summary>
    public string? Curve { get; set; }
    
    /// <summary>
    /// Pixel spacing between seats
    /// </summary>
    public int SeatSpacing { get; set; } = 30;
    
    public virtual ICollection<Seat> Seats { get; set; } = new List<Seat>();
}
