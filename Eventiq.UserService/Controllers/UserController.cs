using System.Security.Claims;
using Eventiq.UserService.Application.Dto;
using Eventiq.UserService.Application.Service;
using Eventiq.UserService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.UserService.Controllers;
[ApiController]
[Route("api/users")]
[Authorize(Roles=nameof(AppRoles.Admin))]
public class UserController:ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly IUserService _userService;

    public UserController(ILogger<AuthController> logger, IUserService userService)
    {
        _logger = logger;
        _userService = userService;
    }
    [HttpPatch("{userId}/ban")]
    public async Task<ActionResult> BanUser([FromRoute] Guid userId,[FromBody] BanUserRequest dto)
    {
        var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminIdStr) || !Guid.TryParse(adminIdStr, out var parsedAdminId))
            throw new UnauthorizedException("User id is required");
        var rs = await _userService.BanUser(parsedAdminId,userId, dto);
        return Ok(rs);
    }
    [HttpPatch("{userId}/unban")]
    public async Task<ActionResult> UnBanUser([FromRoute] Guid userId,[FromBody] BanUserRequest dto)
    {
        var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminIdStr) || !Guid.TryParse(adminIdStr, out var parsedAdminId))
            throw new UnauthorizedException("User id is required");
        var rs = await _userService.UnbanUser(parsedAdminId,userId);
        return Ok(rs);
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResult<UserResponse>>> GetAllUsers([FromQuery] string? query, [FromQuery] int page =1 ,[FromQuery] int size = 10)
    {
        if (page <= 0 || size <= 0)
            return BadRequest("Page and size must be greater than 0");
        var rs = await _userService.GetAllUsers(page, size, query);
        return Ok(rs);
    }
}