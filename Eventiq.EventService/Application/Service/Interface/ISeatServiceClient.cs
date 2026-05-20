namespace Eventiq.EventService.Application.Service;

public interface ISeatServiceClient
{
    Task<bool> HasPublishedSeatMapAsync(Guid eventId);
}
