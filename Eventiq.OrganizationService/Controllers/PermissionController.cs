using System.Security.Claims;
using Eventiq.Contracts;
using Eventiq.OrganizationService.Application.Service;
using Eventiq.OrganizationService.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.OrganizationService.Controllers;

[ApiController]
[Route("api/organizations/{orgId:guid}/permissions")]
public class PermissionController : ControllerBase
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<PermissionController> _logger;

    public PermissionController(
        IPermissionService permissionService,
        ILogger<PermissionController> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    [Authorize(Roles = $"{nameof(AppRoles.Organization)},{nameof(AppRoles.Staff)}")]
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<PermissionResponse>>> GetPermissions(
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

        var result = await _permissionService.GetPermissionsAsync(userId, orgId, page, size, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = nameof(AppRoles.Organization))]
    [HttpPost]
    public async Task<ActionResult<PermissionResponse>> CreatePermission(
        Guid orgId,
        [FromBody] PermissionDto dto,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");

        var result = await _permissionService.AddPermissionAsync(userId, orgId, dto, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = nameof(AppRoles.Organization))]
    [HttpPatch("{permissionId:guid}")]
    public async Task<ActionResult<PermissionResponse>> UpdatePermission(
        Guid orgId,
        Guid permissionId,
        [FromBody] UpdatePermissionDto dto,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");

        var result = await _permissionService.UpdatePermissionAsync(userId, orgId, permissionId, dto, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = nameof(AppRoles.Organization))]
    [HttpDelete("{permissionId:guid}")]
    public async Task<ActionResult> DeletePermission(
        Guid orgId,
        Guid permissionId,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");

        await _permissionService.DeletePermissionAsync(userId, orgId, permissionId, cancellationToken);
        return NoContent();
    }
}
