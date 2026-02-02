using Eventiq.UserService.Domain.Entity;
using Eventiq.UserService.Domain.Repositories;
using Eventiq.UserService.Model;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.UserService.Infrastructure.Persistence;

public class RefreshRepository:IRefreshTokenRepository
{
    private DbSet<RefreshToken> _refreshTokens;
    private readonly ILogger<RefreshRepository> _logger;
    private EvtUserDbContext _context;

    public RefreshRepository(EvtUserDbContext context, ILogger<RefreshRepository> logger)
    {
        _context = context;
        _logger = logger;
        _refreshTokens = _context.Set<RefreshToken>();
    }
    public async Task<RefreshTokenModel> AddRefreshToken(RefreshToken refreshToken)
    {
        var token = await _refreshTokens.AddAsync(refreshToken);
        await _context.SaveChangesAsync();
        return new RefreshTokenModel
        {
            Expires = token.Entity.Expires,
            Token = token.Entity.Token,
            UserId = token.Entity.UserId
        };
    }

    public async  void RemoveRefreshToken(string refreshToken)
    {
        var token = await _refreshTokens.SingleOrDefaultAsync(t => t.Token == refreshToken);
        if (token == null) return;
        _refreshTokens.Remove(token);
        await _context.SaveChangesAsync();
    }

    public async Task<RefreshTokenModel?> GetRefreshToken(string refreshToken)
    {
        return await _refreshTokens
            .Where(t => t.Token == refreshToken)
            .Select(token => new RefreshTokenModel
            {
                Expires = token.Expires,
                Token = token.Token,
                UserId = token.UserId
            })
            .SingleOrDefaultAsync();;
    }
}