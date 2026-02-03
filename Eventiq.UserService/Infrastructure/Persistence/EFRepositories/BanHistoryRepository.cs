using Eventiq.UserService.Application.Dto;
using Eventiq.UserService.Domain.Entity;
using Eventiq.UserService.Domain.Repositories;
using Eventiq.UserService.Model;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.UserService.Infrastructure.Persistence;

public class BanHistoryRepository:IBanHistoryRepository
{
    private readonly DbContext _context;
    private readonly ILogger<BanHistoryRepository> _logger;
    private readonly DbSet<BanHistory> _banHistory;

    public BanHistoryRepository(EvtUserDbContext context, ILogger<BanHistoryRepository> logger)
    {
        _context = context;
        _logger = logger;
        _banHistory = context.Set<BanHistory>();
    }
    public async Task AddBanHistory(BanHistory banHistory)
    {
        await _banHistory.AddAsync(banHistory);
        await _context.SaveChangesAsync();
    }

    public async Task<PaginatedResult<BanHistoryModel>> GetBanHistoryByAdminId(string adminId, int page, int size)
    {
        var query =  _banHistory.AsNoTracking()
            .Where(history => history.BannedById == Guid.Parse(adminId))
            .OrderDescending();
        int total = await query.CountAsync();
        var data = new List<BanHistoryModel>();
        if ((page-1) * size < total)
            data = await query
                .Skip((page - 1) * size)
                .Take(size)
                .Select(history=> new BanHistoryModel
                {
                    AdminEmail = history.BannedByUser.Email,
                    AdminId = history.BannedByUser.Id,
                    UserId = history.UserId,
                    UserEmail = history.User.Email,
                    Reason = history.Reason ?? string.Empty,
                    Date = history.CreatedAt,
                }).ToListAsync();
        return new PaginatedResult<BanHistoryModel>
        {
            Data = data,
            Total = total,
            Page = page,
            Size = size
        };
    }

    public async Task<PaginatedResult<BanHistoryModel>> GetBanHistoryByUserId(string userId, int page, int size)
    {
        var query =  _banHistory.AsNoTracking()
            .Where(history => history.UserId == Guid.Parse(userId))
            .OrderDescending();
        int total = await query.CountAsync();
        var data = new List<BanHistoryModel>();
        if ((page-1) * size < total)
            data = await query
                .Skip((page - 1) * size)
                .Take(size)
                .Select(history=> new BanHistoryModel
                {
                    AdminEmail = history.BannedByUser.Email,
                    AdminId = history.BannedByUser.Id,
                    UserId = history.UserId,
                    UserEmail = history.User.Email,
                    Reason = history.Reason ?? string.Empty,
                    Date = history.CreatedAt,
                }).ToListAsync();
        return new PaginatedResult<BanHistoryModel>
        {
            Data = data,
            Total = total,
            Page = page,
            Size = size
        };
    }
}