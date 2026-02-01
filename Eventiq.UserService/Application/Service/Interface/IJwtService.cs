using System.Security.Claims;

namespace Eventiq.UserService.Application.Service;

public interface IJwtService
{
    string GenerateAccessToken(
        string userId,
        string role,
        IDictionary<string, string>? extraClaims = null
    );
}