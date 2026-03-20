using Eventiq.SeatService.Domain.Enum;

namespace Eventiq.SeatService.Domain.Entity;

public class SeatSection : BaseEntity
{
    public Guid SeatMapId { get; set; }
    public virtual SeatMap SeatMap { get; set; } = null!;
    
    public string Label { get; set; } = string.Empty;
    public SectionType SectionType { get; set; } = SectionType.Rectangle;
    
    /// <summary>
    /// JSONB: { x, y, width, height, rotation, points[] }
    /// </summary>
    public string? Geometry { get; set; }
    
    /// <summary>
    /// JSONB: { fill, stroke, opacity }
    /// </summary>
    public string? Style { get; set; }
    
    /// <summary>
    /// Reference to Legend in EventService for price tier / color
    /// </summary>
    public Guid? LegendId { get; set; }
    
    public int SortOrder { get; set; }
    
    public virtual ICollection<SeatRow> Rows { get; set; } = new List<SeatRow>();
}
