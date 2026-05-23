using Eventiq.Contracts;
using Eventiq.SeatService.Domain.Entity;
using Eventiq.SeatService.Domain.Enum;
using MassTransit;

namespace Eventiq.SeatService.Consumers;

public class SessionSeatMapCloneConsumer : IConsumer<SessionSeatMapCloneRequested>
{
    private readonly ILogger<SessionSeatMapCloneConsumer> _logger;
    private readonly IUnitOfWork _uow;

    public SessionSeatMapCloneConsumer(ILogger<SessionSeatMapCloneConsumer> logger, IUnitOfWork uow)
    {
        _logger = logger;
        _uow = uow;
    }

    public async Task Consume(ConsumeContext<SessionSeatMapCloneRequested> context)
    {
        var msg = context.Message;

        var existing = await _uow.SeatMaps.GetBySessionIdAsync(msg.SessionId);
        if (existing != null)
        {
            _logger.LogInformation(
                "Clone already exists for SessionId={SessionId}, skipping", msg.SessionId);
            return;
        }

        var template = await _uow.SeatMaps.GetPublishedTemplateByChartIdAsync(msg.ChartId);
        if (template == null)
        {
            _logger.LogWarning(
                "No published template for ChartId={ChartId}, cannot clone SessionId={SessionId}",
                msg.ChartId, msg.SessionId);
            throw new InvalidOperationException(
                $"Published template not found for ChartId={msg.ChartId}");
        }

        var clone = CloneSeatMap(template, msg.SessionId);
        await _uow.SeatMaps.AddAsync(clone);
        await _uow.SaveChangesAsync();

        _logger.LogInformation(
            "Cloned SeatMap {CloneId} for SessionId={SessionId} from template {TemplateId}",
            clone.Id, msg.SessionId, template.Id);
    }

    private static SeatMap CloneSeatMap(SeatMap template, Guid sessionId)
    {
        var clone = new SeatMap
        {
            Id = Guid.NewGuid(),
            ChartId = template.ChartId,
            EventId = template.EventId,
            OrganizationId = template.OrganizationId,
            SessionId = sessionId,
            Name = template.Name,
            Status = SeatMapStatus.Published,
            Version = 1,
            TotalSeats = template.Seats.Count,
            CanvasSettings = template.CanvasSettings,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var seat in template.Seats)
        {
            clone.Seats.Add(new Seat
            {
                Id = Guid.NewGuid(),
                SeatMapId = clone.Id,
                Label = seat.Label,
                SeatNumber = seat.SeatNumber,
                SeatType = seat.SeatType,
                Status = SeatStatus.Available,
                Position = seat.Position,
                LegendId = seat.LegendId,
                CustomProperties = seat.CustomProperties,
                CreatedAt = DateTime.UtcNow
            });
        }

        foreach (var obj in template.Objects)
        {
            clone.Objects.Add(new SeatObject
            {
                Id = Guid.NewGuid(),
                SeatMapId = clone.Id,
                ObjectType = obj.ObjectType,
                Label = obj.Label,
                Geometry = obj.Geometry,
                Style = obj.Style,
                ZIndex = obj.ZIndex,
                CreatedAt = DateTime.UtcNow
            });
        }

        return clone;
    }
}
