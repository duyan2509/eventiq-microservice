namespace Eventiq.EventService.Application.Service;

public interface ISeatServiceClient
{
    Task<bool> HasPublishedSeatMapAsync(Guid eventId);
    Task<bool> HasSeatMapDesignAsync(Guid eventId);
}
