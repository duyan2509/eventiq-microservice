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

    // Auto-save debounce tracking: seatMapId → CancellationTokenSource
    private static readonly Dictionary<Guid, CancellationTokenSource> _autoSaveTimers = new();
    private static readonly object _timerLock = new();
    private const int AutoSaveDelayMs = 2000; // 2 seconds debounce

    public SeatDesignHub(
        ISeatDesignService designService,
        IPresenceService presenceService,
        ILogger<SeatDesignHub> logger)
    {
        _designService = designService;
        _presenceService = presenceService;
        _logger = logger;
    }

    // ========== Connection Lifecycle ==========

    public async Task JoinSeatMap(Guid seatMapId)
    {
        var user = GetCurrentUser();
        var groupName = GetGroupName(seatMapId);

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await _presenceService.AddUserAsync(seatMapId, user);

        // Send current online users to the joining user
        var onlineUsers = await _presenceService.GetOnlineUsersAsync(seatMapId);
        var selections = await _presenceService.GetSelectionsAsync(seatMapId);

        await Clients.Caller.SendAsync("CurrentPresence", new
        {
            OnlineUsers = onlineUsers,
            Selections = selections
        });

        // Notify others that a new user joined
        await Clients.OthersInGroup(groupName).SendAsync("UserJoined", user);

        _logger.LogInformation("User {UserId} joined seat map {SeatMapId}", user.UserId, seatMapId);
    }

    public async Task LeaveSeatMap(Guid seatMapId)
    {
        var userId = GetUserId();
        var groupName = GetGroupName(seatMapId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        await _presenceService.RemoveUserAsync(seatMapId, userId);

        await Clients.OthersInGroup(groupName).SendAsync("UserLeft", userId);

        _logger.LogInformation("User {UserId} left seat map {SeatMapId}", userId, seatMapId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Connection {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // ========== Section Operations ==========

    public async Task AddSection(Guid seatMapId, AddSectionDto dto)
    {
        var orgId = GetOrgId();
        var result = await _designService.AddSectionAsync(seatMapId, orgId, dto);

        await Clients.Group(GetGroupName(seatMapId)).SendAsync("SectionAdded", result);
        await TriggerAutoSave(seatMapId);
    }

    public async Task UpdateSection(Guid seatMapId, UpdateSectionDto dto)
    {
        var orgId = GetOrgId();
        var result = await _designService.UpdateSectionAsync(seatMapId, orgId, dto);

        await Clients.Group(GetGroupName(seatMapId)).SendAsync("SectionUpdated", result);
        await TriggerAutoSave(seatMapId);
    }

    public async Task DeleteSection(Guid seatMapId, Guid sectionId)
    {
        var orgId = GetOrgId();
        await _designService.DeleteSectionAsync(seatMapId, orgId, sectionId);

        await Clients.Group(GetGroupName(seatMapId)).SendAsync("SectionDeleted", sectionId);
        await TriggerAutoSave(seatMapId);
    }

    // ========== Row Operations ==========

    public async Task AddRow(Guid seatMapId, AddRowDto dto)
    {
        var orgId = GetOrgId();
        var result = await _designService.AddRowAsync(seatMapId, orgId, dto);

        await Clients.Group(GetGroupName(seatMapId)).SendAsync("RowAdded", result);
        await TriggerAutoSave(seatMapId);
    }

    public async Task UpdateRow(Guid seatMapId, UpdateRowDto dto)
    {
        var orgId = GetOrgId();
        var result = await _designService.UpdateRowAsync(seatMapId, orgId, dto);

        await Clients.Group(GetGroupName(seatMapId)).SendAsync("RowUpdated", result);
        await TriggerAutoSave(seatMapId);
    }

    public async Task DeleteRow(Guid seatMapId, Guid rowId)
    {
        var orgId = GetOrgId();
        await _designService.DeleteRowAsync(seatMapId, orgId, rowId);

        await Clients.Group(GetGroupName(seatMapId)).SendAsync("RowDeleted", rowId);
        await TriggerAutoSave(seatMapId);
    }

    // ========== Seat Operations ==========

    public async Task AddSeat(Guid seatMapId, AddSeatDto dto)
    {
        var orgId = GetOrgId();
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

    // ========== Cursor & Presence (Fire & Forget) ==========

    public async Task SendCursorPosition(Guid seatMapId, CursorDto cursor)
    {
        var userId = GetUserId();
        await Clients.OthersInGroup(GetGroupName(seatMapId)).SendAsync("CursorMoved", new
        {
            UserId = userId,
            cursor.X,
            cursor.Y
        });
    }

    public async Task SendSelection(Guid seatMapId, SelectionDto selection)
    {
        var userId = GetUserId();
        await _presenceService.UpdateSelectionAsync(seatMapId, userId, selection.ElementIds);

        await Clients.OthersInGroup(GetGroupName(seatMapId)).SendAsync("SelectionChanged", new
        {
            UserId = userId,
            selection.ElementIds
        });
    }

    // ========== Auto-save with Debounce ==========

    private async Task TriggerAutoSave(Guid seatMapId)
    {
        var userId = GetUserId();

        lock (_timerLock)
        {
            // Cancel existing timer if any (debounce)
            if (_autoSaveTimers.TryGetValue(seatMapId, out var existingCts))
            {
                existingCts.Cancel();
                existingCts.Dispose();
            }

            var cts = new CancellationTokenSource();
            _autoSaveTimers[seatMapId] = cts;

            // Fire debounced auto-save
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(AutoSaveDelayMs, cts.Token);

                    // Still valid after delay? Execute auto-save
                    var versionResponse = await _designService.AutoSaveSnapshotAsync(seatMapId, userId);

                    await Clients.Group(GetGroupName(seatMapId)).SendAsync("AutoSaved", new
                    {
                        versionResponse.VersionNumber,
                        versionResponse.CreatedAt,
                        versionResponse.ChangeDescription
                    });

                    _logger.LogInformation("Auto-saved seat map {SeatMapId} as version {Version}",
                        seatMapId, versionResponse.VersionNumber);
                }
                catch (OperationCanceledException)
                {
                    // Debounced — another change came in, skip this save
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-save failed for seat map {SeatMapId}", seatMapId);
                }
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

    // ========== Helpers ==========

    private Guid GetUserId()
    {
        var sub = Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var userId) ? userId : throw new UnauthorizedException("Invalid user.");
    }

    private Guid GetOrgId()
    {
        var orgClaim = Context.User?.FindFirst("org_id")?.Value;
        return Guid.TryParse(orgClaim, out var orgId) ? orgId : throw new UnauthorizedException("Organization not found in token.");
    }

    private UserPresenceDto GetCurrentUser()
    {
        var userId = GetUserId();
        var email = Context.User?.FindFirst("email")?.Value ?? "";
        var name = Context.User?.FindFirst("name")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";

        // Generate deterministic avatar color from userId
        var hash = userId.GetHashCode();
        var colors = new[] { "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7", "#DDA0DD", "#98D8C8", "#F7DC6F" };
        var avatarColor = colors[Math.Abs(hash) % colors.Length];

        return new UserPresenceDto
        {
            UserId = userId,
            Email = email,
            DisplayName = name,
            AvatarColor = avatarColor
        };
    }

    private static string GetGroupName(Guid seatMapId) => $"seatmap-{seatMapId}";
}
