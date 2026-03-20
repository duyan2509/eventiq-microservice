using System.Text.Json;
using Eventiq.SeatService.Application.Dtos;
using Eventiq.SeatService.Application.Service.Interface;
using StackExchange.Redis;

namespace Eventiq.SeatService.Infrastructure.Redis;

public class RedisPresenceService : IPresenceService
{
    private readonly IConnectionMultiplexer _redis;
    private const string PresencePrefix = "seat:presence:";
    private const string SelectionPrefix = "seat:selection:";

    public RedisPresenceService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task AddUserAsync(Guid seatMapId, UserPresenceDto user)
    {
        var db = _redis.GetDatabase();
        var key = $"{PresencePrefix}{seatMapId}";
        var userJson = JsonSerializer.Serialize(user);
        await db.HashSetAsync(key, user.UserId.ToString(), userJson);
        await db.KeyExpireAsync(key, TimeSpan.FromHours(24));
    }

    public async Task RemoveUserAsync(Guid seatMapId, Guid userId)
    {
        var db = _redis.GetDatabase();
        await db.HashDeleteAsync($"{PresencePrefix}{seatMapId}", userId.ToString());
        await db.HashDeleteAsync($"{SelectionPrefix}{seatMapId}", userId.ToString());
    }

    public async Task<List<UserPresenceDto>> GetOnlineUsersAsync(Guid seatMapId)
    {
        var db = _redis.GetDatabase();
        var entries = await db.HashGetAllAsync($"{PresencePrefix}{seatMapId}");

        return entries
            .Where(e => e.Value.HasValue)
            .Select(e => JsonSerializer.Deserialize<UserPresenceDto>(e.Value!))
            .Where(u => u != null)
            .Select(u => u!)
            .ToList();
    }

    public async Task UpdateSelectionAsync(Guid seatMapId, Guid userId, List<Guid> elementIds)
    {
        var db = _redis.GetDatabase();
        var key = $"{SelectionPrefix}{seatMapId}";
        var value = JsonSerializer.Serialize(elementIds);
        await db.HashSetAsync(key, userId.ToString(), value);
        await db.KeyExpireAsync(key, TimeSpan.FromHours(24));
    }

    public async Task<Dictionary<Guid, List<Guid>>> GetSelectionsAsync(Guid seatMapId)
    {
        var db = _redis.GetDatabase();
        var entries = await db.HashGetAllAsync($"{SelectionPrefix}{seatMapId}");

        var result = new Dictionary<Guid, List<Guid>>();
        foreach (var entry in entries)
        {
            if (Guid.TryParse(entry.Name, out var userId) && entry.Value.HasValue)
            {
                var ids = JsonSerializer.Deserialize<List<Guid>>(entry.Value!);
                if (ids != null)
                    result[userId] = ids;
            }
        }
        return result;
    }

    public async Task ClearSelectionAsync(Guid seatMapId, Guid userId)
    {
        var db = _redis.GetDatabase();
        await db.HashDeleteAsync($"{SelectionPrefix}{seatMapId}", userId.ToString());
    }
}
