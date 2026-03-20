using System.Security.Claims;
using Eventiq.Contracts;
using Eventiq.EventService.Application.Service;
using Eventiq.EventService.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.EventService.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/submissions")]
public class SubmissionController : ControllerBase
{
    private readonly ISubmissionService _submissionService;
    private readonly ILogger<SubmissionController> _logger;

    public SubmissionController(ISubmissionService submissionService, ILogger<SubmissionController> logger)
    {
        _submissionService = submissionService;
        _logger = logger;
    }

    [Authorize(Roles = $"{nameof(AppRoles.Organization)},{nameof(AppRoles.Staff)},{nameof(AppRoles.Admin)}")]
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<SubmissionResponse>>> GetAllSubmissions(
        Guid eventId)
    {
        var userId = GetUserId();
        var result = await _submissionService.GetAllSubmissionByEventIdAsync(userId, eventId);
        return Ok(result);
    }

    [Authorize(Roles = $"{nameof(AppRoles.Organization)},{nameof(AppRoles.Staff)}")]
    [HttpPost("{orgId:guid}")]
    public async Task<ActionResult<SubmissionResponse>> SubmitEvent(
        Guid eventId,
        Guid orgId)
    {
        var userId = GetUserId();
        var result = await _submissionService.SubmitEventAsync(userId, orgId, eventId);
        return Ok(result);
    }

    [Authorize(Roles = nameof(AppRoles.Admin))]
    [HttpPost("accept")]
    public async Task<ActionResult<SubmissionResponse>> AcceptEvent(
        Guid eventId,
        [FromBody] UpdateSubmissioDto dto)
    {
        var userId = GetUserId();
        var adminEmail = GetUserEmail();
        var result = await _submissionService.AcceptEventAsync(userId, adminEmail, eventId, dto);
        return Ok(result);
    }

    [Authorize(Roles = nameof(AppRoles.Admin))]
    [HttpPost("reject")]
    public async Task<ActionResult<SubmissionResponse>> RejectEvent(
        Guid eventId,
        [FromBody] UpdateSubmissioDto dto)
    {
        var userId = GetUserId();
        var adminEmail = GetUserEmail();
        var result = await _submissionService.RejectEventAsync(userId, adminEmail, eventId, dto);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");
        return userId;
    }

    private string GetUserEmail()
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        if (string.IsNullOrEmpty(email))
            throw new UnauthorizedException("User email is required");
        return email;
    }
}
