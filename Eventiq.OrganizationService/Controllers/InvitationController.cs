using System.Security.Claims;
using Eventiq.Contracts;
using Eventiq.OrganizationService.Application.Service;
using Eventiq.OrganizationService.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.OrganizationService.Controllers;

[ApiController]
[Route("api/organizations/{orgId:guid}/invitations")]
public class InvitationController : ControllerBase
{
    private readonly IInvitationService _invitationService;
    private readonly ILogger<InvitationController> _logger;

    public InvitationController(
        IInvitationService invitationService,
        ILogger<InvitationController> logger)
    {
        _invitationService = invitationService;
        _logger = logger;
    }

    [Authorize(Roles = nameof(AppRoles.Organization))]
    [HttpPost]
    public async Task<ActionResult<InviationResponse>> CreateInvitation(
        Guid orgId,
        [FromBody] CreateInvitationDto dto,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");

        if (string.IsNullOrWhiteSpace(dto.UserEmail))
            throw new BadRequestException("User email is required");

        var result = await _invitationService.AddInvitationAsync(
            dto.UserEmail,
            userId,
            orgId,
            new InvitationDto { PermissionId = dto.PermissionId },
            cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = nameof(AppRoles.Organization))]
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<InviationResponse>>> GetOrgInvitations(
        Guid orgId,
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");

        if (page <= 0 || size <= 0)
            throw new BadRequestException("Page and size must be greater than 0");

        var result = await _invitationService.GetOrgInvitationsAsync(userId, orgId, page, size, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = nameof(AppRoles.Organization))]
    [HttpPost("{invitationId:guid}/cancel")]
    public async Task<ActionResult<InviationResponse>> CancelInvitation(
        Guid orgId,
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");

        var result = await _invitationService.CancelInvitationAsync(userId, orgId, invitationId, cancellationToken);
        return Ok(result);
    }
}

[ApiController]
[Route("api/invitations")]
public class UserInvitationController : ControllerBase
{
    private readonly IInvitationService _invitationService;
    private readonly ILogger<UserInvitationController> _logger;

    public UserInvitationController(
        IInvitationService invitationService,
        ILogger<UserInvitationController> logger)
    {
        _invitationService = invitationService;
        _logger = logger;
    }

    [Authorize(Roles = $"{nameof(AppRoles.Organization)},{nameof(AppRoles.Staff)},{nameof(AppRoles.User)}")]
    [HttpGet("me")]
    public async Task<ActionResult<PaginatedResult<InviationResponse>>> GetMyInvitations(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");

        if (page <= 0 || size <= 0)
            throw new BadRequestException("Page and size must be greater than 0");

        var result = await _invitationService.GetUserInvitationsAsync(userId, page, size, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("{orgId:guid}/{invitationId:guid}/accept")]
    public async Task<ActionResult<InviationResponse>> AcceptInvitation(
        Guid orgId,
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");

        var result = await _invitationService.AcceptInvitationAsync(userId, orgId, invitationId, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("{orgId:guid}/{invitationId:guid}/reject")]
    public async Task<ActionResult<InviationResponse>> RejectInvitation(
        Guid orgId,
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");

        var result = await _invitationService.RejectInvitationAsync(userId, orgId, invitationId, cancellationToken);
        return Ok(result);
    }
}
