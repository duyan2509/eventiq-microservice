using System.Security.Claims;
using Eventiq.UserService.Application.Dto;
using Eventiq.UserService.Application.Service;
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

    private void SetRefreshTokenCookie(string refreshToken)
    {
        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = false, // set true in production (HTTPS)
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.FromDays(7)
        });
    }

    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = User.FindFirstValue(ClaimTypes.Role);
        var orgId = User.FindFirstValue("orgId");
        var orgName = User.FindFirstValue("orgName");

        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var parsedUserId))
            throw new UnauthorizedException("User id is required");

        var user = await _userService.GetMe(parsedUserId, role ?? "User", orgId, orgName);
        return Ok(user);
    }

    [HttpPost("login")]
    public async Task<ActionResult> Login([FromBody] LoginDto dto)
    {
        var rs = await _userService.Login(dto);
        SetRefreshTokenCookie(rs.RefreshToken);
        return Ok(new { accessToken = rs.AccessToken });
    }

    [Authorize]
    [HttpPost("role")]
    public async Task<ActionResult> SwitchRole([FromBody] SwitchRoleRequest dto)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var parsedUserId))
            throw new UnauthorizedException("User id is required");
        var rs = await _userService.SwitchRole(parsedUserId, dto.OrganizationId, dto.OrganizationName);
        SetRefreshTokenCookie(rs.RefreshToken);
        return Ok(new { accessToken = rs.AccessToken });
    }

    [HttpPost("register")]
    public async Task<ActionResult<bool>> Register([FromBody] RegisterDto dto)
    {
        var rs = await _userService.Register(dto);
        return Ok(rs);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult> Refresh()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new { message = "No refresh token provided" });

        var rs = await _userService.Refresh(refreshToken);
        SetRefreshTokenCookie(rs.RefreshToken);
        return Ok(new { accessToken = rs.AccessToken });
    }

    [HttpPost("logout")]
    public async Task<ActionResult> Logout()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (!string.IsNullOrEmpty(refreshToken))
            await _userService.Logout(refreshToken);
        ClearRefreshTokenCookie();
        return Ok();
    }

    [HttpPatch("change-password")]
    public async Task<ActionResult<UserDto>> ChangePassword([FromBody] ChangePasswordRequest dto)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var parsedUserId))
            throw new UnauthorizedException("User id is required");
        var rs = await _userService.ChangePassword(parsedUserId, dto);
        return Ok(rs);
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest dto)
    {
        await _userService.ForgotPassword(dto.Email);
        return Ok(new { message = "If the email exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<bool>> ResetPassword([FromBody] ResetPasswordRequest dto)
    {
        var rs = await _userService.ResetPassword(dto.Token, dto.NewPassword);
        return Ok(rs);
    }

    [Authorize]
    [HttpPost("switch-to-user")]
    public async Task<ActionResult> SwitchToUser()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var parsedUserId))
            throw new UnauthorizedException("User id is required");

        var rs = await _userService.SwitchToUser(parsedUserId);
        SetRefreshTokenCookie(rs.RefreshToken);
        return Ok(new { accessToken = rs.AccessToken });
    }
}
