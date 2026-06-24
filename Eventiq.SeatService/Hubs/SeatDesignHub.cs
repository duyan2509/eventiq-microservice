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

    // Tracks which seat map + user each live connection belongs to, so a dropped
    // connection (tab close, network loss) can clean up presence in OnDisconnectedAsync
    // even when the client never reaches LeaveSeatMap. Ref-counted per user so a user
    // with multiple tabs open only disappears once the LAST connection goes away.
    private static readonly Dictionary<string, (Guid SeatMapId, Guid UserId)> _connectionInfo = new();
    private static readonly Dictionary<(Guid SeatMapId, Guid UserId), HashSet<string>> _userConnections = new();
    private static readonly object _connLock = new();

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
        RegisterConnection(seatMapId, user.UserId, Context.ConnectionId);

        var onlineUsers = await _presenceService.GetOnlineUsersAsync(seatMapId);
        var selections = await _presenceService.GetSelectionsAsync(seatMapId);

        await Clients.Caller.SendAsync("CurrentPresence", new { OnlineUsers = onlineUsers, Selections = selections });
        await Clients.OthersInGroup(groupName).SendAsync("UserJoined", user);
    }

    public async Task LeaveSeatMap(Guid seatMapId)
    {
        var groupName = GetGroupName(seatMapId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        // Only clear presence and notify peers when this was the user's last open tab.
        if (UnregisterConnection(Context.ConnectionId) is (var smId, var uId, true))
        {
            await _presenceService.RemoveUserAsync(smId, uId);
            await Clients.OthersInGroup(GetGroupName(smId)).SendAsync("UserLeft", uId);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        lock (_orgIdLock) { _connectionOrgId.Remove(Context.ConnectionId); }

        // A dropped connection (closed tab, lost network) never calls LeaveSeatMap, so
        // do the same cleanup here — otherwise the user's cursor and presence badge stay
        // visible to everyone else forever. SignalR has already pulled this connection
        // out of its groups, so Clients.Group reaches only the remaining members.
        if (UnregisterConnection(Context.ConnectionId) is (var smId, var uId, true))
        {
            await _presenceService.RemoveUserAsync(smId, uId);
            await Clients.Group(GetGroupName(smId)).SendAsync("UserLeft", uId);
        }

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

    public async Task AddSeats(Guid seatMapId, AddSeatsBatchDto dto)
    {
        var orgId = GetOrgId();
        dto.SeatMapId = seatMapId;
        var result = await _designService.AddSeatsAsync(seatMapId, orgId, dto);

        await Clients.Group(GetGroupName(seatMapId)).SendAsync("SeatsAdded", result);
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

    public async Task SendCursorLeave(Guid seatMapId)
    {
        var userId = GetUserId();
        await Clients.OthersInGroup(GetGroupName(seatMapId)).SendAsync("CursorLeft", userId);
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

    private static void RegisterConnection(Guid seatMapId, Guid userId, string connectionId)
    {
        lock (_connLock)
        {
            _connectionInfo[connectionId] = (seatMapId, userId);
            var key = (seatMapId, userId);
            if (!_userConnections.TryGetValue(key, out var set))
            {
                set = new HashSet<string>();
                _userConnections[key] = set;
            }
            set.Add(connectionId);
        }
    }

    // Returns the seat map / user the connection belonged to, and whether it was the
    // user's last connection on that map. Null if the connection was never tracked
    // (e.g. disconnected before JoinSeatMap, or already unregistered by LeaveSeatMap).
    private static (Guid SeatMapId, Guid UserId, bool IsLast)? UnregisterConnection(string connectionId)
    {
        lock (_connLock)
        {
            if (!_connectionInfo.Remove(connectionId, out var info))
                return null;

            var key = (info.SeatMapId, info.UserId);
            var isLast = true;
            if (_userConnections.TryGetValue(key, out var set))
            {
                set.Remove(connectionId);
                if (set.Count == 0) _userConnections.Remove(key);
                else isLast = false;
            }
            return (info.SeatMapId, info.UserId, isLast);
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
