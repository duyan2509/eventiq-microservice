using Eventiq.Contracts;
using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Hubs;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace Eventiq.SeatService.Consumers;

/// <summary>
/// Consumes StaffRoleChanged from OrganizationService. A permission change may
/// flip the staff's designer flag (edit ↔ read-only), so notify that user in any
/// active design session to re-evaluate their access — the client re-fetches its
/// membership and updates the read-only gate without needing a page reload.
/// </summary>
public class StaffRoleChangedConsumer : IConsumer<StaffRoleChanged>
{
    private readonly ILogger<StaffRoleChangedConsumer> _logger;
    private readonly IUnitOfWork _uow;
    private readonly IHubContext<SeatDesignHub> _hubContext;

    public StaffRoleChangedConsumer(
        ILogger<StaffRoleChangedConsumer> logger,
        IUnitOfWork uow,
        IHubContext<SeatDesignHub> hubContext)
    {
        _logger = logger;
        _uow = uow;
        _hubContext = hubContext;
    }

    public async Task Consume(ConsumeContext<StaffRoleChanged> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Received StaffRoleChanged: UserId={UserId}, OrgId={OrgId}, NewRole={Role}",
            message.UserId, message.OrganizationId, message.NewRoleName);

        var seatMaps = await _uow.SeatMaps.GetByOrganizationIdAsync(message.OrganizationId);
        foreach (var seatMap in seatMaps)
        {
            // Whole-group send; the client ignores it unless the UserId is its own.
            await _hubContext.Clients.Group($"seatmap-{seatMap.Id}")
                .SendAsync("PermissionChanged", new { UserId = message.UserId });
        }
    }
}
