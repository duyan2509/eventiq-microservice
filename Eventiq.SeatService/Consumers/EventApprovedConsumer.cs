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

        // Publish all templates first (one per unique chart) so clone consumers find them ready
        var processedCharts = new HashSet<Guid>();
        foreach (var pair in msg.Sessions)
        {
            if (!processedCharts.Add(pair.ChartId)) continue;

            var template = await _uow.SeatMaps.GetTemplateByChartIdWithDetailsAsync(pair.ChartId);
            if (template == null)
            {
                _logger.LogWarning(
                    "No seat map template for ChartId={ChartId}, skipping", pair.ChartId);
                continue;
            }

            if (template.Status != SeatMapStatus.Published)
            {
                template.Publish();
                await _uow.SeatMaps.UpdateAsync(template);
                _logger.LogInformation(
                    "Published template {TemplateId} for ChartId={ChartId}", template.Id, pair.ChartId);
            }
        }

        await _uow.SaveChangesAsync();

        // Enqueue one clone job per session — processed independently in background
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
