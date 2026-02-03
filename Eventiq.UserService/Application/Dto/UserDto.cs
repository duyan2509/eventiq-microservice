using System.ComponentModel.DataAnnotations;
using Eventiq.UserService.Domain.Enums;

namespace Eventiq.UserService.Application.Dto;
public class UserDto
{
    public string Id { get; set; }
    public string Email { get; set; } = string.Empty;
    
    public string? CurrentRole { get; set; } 
    public ICollection<string>? Roles { get; set; } = new List<string>();
    public bool IsBanned { get; set; } = false;
}

public class RegisterDto
{
    [Required]
    public string Email { get; set; }
    [Required]
    public string Password { get; set; }
}


public class UserResponse
{
    public string Id { get; set; }
    public string Email  { get; set; }
    public bool IsBanned { get; set; }
    public IReadOnlyList<string> Roles { get; init; } = [];
}
public class LoginDto
{
    
    [Required]
    public string Email { get; set; }
    
    [Required]
    public string Password { get; set; }
}

public class LoginResponse
{
    public LoginResponse(string accessToken, string refreshToken)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
    }

    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}
public class SwitchRoleRepsponse:LoginResponse
{
    public SwitchRoleRepsponse(string accessToken, string refreshToken) : base(accessToken, refreshToken)
    {
    }
}
public class RefreshResponse:LoginResponse
{
    public RefreshResponse(string accessToken, string refreshToken) : base(accessToken, refreshToken)
    {
    }
}

public class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
public class CreateUserDto
{

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(15, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;


}
public class BanUserRequest
{
    public string? BanReason { get; set; }
}
public class ChangePasswordRequest
{
    [Required]
    [StringLength(15, MinimumLength = 6)]
    public string CurrentPassword { get; set; } = string.Empty;
    [Required]
    [StringLength(15, MinimumLength = 6)]
    public string NewPassword { get; set; } = string.Empty;
}