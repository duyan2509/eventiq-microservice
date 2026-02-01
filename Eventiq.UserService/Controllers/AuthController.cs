using Eventiq.UserService.Application.Dto;
using Eventiq.UserService.Application.Service;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Eventiq.UserService.Controllers;

[ApiController][Route("api/auth")]
public class AuthController : ControllerBase
{
    
    private readonly ILogger<AuthController> _logger;
    private readonly IUserService _userService;

    public AuthController(ILogger<AuthController> logger,  IUserService userService)
    {
        _logger = logger;
        _userService = userService;
    }
    [HttpGet("me")]
    public ActionResult<UserResponse> GetMe()
    {
        try
        {
            var userId = GetUserId();
            return Ok(userId); 
            if (userId == null)
                return Unauthorized();
            return Ok(new UserResponse
            {
                Email = "tmp@gmail.com",
                UserName = "temp user name"
            });
        }
        catch (KeyNotFoundException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginDto dto)
    {
        try
        {
            var rs = await _userService.Login(dto);
            return Ok(rs);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
    [HttpPost("register")]
    public ActionResult<bool> Register([FromBody] RegisterDto dto)
    {
        try
        {
            return Ok(true);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshResponse>> Refresh([FromBody] RefreshRequest dto)
    {
        try
        {
            var rs = await _userService.Refresh(dto.RefreshToken);
            return Ok(rs);
        }
        catch (SecurityTokenException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
    [HttpPost("logout")]
    public ActionResult Logout([FromBody] RefreshRequest dto)
    {
        try
        {
            _userService.Logout(dto.RefreshToken);
            return Ok();
        }
        catch (SecurityTokenException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
    public string? GetUserId()
    {
        return Request.Headers["X-User-Id"].ToString();
    }
}


