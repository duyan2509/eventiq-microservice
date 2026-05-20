using Eventiq.Contracts;
using Eventiq.SeatService.Domain.Entity;
using Eventiq.SeatService.Domain.Enum;
using MassTransit;

namespace Eventiq.SeatService.Consumers;

public class EventApprovedConsumer : IConsumer<EventApproved>
{
    private readonly ILogger<EventApprovedConsumer> _logger;
    private readonly IUnitOfWork _uow;

    public EventApprovedConsumer(ILogger<EventApprovedConsumer> logger, IUnitOfWork uow)
    {
        _logger = logger;
        _uow = uow;
    }

    public async Task Consume(ConsumeContext<EventApproved> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "EventApproved received: EventId={EventId}, Sessions={Count}",
            msg.EventId, msg.Sessions.Length);

        try
        {
            foreach (var pair in msg.Sessions)
            {
                var template = await _uow.SeatMaps.GetPublishedTemplateByChartIdAsync(pair.ChartId);
                if (template == null)
                {
                    _logger.LogWarning(
                        "No published template for ChartId={ChartId}, skipping session {SessionId}",
                        pair.ChartId, pair.SessionId);
                    continue;
                }

                // Skip if clone already exists (idempotency)
                var existing = await _uow.SeatMaps.GetBySessionIdAsync(pair.SessionId);
                if (existing != null)
                {
                    _logger.LogInformation(
                        "Clone already exists for SessionId={SessionId}, skipping", pair.SessionId);
                    continue;
                }

                var clone = CloneSeatMap(template, pair.SessionId);
                await _uow.SeatMaps.AddAsync(clone);

                _logger.LogInformation(
                    "Cloned SeatMap {CloneId} for SessionId={SessionId} from template {TemplateId}",
                    clone.Id, pair.SessionId, template.Id);
            }

            await _uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing EventApproved for EventId={EventId}", msg.EventId);
            throw; // Let MassTransit retry
        }
    }

    private static SeatMap CloneSeatMap(SeatMap template, Guid sessionId)
    {
        var totalSeats = template.Sections
            .SelectMany(s => s.Rows)
            .SelectMany(r => r.Seats)
            .Count();

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
            TotalSeats = totalSeats,
            CanvasSettings = template.CanvasSettings,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var section in template.Sections)
        {
            var clonedSection = new SeatSection
            {
                Id = Guid.NewGuid(),
                SeatMapId = clone.Id,
                Label = section.Label,
                SectionType = section.SectionType,
                Geometry = section.Geometry,
                Style = section.Style,
                LegendId = section.LegendId,
                SortOrder = section.SortOrder,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var row in section.Rows)
            {
                var clonedRow = new SeatRow
                {
                    Id = Guid.NewGuid(),
                    SectionId = clonedSection.Id,
                    Label = row.Label,
                    RowNumber = row.RowNumber,
                    Curve = row.Curve,
                    SeatSpacing = row.SeatSpacing,
                    CreatedAt = DateTime.UtcNow
                };

                foreach (var seat in row.Seats)
                {
                    clonedRow.Seats.Add(new Seat
                    {
                        Id = Guid.NewGuid(),
                        RowId = clonedRow.Id,
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

                clonedSection.Rows.Add(clonedRow);
            }

            clone.Sections.Add(clonedSection);
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
