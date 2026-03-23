using Eventiq.UserService.Domain.Entity;
using Eventiq.UserService.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.UserService.Infrastructure.Persistence;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly EvtUserDbContext _context;
    private readonly DbSet<PasswordResetToken> _tokens;

    public PasswordResetTokenRepository(EvtUserDbContext context)
    {
        _context = context;
        _tokens = context.Set<PasswordResetToken>();
    }

    public async Task AddToken(PasswordResetToken token)
    {
        _tokens.Add(token);
        await _context.SaveChangesAsync();
    }

    public async Task<PasswordResetToken?> GetByToken(string token)
    {
        return await _tokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token);
    }

    public async Task RemoveToken(PasswordResetToken token)
    {
        _tokens.Remove(token);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveTokensByUserId(Guid userId)
    {
        var tokens = await _tokens.Where(t => t.UserId == userId).ToListAsync();
        _tokens.RemoveRange(tokens);
        await _context.SaveChangesAsync();
    }
}
