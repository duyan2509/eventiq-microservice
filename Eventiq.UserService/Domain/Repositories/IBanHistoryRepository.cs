using Eventiq.UserService.Application.Dto;
using Eventiq.UserService.Domain.Entity;
using Eventiq.UserService.Model;

namespace Eventiq.UserService.Domain.Repositories;

public interface IBanHistoryRepository
{
    Task AddBanHistory(BanHistory banHistory);
    Task<PaginatedResult<BanHistoryModel>> GetBanHistoryByAdminId(string adminId, int page, int size);
    Task<PaginatedResult<BanHistoryModel>> GetBanHistoryByUserId(string userId, int page, int size);
}

