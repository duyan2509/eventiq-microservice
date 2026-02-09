using System.Security.Claims;
using Eventiq.Contracts;
using Eventiq.OrganizationService.Application.Service;
using Eventiq.OrganizationService.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Operations;

namespace Eventiq.OrganizationService.Controllers;

[ApiController]
[Route("api/organizations")]
public class OrganizationController : ControllerBase
{
    private readonly IOrganizationService _organizationService;
    private readonly ILogger<OrganizationController> _logger;

    public OrganizationController(
        IOrganizationService organizationService,
        ILogger<OrganizationController> logger)
    {
        _organizationService = organizationService;
        _logger = logger;
    }
    [Authorize(Roles = nameof(AppRoles.Admin))]
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<OrganizationDetail>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0 || size <= 0)
            throw new BadRequestException("Page and size must be greater than 0");

        var result = await _organizationService.GetAllAsync(page, size, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrganizationDetail>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await _organizationService.GetByIdAsync(id, cancellationToken);
        if (item == null)
            throw new NotFoundException($"Organization with id {id} does not exist");
        return Ok(item);
    }
    [Authorize(Roles = $"{nameof(AppRoles.Organization)},{nameof(AppRoles.Staff)}")]
    [HttpGet("me")]
    public async Task<ActionResult<PaginatedResult<OrganizationDetail>>> GetMyOrganizations(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");

        if (page <= 0 || size <= 0)
            throw new BadRequestException("Page and size must be greater than 0");

        var result = await _organizationService.GetMyOrganizationsAsync(userId, page, size, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("create")]
    public async Task<ActionResult<OrganizationResponse>> Create(
        [FromBody] OrganizationDto dto,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");

        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        if (string.IsNullOrEmpty(email))
            throw new UnauthorizedException("User email is required");

        var result = await _organizationService.AddAsync(userId, email, dto, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = nameof(AppRoles.Organization))]
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<OrganizationResponse>> Update(
        Guid id,
        [FromBody] UpdateOrganizationDto dto,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("User id is required");

        var result = await _organizationService.UpdateAsync(userId, id, dto, cancellationToken);
        return Ok(result);
    }
}
