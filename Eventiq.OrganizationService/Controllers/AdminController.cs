using System.Security.Claims;
using Eventiq.Contracts;
using Eventiq.OrganizationService.Application.Service;
using Eventiq.OrganizationService.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.OrganizationService.Controllers;

[ApiController]
[Route("api/admin/platform-config")]
[Authorize(Roles = nameof(AppRoles.Admin))]
public class AdminController : ControllerBase
{
    private readonly IPlatformConfigService _configService;

    public AdminController(IPlatformConfigService configService)
    {
        _configService = configService;
    }

    [HttpGet]
    public async Task<ActionResult<PlatformConfigResponse>> Get(CancellationToken ct)
        => Ok(await _configService.GetAsync(ct));

    [HttpPut]
    public async Task<ActionResult<PlatformConfigResponse>> Update(
        [FromBody] UpdatePlatformConfigRequest request, CancellationToken ct)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await _configService.UpdateAsync(adminId, request, ct));
    }
}
