using System.Security.Claims;
using Eventiq.Contracts;
using Eventiq.EventService.Application.Service;
using Eventiq.EventService.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.EventService.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/sessions")]
public class SessionController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly ILogger<SessionController> _logger;

    public SessionController(ISessionService sessionService, ILogger<SessionController> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    [Authorize(Roles = $"{nameof(AppRoles.Organization)},{nameof(AppRoles.Staff)}")]
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<SessionResponse>>> GetAllSessions(
        Guid eventId,
        [FromQuery] int page = 1,
        [FromQuery] int size = 10)
    {
        if (page <= 0 || size <= 0)
            throw new BadRequestException("Page and size must be greater than 0");

        var result = await _sessionService.GetAllSessionByEventIdAsync(eventId, page, size);
        return Ok(result);
    }

    [Authorize(Roles = $"{nameof(AppRoles.Organization)},{nameof(AppRoles.Staff)}")]
    [HttpPost("{orgId:guid}")]
    public async Task<ActionResult<SessionResponse>> CreateSession(
        Guid eventId,
        Guid orgId,
        [FromBody] CreateSessionDto dto)
    {
        var userId = GetUserId();
        var result = await _sessionService.CreateSessionAsync(userId, orgId, eventId, dto);
        return Ok(result);
    }

    [Authorize(Roles = $"{nameof(AppRoles.Organization)},{nameof(AppRoles.Staff)}")]
    [HttpPatch("{sessionId:guid}/{orgId:guid}")]
    public async Task<ActionResult<SessionResponse>> UpdateSession(
        Guid eventId,
        Guid sessionId,
        Guid orgId,
        [FromBody] UpdateSessionDto dto)
    {
        var userId = GetUserId();
        var result = await _sessionService.UpdateSessionAsync(userId, orgId, eventId, sessionId, dto);
        return Ok(result);
    }

    [Authorize(Roles = nameof(AppRoles.Organization))]
    [HttpDelete("{sessionId:guid}/{orgId:guid}")]
    public async Task<ActionResult> DeleteSession(
        Guid eventId,
        Guid sessionId,
        Guid orgId)
    {
        await _sessionService.DeleteSessionAsync(eventId, orgId, sessionId);
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
