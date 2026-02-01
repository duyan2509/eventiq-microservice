using Eventiq.UserService.Application.Dto;

using Microsoft.IdentityModel.Tokens;

namespace Eventiq.UserService.Application.Service;

public class UserService:IUserService
{
    private readonly IJwtService _jwt;
    private readonly IRefreshTokenService _refresh;

    public UserService(
        IJwtService jwt,
        IRefreshTokenService refresh)
    {
        _jwt = jwt;
        _refresh = refresh;
    }
    public async Task<LoginResponse> Login(LoginDto dto)
    {
        var userId = "123";
        var email = "admin@eventiq.com";
        var role = "admin";
        var accessToken = _jwt.GenerateAccessToken(
            userId, role, new Dictionary<string, string>
            {
                ["email"]=email
            }
        );

        var refreshToken = _refresh.GenerateRefreshToken(userId);

        return new LoginResponse(
            accessToken,
            refreshToken
        );
    }



    public async Task<RefreshResponse> Refresh(string refreshToken)
    {
        var email = "admin@eventiq.com";
        var role = "admin";
        if (!_refresh.ValidateRefreshToken(
                refreshToken,
                out var userId))
        {
            throw new SecurityTokenException(
                "Invalid refresh token"
            );
        }

        var accessToken = _jwt.GenerateAccessToken(
            userId, role, new Dictionary<string, string>
            {
                ["email"]=email
            }
        );
        return new RefreshResponse(
            accessToken,
            refreshToken
        );
    }

    public void Logout(string refreshToken)
    {
        _refresh.RevokeRefreshToken(refreshToken);
    }
}