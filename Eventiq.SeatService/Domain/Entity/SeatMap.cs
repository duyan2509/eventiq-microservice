using Eventiq.SeatService.Domain.Enum;

namespace Eventiq.SeatService.Domain.Entity;

public class SeatMap : BaseEntity
{
    public Guid ChartId { get; set; }
    public Guid EventId { get; set; }
    public Guid OrganizationId { get; set; }
    /// <summary>
    /// Null = designer template. Set when cloned per-session on event approval.
    /// </summary>
    public Guid? SessionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SeatMapStatus Status { get; set; } = SeatMapStatus.Draft;

    /// <summary>
    /// JSONB: { width, height, zoom, backgroundColor }
    /// </summary>
    public string? CanvasSettings { get; set; }

    /// <summary>
    /// Optimistic concurrency version for collaborative editing.
    /// </summary>
    public int Version { get; set; } = 1;
    public int TotalSeats { get; set; }
    public int NextSeatNumber { get; set; }

    public virtual ICollection<Seat> Seats { get; set; } = new List<Seat>();
    public virtual ICollection<SeatObject> Objects { get; set; } = new List<SeatObject>();
    public virtual ICollection<SeatMapVersion> Versions { get; set; } = new List<SeatMapVersion>();

    public void IncrementVersion()
    {
        Version++;
        MarkUpdated();
    }

    public void Publish()
    {
        if (Status != SeatMapStatus.Draft)
            throw new InvalidOperationException("Only draft seat maps can be published.");
        Status = SeatMapStatus.Published;
        MarkUpdated();
    }

    public void Archive()
    {
        Status = SeatMapStatus.Archived;
        MarkUpdated();
    }
}
