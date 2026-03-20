using System.Security.Claims;
using Eventiq.Contracts;
using Eventiq.EventService.Application.Service;
using Eventiq.EventService.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.EventService.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/charts")]
public class ChartController : ControllerBase
{
    private readonly IChartService _chartService;
    private readonly ILogger<ChartController> _logger;

    public ChartController(IChartService chartService, ILogger<ChartController> logger)
    {
        _chartService = chartService;
        _logger = logger;
    }

    [Authorize(Roles = $"{nameof(AppRoles.Organization)},{nameof(AppRoles.Staff)}")]
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<ChartResponse>>> GetAllCharts(
        Guid eventId,
        [FromQuery] int page = 1,
        [FromQuery] int size = 10)
    {
        if (page <= 0 || size <= 0)
            throw new BadRequestException("Page and size must be greater than 0");

        var result = await _chartService.GetAllChartsByEventIdAsync(eventId, page, size);
        return Ok(result);
    }

    [Authorize(Roles = $"{nameof(AppRoles.Organization)},{nameof(AppRoles.Staff)}")]
    [HttpPost("{orgId:guid}")]
    public async Task<ActionResult<ChartResponse>> CreateChart(
        Guid eventId,
        Guid orgId,
        [FromBody] CreateChartDto dto)
    {
        var userId = GetUserId();
        var result = await _chartService.CreateChartAsync(userId, orgId, eventId, dto);
        return Ok(result);
    }

    [Authorize(Roles = $"{nameof(AppRoles.Organization)},{nameof(AppRoles.Staff)}")]
    [HttpPatch("{chartId:guid}/{orgId:guid}")]
    public async Task<ActionResult<ChartResponse>> UpdateChart(
        Guid eventId,
        Guid chartId,
        Guid orgId,
        [FromBody] UpdateChartDto dto)
    {
        var userId = GetUserId();
        var result = await _chartService.UpdateChartAsync(userId, orgId, eventId, chartId, dto);
        return Ok(result);
    }

    [Authorize(Roles = nameof(AppRoles.Organization))]
    [HttpDelete("{chartId:guid}/{orgId:guid}")]
    public async Task<ActionResult> DeleteChart(
        Guid eventId,
        Guid chartId,
        Guid orgId)
    {
        await _chartService.DeleteChartAsync(eventId, orgId, chartId);
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
