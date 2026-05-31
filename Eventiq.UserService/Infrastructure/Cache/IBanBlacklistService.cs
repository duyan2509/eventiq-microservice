namespace Eventiq.UserService.Infrastructure.Cache;

public interface IBanBlacklistService
{
    Task BanAsync(Guid userId);
    Task UnbanAsync(Guid userId);
    Task<bool> IsBannedAsync(Guid userId);
}
