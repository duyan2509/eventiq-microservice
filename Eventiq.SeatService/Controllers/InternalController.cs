using Eventiq.SeatService.Application.Service.Interface;
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

    [HttpGet("seat-maps/has-design")]
    public async Task<ActionResult<HasDesignResponse>> HasSeatMapDesign([FromQuery] Guid eventId)
    {
        var hasDesign = await _seatMapService.HasSeatMapDesignAsync(eventId);
        return Ok(new HasDesignResponse(hasDesign));
    }
}

public record PublishedCheckResponse(bool HasSeatMap);
public record HasDesignResponse(bool HasDesign);
