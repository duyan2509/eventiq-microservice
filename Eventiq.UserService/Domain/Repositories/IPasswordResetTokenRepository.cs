using Eventiq.UserService.Domain.Entity;

namespace Eventiq.UserService.Domain.Repositories;

public interface IPasswordResetTokenRepository
{
    Task AddToken(PasswordResetToken token);
    Task<PasswordResetToken?> GetByToken(string token);
    Task RemoveToken(PasswordResetToken token);
    Task RemoveTokensByUserId(Guid userId);
}
