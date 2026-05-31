using StackExchange.Redis;

namespace Eventiq.UserService.Infrastructure.Cache;

public class RedisBanBlacklistService : IBanBlacklistService
{
    private readonly IDatabase _db;
    private const string KeyPrefix = "ban:user:";

    public RedisBanBlacklistService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public Task BanAsync(Guid userId)
        => _db.StringSetAsync($"{KeyPrefix}{userId}", "1");

    public Task UnbanAsync(Guid userId)
        => _db.KeyDeleteAsync($"{KeyPrefix}{userId}");

    public async Task<bool> IsBannedAsync(Guid userId)
        => await _db.KeyExistsAsync($"{KeyPrefix}{userId}");
}
