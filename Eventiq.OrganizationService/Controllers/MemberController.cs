using System.Security.Claims;
using Eventiq.Contracts;
using Eventiq.OrganizationService.Application.Service;
using Eventiq.OrganizationService.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.OrganizationService.Controllers;

[ApiController]
[Route("api/organizations/{orgId:guid}/members")]
public class MemberController : ControllerBase
{
    private readonly IMemberService _memberService;
    private readonly ILogger<MemberController> _logger;

    public MemberController(
        IMemberService memberService,
        ILogger<MemberController> logger)
    {
        _memberService = memberService;
        _logger = logger;
    }

    [Authorize(Roles = $"{nameof(AppRoles.Organization)},{nameof(AppRoles.Staff)}")]
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<MemberReponse>>> GetMembers(
        Guid orgId,
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0 || size <= 0)
            throw new BadRequestException("Page and size must be greater than 0");

        var result = await _memberService.GetMembersAsync(orgId, page, size, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = nameof(AppRoles.Organization))]
    [HttpPatch("{memberId:guid}/permission")]
    public async Task<ActionResult<MemberReponse>> ChangeMemberPermission(
        Guid orgId,
        Guid memberId,
        [FromBody] ChangePermission dto,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");

        var result = await _memberService.ChangeMemberPermissionsAsync(userId, memberId, orgId, dto, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = nameof(AppRoles.Organization))]
    [HttpDelete("{memberId:guid}")]
    public async Task<ActionResult> DeleteMember(
        Guid orgId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");

        await _memberService.DeleteMemberAsync(userId, memberId, orgId, cancellationToken);
        return NoContent();
    }
}
