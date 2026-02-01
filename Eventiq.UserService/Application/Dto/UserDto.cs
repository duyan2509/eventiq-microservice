namespace Eventiq.UserService.Application.Dto;


public class RegisterDto
{
    public string Email { get; set; }
    public string Password { get; set; }
}

public class UserResponse
{
    public string Email { get; set; }
    public string? UserName { get; set; }
}

public class LoginDto
{
    public string Email { get; set; }
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
public class RefreshResponse:LoginResponse
{
    public RefreshResponse(string accessToken, string refreshToken) : base(accessToken, refreshToken)
    {
    }
}
