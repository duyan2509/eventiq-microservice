using System.Net.Http.Json;
using Eventiq.EventService.Application.Service;

namespace Eventiq.EventService.Infrastructure.Http;

public class SeatServiceClient : ISeatServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SeatServiceClient> _logger;

    public SeatServiceClient(HttpClient httpClient, ILogger<SeatServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> HasPublishedSeatMapAsync(Guid eventId)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<PublishedCheckResponse>(
                $"api/internal/seat-maps/published?eventId={eventId}");
            return result?.HasSeatMap == true;
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
            var result = await _httpClient.GetFromJsonAsync<HasDesignResponse>(
                $"api/internal/seat-maps/has-design?eventId={eventId}");
            return result?.HasDesign == true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check seat map design for event {EventId}", eventId);
            return false;
        }
    }

    private record PublishedCheckResponse(bool HasSeatMap);
    private record HasDesignResponse(bool HasDesign);
}
