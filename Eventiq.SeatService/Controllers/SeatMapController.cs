using Eventiq.SeatService.Application.Dtos;
using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Eventiq.SeatService.Controllers;

[ApiController]
[Route("api/seat-maps")]
[Authorize]
public class SeatMapController : ControllerBase
{
    private readonly ISeatMapService _seatMapService;
    private readonly IOutputCacheStore _cacheStore;

    public SeatMapController(ISeatMapService seatMapService, IOutputCacheStore cacheStore)
    {
        _seatMapService = seatMapService;
        _cacheStore = cacheStore;
    }

    [HttpGet]
    public async Task<IActionResult> GetByEventId([FromQuery] Guid eventId)
    {
        var result = await _seatMapService.GetByEventIdAsync(eventId);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _seatMapService.GetByIdAsync(id);
        return Ok(result);
    }

    [HttpGet("sessions/{sessionId:guid}")]
    [OutputCache(PolicyName = OutputCachePolicies.SeatMapLayout)]
    public async Task<IActionResult> GetBySessionId(Guid sessionId)
    {
        var result = await _seatMapService.GetBySessionIdAsync(sessionId);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSeatMapDto dto)
    {
        var userId = GetUserId();
        var orgId = GetOrgId();
        var result = await _seatMapService.CreateAsync(userId, orgId, dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}/settings")]
    public async Task<IActionResult> UpdateSettings(Guid id, [FromBody] UpdateSeatMapSettingsDto dto)
    {
        var userId = GetUserId();
        var orgId = GetOrgId();
        var result = await _seatMapService.UpdateSettingsAsync(userId, orgId, id, dto);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var orgId = GetOrgId();
        await _seatMapService.DeleteAsync(orgId, id);
        return NoContent();
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id)
    {
        var orgId = GetOrgId();
        var result = await _seatMapService.PublishAsync(orgId, id);

        // Evict cached session layout so the next request picks up the published state.
        await _cacheStore.EvictByTagAsync(OutputCachePolicies.SeatMapLayoutTag, default);

        return Ok(result);
    }

    [HttpGet("{id:guid}/stats")]
    public async Task<IActionResult> GetStats(Guid id)
    {
        var result = await _seatMapService.GetStatsAsync(id);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var userId) ? userId : throw new UnauthorizedException("Invalid user.");
    }

    private Guid GetOrgId()
    {
        var orgClaim = User.FindFirst("org_id")?.Value;
        return Guid.TryParse(orgClaim, out var orgId) ? orgId : throw new UnauthorizedException("Organization not found in token.");
    }
}
