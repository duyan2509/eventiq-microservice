using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Domain.Enum;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.SeatService.Controllers;

[ApiController]
[Route("api/internal")]
public class InternalController : ControllerBase
{
    private readonly ISeatMapService _seatMapService;

    public InternalController(ISeatMapService seatMapService)
    {
        _seatMapService = seatMapService;
    }

    [HttpGet("seat-maps/published")]
    public async Task<ActionResult<PublishedCheckResponse>> HasPublishedSeatMap([FromQuery] Guid eventId)
    {
        var hasSeatMap = await _seatMapService.HasPublishedTemplateForEventAsync(eventId);
        return Ok(new PublishedCheckResponse(hasSeatMap));
    }
}

public record PublishedCheckResponse(bool HasSeatMap);
