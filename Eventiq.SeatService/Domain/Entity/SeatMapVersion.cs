namespace Eventiq.SeatService.Domain.Entity;

public class SeatMapVersion : BaseEntity
{
    public Guid SeatMapId { get; set; }
    public virtual SeatMap SeatMap { get; set; } = null!;
    
    public int VersionNumber { get; set; }
    
    /// <summary>
    /// JSONB: Full serialized snapshot of the seat map state at this point
    /// </summary>
    public string Snapshot { get; set; } = string.Empty;
    
    public Guid CreatedBy { get; set; }
    public string? ChangeDescription { get; set; }
}
