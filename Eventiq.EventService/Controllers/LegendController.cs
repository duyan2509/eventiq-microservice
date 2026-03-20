using System.Security.Claims;
using Eventiq.Contracts;
using Eventiq.EventService.Application.Service;
using Eventiq.EventService.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.EventService.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/legends")]
public class LegendController : ControllerBase
{
    private readonly ILegendService _legendService;
    private readonly ILogger<LegendController> _logger;

    public LegendController(ILegendService legendService, ILogger<LegendController> logger)
    {
        _legendService = legendService;
        _logger = logger;
    }

    [Authorize(Roles = $"{nameof(AppRoles.Organization)},{nameof(AppRoles.Staff)}")]
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<LegendResponse>>> GetAllLegends(
        Guid eventId,
        [FromQuery] int page = 1,
        [FromQuery] int size = 10)
    {
        if (page <= 0 || size <= 0)
            throw new BadRequestException("Page and size must be greater than 0");

        var result = await _legendService.GetAllLegendsByEventIdAsync(eventId, page, size);
        return Ok(result);
    }

    [Authorize(Roles = $"{nameof(AppRoles.Organization)},{nameof(AppRoles.Staff)}")]
    [HttpPost("{orgId:guid}")]
    public async Task<ActionResult<LegendResponse>> CreateLegend(
        Guid eventId,
        Guid orgId,
        [FromBody] CreateLegendDto dto)
    {
        var userId = GetUserId();
        var result = await _legendService.CreateLegendAsync(userId, orgId, eventId, dto);
        return Ok(result);
    }

    [Authorize(Roles = $"{nameof(AppRoles.Organization)},{nameof(AppRoles.Staff)}")]
    [HttpPatch("{legendId:guid}/{orgId:guid}")]
    public async Task<ActionResult<LegendResponse>> UpdateLegend(
        Guid eventId,
        Guid legendId,
        Guid orgId,
        [FromBody] UpdateLegendDto dto)
    {
        var userId = GetUserId();
        var result = await _legendService.UpdateLegendAsync(userId, orgId, eventId, legendId, dto);
        return Ok(result);
    }

    [Authorize(Roles = nameof(AppRoles.Organization))]
    [HttpDelete("{legendId:guid}/{orgId:guid}")]
    public async Task<ActionResult> DeleteLegend(
        Guid eventId,
        Guid legendId,
        Guid orgId)
    {
        await _legendService.DeleteLegendAsync(eventId, orgId, legendId);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");
        return userId;
    }
}
