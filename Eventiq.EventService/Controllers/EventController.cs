using System.Security.Claims;
using Eventiq.Contracts;
using Eventiq.EventService.Application.Service;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.EventService.Controllers;

[ApiController]
[Route("api/events")]
public class EventController : ControllerBase
{
    private readonly IEventService _eventService;
    private readonly ILogger<EventController> _logger;

    public EventController(IEventService eventService, ILogger<EventController> logger)
    {
        _eventService = eventService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResult<EventQuickViewData>>> GetAllEvents(
        [FromQuery] string? query,
        [FromQuery] EventStatus? status,
        [FromQuery] string? province,
        [FromQuery] bool newest = true,
        [FromQuery] bool increasePrice = true,
        [FromQuery] int page = 1,
        [FromQuery] int size = 10)
    {
        if (page <= 0 || size <= 0)
            throw new BadRequestException("Page and size must be greater than 0");

        var result = await _eventService.GetAllEventsAsync(query, status, province, newest, increasePrice, page, size);
        return Ok(result);
    }

    [Authorize(Roles = $"{nameof(AppRoles.Organization)},{nameof(AppRoles.Staff)}")]
    [HttpPost("{orgId:guid}")]
    public async Task<ActionResult<EventQuickViewData>> CreateEvent(
        Guid orgId,
        [FromBody] CreateEventDto dto)
    {
        var userId = GetUserId();
        var result = await _eventService.CreateEventAsync(userId, orgId, dto);
        return Ok(result);
    }

    [HttpGet("{eventId:guid}")]
    public async Task<ActionResult<EventDetail>> GetDetailEvent(Guid eventId)
    {
        var userId = GetUserIdOrDefault();
        var result = await _eventService.GetDetailEventAsync(userId, eventId);
        return Ok(result);
    }

    [Authorize(Roles = $"{nameof(AppRoles.Organization)},{nameof(AppRoles.Staff)}")]
    [HttpPatch("{eventId:guid}")]
    public async Task<ActionResult<EventQuickViewData>> UpdateEvent(
        Guid eventId,
        [FromBody] UpdateEventDto dto)
    {
        var userId = GetUserId();
        var result = await _eventService.UpdateEventAsync(userId, eventId, dto);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");
        return userId;
    }

    private Guid GetUserIdOrDefault()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userIdStr) && Guid.TryParse(userIdStr, out var userId))
            return userId;
        return Guid.Empty;
    }
}
