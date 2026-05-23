using Eventiq.Contracts;
using Eventiq.UserService.Application.Service;
using MassTransit;

namespace Eventiq.UserService.Consumers;

public class StaffRoleChangedConsumer : IConsumer<StaffRoleChanged>
{
    private readonly ILogger<StaffRoleChangedConsumer> _logger;
    private readonly IRoleService _roleService;

    public StaffRoleChangedConsumer(ILogger<StaffRoleChangedConsumer> logger, IRoleService roleService)
    {
        _logger = logger;
        _roleService = roleService;
    }

    public async Task Consume(ConsumeContext<StaffRoleChanged> context)
    {
        var msg = context.Message;
        _logger.LogInformation("StaffRoleChanged: userId={UserId}, orgId={OrgId}, newRole={Role}",
            msg.UserId, msg.OrganizationId, msg.NewRoleName);
        await _roleService.UpdateOrgRoleAsync(msg.UserId, msg.OrganizationId, msg.NewRoleName);
    }
}
