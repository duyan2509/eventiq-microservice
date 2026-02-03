using System.Security.Claims;
using Eventiq.UserService.Application.Dto;
using Eventiq.UserService.Application.Service;
using Eventiq.UserService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.UserService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly IUserService _userService;

    public AuthController(ILogger<AuthController> logger, IUserService userService)
    {
        _logger = logger;
        _userService = userService;
    }
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe()
    {

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var parsedUserId))
            throw new UnauthorizedException("User id is required");

        var user = await _userService.GetMe(parsedUserId, role);
        return Ok(user);
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginDto dto)
    {
        var rs = await _userService.Login(dto);
        return Ok(rs);
    }
    [Authorize]
    [HttpPost("role")]
    public async Task<ActionResult<SwitchRoleRepsponse>> Login([FromBody] AppRoles role)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var parsedUserId))
            throw new UnauthorizedException("User id is required");
        var rs = await _userService.SwitchRole(parsedUserId, role );
        return Ok(rs);
    }

    [HttpPost("register")]
    public async Task<ActionResult<bool>> Register([FromBody] RegisterDto dto)
    {
        var rs = await _userService.Register(dto);
        return Ok(rs);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshResponse>> Refresh([FromBody] RefreshRequest dto)
    {
        var rs = await _userService.Refresh(dto.RefreshToken);
        return Ok(rs);
    }

    [HttpPost("logout")]
    public async Task<ActionResult> Logout([FromBody] RefreshRequest dto)
    {
        _userService.Logout(dto.RefreshToken);
        return Ok();
    }

    [HttpPatch("change-password")]
    public async Task<ActionResult<UserDto>> ChangePassword([FromBody] ChangePasswordRequest dto)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var parsedUserId))
            throw new UnauthorizedException("User id is required");
        var rs = await _userService.ChangePassword(parsedUserId,dto);
        return Ok(rs);
    }
    
}


