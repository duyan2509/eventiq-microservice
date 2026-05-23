using System.Security.Claims;
using Eventiq.SeatService.Application.Dtos;
using Eventiq.SeatService.Application.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Eventiq.SeatService.Hubs;

[Authorize]
public class SeatDesignHub : Hub
{
    private readonly ISeatDesignService _designService;
    private readonly IPresenceService _presenceService;
    private readonly ILogger<SeatDesignHub> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<SeatDesignHub> _hubContext;

    private static readonly Dictionary<Guid, CancellationTokenSource> _autoSaveTimers = new();
    private static readonly object _timerLock = new();
    private const int AutoSaveDelayMs = 2000;

    private static readonly Dictionary<string, Guid> _connectionOrgId = new();
    private static readonly object _orgIdLock = new();

    private static readonly Dictionary<Guid, Guid> _seatMapOrgCache = new();
    private static readonly object _orgCacheLock = new();

    public SeatDesignHub(
        ISeatDesignService designService,
        IPresenceService presenceService,
        ILogger<SeatDesignHub> logger,
        IServiceScopeFactory scopeFactory,
        IHubContext<SeatDesignHub> hubContext)
    {
        _designService = designService;
        _presenceService = presenceService;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
    }

    // ========== Connection Lifecycle ==========

    public async Task JoinSeatMap(Guid seatMapId)
    {
        var user = GetCurrentUser();
        var groupName = GetGroupName(seatMapId);

        Guid orgId;
        lock (_orgCacheLock) { _seatMapOrgCache.TryGetValue(seatMapId, out orgId); }
        if (orgId == Guid.Empty)
        {
            orgId = await _designService.GetSeatMapOrgIdAsync(seatMapId);
            lock (_orgCacheLock) { _seatMapOrgCache[seatMapId] = orgId; }
        }
        lock (_orgIdLock) { _connectionOrgId[Context.ConnectionId] = orgId; }

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await _presenceService.AddUserAsync(seatMapId, user);

        var onlineUsers = await _presenceService.GetOnlineUsersAsync(seatMapId);
        var selections = await _presenceService.GetSelectionsAsync(seatMapId);

        await Clients.Caller.SendAsync("CurrentPresence", new { OnlineUsers = onlineUsers, Selections = selections });
        await Clients.OthersInGroup(groupName).SendAsync("UserJoined", user);
    }

