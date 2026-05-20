namespace Eventiq.Contracts;

public record EventApproved
{
    public Guid EventId { get; init; }
    public SessionChartPair[] Sessions { get; init; } = [];
    public DateTime ApprovedAt { get; init; }
}

public record SessionChartPair(Guid SessionId, Guid ChartId);
