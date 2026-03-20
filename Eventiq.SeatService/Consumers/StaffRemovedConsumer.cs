using Eventiq.Contracts;
using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Hubs;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace Eventiq.SeatService.Consumers;

/// <summary>
/// Consumes StaffRemoved event from OrganizationService.
/// When a staff member is removed from an org, disconnect them from any
/// active seat design sessions for that org's seat maps.
/// </summary>
public class StaffRemovedConsumer : IConsumer<StaffRemoved>
{
    private readonly ILogger<StaffRemovedConsumer> _logger;
    private readonly IUnitOfWork _uow;
    private readonly IPresenceService _presenceService;
    private readonly IHubContext<SeatDesignHub> _hubContext;

    public StaffRemovedConsumer(
        ILogger<StaffRemovedConsumer> logger,
        IUnitOfWork uow,
        IPresenceService presenceService,
        IHubContext<SeatDesignHub> hubContext)
    {
        _logger = logger;
        _uow = uow;
        _presenceService = presenceService;
        _hubContext = hubContext;
    }

    public async Task Consume(ConsumeContext<StaffRemoved> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Received StaffRemoved: UserId={UserId}, OrgId={OrgId}",
            message.UserId, message.OrganizationId);

        try
        {
            // Find all seat maps belonging to this organization
            // We need to remove the user from any active design sessions
            var seatMaps = await _uow.SeatMaps.GetByOrganizationIdAsync(message.OrganizationId);

            foreach (var seatMap in seatMaps)
            {
                // Remove from presence tracking
                await _presenceService.RemoveUserAsync(seatMap.Id, message.UserId);

                // Notify remaining users in the group
                var groupName = $"seatmap-{seatMap.Id}";
                await _hubContext.Clients.Group(groupName).SendAsync("UserKicked", new
                {
                    UserId = message.UserId,
                    Reason = "Staff membership revoked from organization."
                });

                _logger.LogInformation(
                    "Removed user {UserId} from seat map {SeatMapId} design session",
                    message.UserId, seatMap.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing StaffRemoved for UserId={UserId}, OrgId={OrgId}",
                message.UserId, message.OrganizationId);
            throw; // Let MassTransit retry
        }
    }
}
