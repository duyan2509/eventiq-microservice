using System.Security.Cryptography;
using Eventiq.UserService.Domain.Entity;
using Eventiq.UserService.Domain.Repositories;
using Eventiq.UserService.Model;

namespace Eventiq.UserService.Application.Service;

public class RefreshTokenService:IRefreshTokenService
{
    private IRefreshTokenRepository _refreshTokenRepository;

    public RefreshTokenService(IRefreshTokenRepository refreshTokenRepository)
    {
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task<string> GenerateRefreshToken(string userId)
    {
        var token = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(64)
        );
        var refresToken = new RefreshToken
        {
            UserId = Guid.Parse(userId),
            Token = token,
            Expires = DateTime.UtcNow.AddDays(5),
        };
        await _refreshTokenRepository.AddRefreshToken(refresToken);
        return token;
    }

    public bool ValidateRefreshToken(RefreshTokenModel? refreshToken)
    {
        if (refreshToken == null)
            throw new NotFoundException("Refresh token not found");
        if(DateTime.UtcNow > refreshToken.Expires)
            return false;
        return true;
    }

    public async Task<RefreshTokenModel?> GetRefreshTokenModel(string refreshToken)
    {
        return await _refreshTokenRepository.GetRefreshToken(refreshToken);
    }

    public async Task RevokeRefreshToken(string refreshToken)
    {
         await _refreshTokenRepository.RemoveRefreshToken(refreshToken);
    }
}