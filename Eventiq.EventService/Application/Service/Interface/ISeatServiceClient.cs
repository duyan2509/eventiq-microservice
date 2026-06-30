namespace Eventiq.EventService.Application.Service;

public interface ISeatServiceClient
{
    Task<Guid?> InitSessionSeatMapAsync(Guid sessionId, Guid chartId, Guid eventId);
}
