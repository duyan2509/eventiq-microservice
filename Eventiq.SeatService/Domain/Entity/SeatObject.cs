using Eventiq.SeatService.Domain.Enum;

namespace Eventiq.SeatService.Domain.Entity;

public class SeatObject : BaseEntity
{
    public Guid SeatMapId { get; set; }
    public virtual SeatMap SeatMap { get; set; } = null!;
    
    public SeatObjectType ObjectType { get; set; }
    public string? Label { get; set; }
    
    /// <summary>
    /// JSONB: { x, y, width, height, rotation }
    /// </summary>
    public string? Geometry { get; set; }
    
    /// <summary>
    /// JSONB: { fill, stroke, fontSize, fontFamily }
    /// </summary>
    public string? Style { get; set; }
    
    public int ZIndex { get; set; }
}
