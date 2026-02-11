using Eventiq.Contracts;
using Eventiq.UserService.Application.Service;
using MassTransit;

namespace Eventiq.UserService.Consumers;

public class StaffAcceptedConsumer:IConsumer<StaffAccepted>
{
    public StaffAcceptedConsumer(ILogger<StaffAcceptedConsumer> logger, IRoleService roleService)
    {
        _logger = logger;
        _roleService = roleService;
    }

    private readonly ILogger<StaffAcceptedConsumer> _logger;
    private readonly IRoleService  _roleService;
    public async Task Consume(ConsumeContext<StaffAccepted> context)
    {
        var message = context.Message;
        _logger.LogInformation($"Received Staff Accepted Message: userId: {message.UserId}, orgId: {message.OrganizationId}");
        await _roleService.AssignOrgStaffRoleAsync(message.UserId, message.OrganizationId);
    }
}