    public async Task LeaveSeatMap(Guid seatMapId)
    {
        var userId = GetUserId();
        var groupName = GetGroupName(seatMapId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        await _presenceService.RemoveUserAsync(seatMapId, userId);
        await Clients.OthersInGroup(groupName).SendAsync("UserLeft", userId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        lock (_orgIdLock) { _connectionOrgId.Remove(Context.ConnectionId); }
        await base.OnDisconnectedAsync(exception);
    }

    // ========== Seat Operations ==========

    public async Task AddSeat(Guid seatMapId, AddSeatDto dto)
    {
        var orgId = GetOrgId();
        // Ensure dto targets the correct map (client sends seatMapId in dto too)
        dto.SeatMapId = seatMapId;
        var result = await _designService.AddSeatAsync(seatMapId, orgId, dto);

        await Clients.Group(GetGroupName(seatMapId)).SendAsync("SeatAdded", result);
        await TriggerAutoSave(seatMapId);
    }

    public async Task UpdateSeats(Guid seatMapId, BatchUpdateSeatsDto dto)
    {
        var orgId = GetOrgId();
        var result = await _designService.BatchUpdateSeatsAsync(seatMapId, orgId, dto);

        await Clients.Group(GetGroupName(seatMapId)).SendAsync("SeatsUpdated", result);
        await TriggerAutoSave(seatMapId);
    }

    public async Task DeleteSeats(Guid seatMapId, List<Guid> seatIds)
    {
        var orgId = GetOrgId();
        await _designService.DeleteSeatsAsync(seatMapId, orgId, seatIds);

        await Clients.Group(GetGroupName(seatMapId)).SendAsync("SeatsDeleted", seatIds);
        await TriggerAutoSave(seatMapId);
    }

    public async Task SetSeatLegend(Guid seatMapId, List<Guid> seatIds, Guid? legendId)
    {
        var orgId = GetOrgId();
        var result = await _designService.SetSeatLegendAsync(seatMapId, orgId, seatIds, legendId);

        await Clients.Group(GetGroupName(seatMapId)).SendAsync("SeatsUpdated", result);
        await TriggerAutoSave(seatMapId);
    }

    // ========== Object Operations ==========

    public async Task AddObject(Guid seatMapId, AddObjectDto dto)
    {
        var orgId = GetOrgId();
        var result = await _designService.AddObjectAsync(seatMapId, orgId, dto);

        await Clients.Group(GetGroupName(seatMapId)).SendAsync("ObjectAdded", result);
        await TriggerAutoSave(seatMapId);
    }

    public async Task UpdateObject(Guid seatMapId, UpdateObjectDto dto)
    {
        var orgId = GetOrgId();
        var result = await _designService.UpdateObjectAsync(seatMapId, orgId, dto);

        await Clients.Group(GetGroupName(seatMapId)).SendAsync("ObjectUpdated", result);
        await TriggerAutoSave(seatMapId);
    }

    public async Task DeleteObject(Guid seatMapId, Guid objectId)
    {
        var orgId = GetOrgId();
        await _designService.DeleteObjectAsync(seatMapId, orgId, objectId);

        await Clients.Group(GetGroupName(seatMapId)).SendAsync("ObjectDeleted", objectId);
        await TriggerAutoSave(seatMapId);
    }

    // ========== Cursor & Presence ==========

    public async Task SendCursorPosition(Guid seatMapId, CursorDto cursor)
    {
        var userId = GetUserId();
        await Clients.OthersInGroup(GetGroupName(seatMapId)).SendAsync("CursorMoved", new { UserId = userId, cursor.X, cursor.Y });
    }

    public async Task SendSelection(Guid seatMapId, SelectionDto selection)
    {
        var userId = GetUserId();
        await _presenceService.UpdateSelectionAsync(seatMapId, userId, selection.ElementIds);
        await Clients.OthersInGroup(GetGroupName(seatMapId)).SendAsync("SelectionChanged", new { UserId = userId, selection.ElementIds });
    }

    // ========== Auto-save ==========

    private async Task TriggerAutoSave(Guid seatMapId)
    {
        var userId = GetUserId();
        lock (_timerLock)
        {
            if (_autoSaveTimers.TryGetValue(seatMapId, out var existingCts))
            {
                existingCts.Cancel();
                existingCts.Dispose();
            }

            var cts = new CancellationTokenSource();
            _autoSaveTimers[seatMapId] = cts;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(AutoSaveDelayMs, cts.Token);
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var svc = scope.ServiceProvider.GetRequiredService<ISeatDesignService>();
                    var versionResponse = await svc.AutoSaveSnapshotAsync(seatMapId, userId);

                    await _hubContext.Clients.Group(GetGroupName(seatMapId)).SendAsync("AutoSaved", new
                    {
                        versionResponse.VersionNumber,
                        versionResponse.CreatedAt,
                        versionResponse.ChangeDescription
                    });
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { _logger.LogError(ex, "Auto-save failed for {SeatMapId}", seatMapId); }
                finally
                {
                    lock (_timerLock)
                    {
                        if (_autoSaveTimers.TryGetValue(seatMapId, out var current) && current == cts)
                            _autoSaveTimers.Remove(seatMapId);
                    }
                }
            });
        }
    }

    private Guid GetUserId()
    {
        var sub = Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var userId) ? userId : throw new UnauthorizedException("Invalid user.");
    }

    private Guid GetOrgId()
    {
        lock (_orgIdLock)
        {
            if (_connectionOrgId.TryGetValue(Context.ConnectionId, out var orgId)) return orgId;
        }
        throw new UnauthorizedException("Join the seat map before performing operations.");
    }

    private UserPresenceDto GetCurrentUser()
    {
        var userId = GetUserId();
        var email = Context.User?.FindFirst("email")?.Value ?? "";
        var name = Context.User?.FindFirst("name")?.Value ?? Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? email;
        var hash = userId.GetHashCode();
        var colors = new[] { "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7", "#DDA0DD", "#98D8C8", "#F7DC6F" };
        return new UserPresenceDto { UserId = userId, Email = email, DisplayName = name, AvatarColor = colors[Math.Abs(hash) % colors.Length] };
    }

    private static string GetGroupName(Guid seatMapId) => $"seatmap-{seatMapId}";
}
