using Eventiq.Contracts;
using Eventiq.SeatService.Domain.Enum;
using MassTransit;

namespace Eventiq.SeatService.Consumers;

public class EventApprovedConsumer : IConsumer<EventApproved>
{
    private readonly ILogger<EventApprovedConsumer> _logger;
    private readonly IUnitOfWork _uow;
    private readonly IPublishEndpoint _publishEndpoint;

    public EventApprovedConsumer(
        ILogger<EventApprovedConsumer> logger,
        IUnitOfWork uow,
        IPublishEndpoint publishEndpoint)
    {
        _logger = logger;
        _uow = uow;
        _publishEndpoint = publishEndpoint;
    }

    public async Task Consume(ConsumeContext<EventApproved> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "EventApproved received: EventId={EventId}, Sessions={Count}",
            msg.EventId, msg.Sessions.Length);

        // Load all seat map templates for this event (SessionId == null).
        // CheckSeatMapDesign only verified *some* template exists — it does not guarantee
        // that each session's ChartId has a 1:1 matching template. We publish every
        // Draft template and then let SessionSeatMapCloneConsumer pick the right one
        // (by ChartId first, event fallback second).
        var allMaps = await _uow.SeatMaps.GetByEventIdAsync(msg.EventId);
        var templates = allMaps.Where(m => m.SessionId == null).ToList();

        if (templates.Count == 0)
        {
            _logger.LogWarning(
                "No seat map template found for EventId={EventId}, skipping all clones",
                msg.EventId);
            return;
        }

        // Publish every Draft template so clone consumers can find them.
        bool needsSave = false;
        foreach (var t in templates)
        {
            if (t.Status == SeatMapStatus.Draft)
            {
                t.Publish();
                await _uow.SeatMaps.UpdateAsync(t);
                needsSave = true;
                _logger.LogInformation("Published template {TemplateId} for EventId={EventId}", t.Id, msg.EventId);
            }
        }
        if (needsSave) await _uow.SaveChangesAsync();

        // Enqueue a clone job for every session.
        // SessionSeatMapCloneConsumer resolves the template by ChartId then falls back
        // to any event-level template, so sessions without an exact ChartId match are
        // still covered.
        foreach (var pair in msg.Sessions)
        {
            await _publishEndpoint.Publish(new SessionSeatMapCloneRequested
            {
                SessionId = pair.SessionId,
                ChartId = pair.ChartId,
                EventId = msg.EventId
            });
        }

        _logger.LogInformation(
            "Enqueued {Count} SessionSeatMapCloneRequested messages for EventId={EventId}",
            msg.Sessions.Length, msg.EventId);
    }
}
