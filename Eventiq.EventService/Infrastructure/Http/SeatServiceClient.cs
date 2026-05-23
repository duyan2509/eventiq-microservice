using Eventiq.Contracts.Grpc;
using Eventiq.EventService.Application.Service;

namespace Eventiq.EventService.Infrastructure.Http;

public class SeatServiceClient : ISeatServiceClient
{
    private readonly SeatInternal.SeatInternalClient _grpcClient;
    private readonly ILogger<SeatServiceClient> _logger;

    public SeatServiceClient(SeatInternal.SeatInternalClient grpcClient, ILogger<SeatServiceClient> logger)
    {
        _grpcClient = grpcClient;
        _logger = logger;
    }

    public async Task<bool> HasPublishedSeatMapAsync(Guid eventId)
    {
        try
        {
            var response = await _grpcClient.CheckSeatMapPublishedAsync(
                new CheckSeatMapPublishedRequest { EventId = eventId.ToString() });
            return response.IsPublished;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check published seat map for event {EventId}", eventId);
            return false;
        }
    }

    public async Task<bool> HasSeatMapDesignAsync(Guid eventId)
    {
        try
        {
            var response = await _grpcClient.CheckSeatMapDesignAsync(
                new CheckSeatMapDesignRequest { EventId = eventId.ToString() });
            return response.HasDesign;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check seat map design for event {EventId}", eventId);
            return false;
        }
    }
}
