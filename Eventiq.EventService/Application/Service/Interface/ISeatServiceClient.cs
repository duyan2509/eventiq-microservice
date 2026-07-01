namespace Eventiq.EventService.Application.Service;

public interface ISeatServiceClient
{
    Task<Guid?> InitSessionSeatMapAsync(Guid sessionId, Guid chartId, Guid eventId);
    Task<bool> IsLegendUsedInTemplateAsync(Guid legendId);
    Task<bool> HasSeatMapForChartAsync(Guid chartId);
}
