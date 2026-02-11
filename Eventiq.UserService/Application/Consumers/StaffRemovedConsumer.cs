using Eventiq.Contracts;
using Eventiq.UserService.Application.Service;
using MassTransit;

namespace Eventiq.UserService.Consumers;

public class StaffRemovedConsumer:IConsumer<StaffRemoved>
{
    public StaffRemovedConsumer(ILogger<StaffRemovedConsumer> logger, IRoleService roleService)
    {
        _logger = logger;
        _roleService = roleService;
    }

    private readonly ILogger<StaffRemovedConsumer> _logger;
    private readonly IRoleService  _roleService;
    public async Task Consume(ConsumeContext<StaffRemoved> context)
    {
        var message = context.Message;
        _logger.LogInformation($"Received Staff Remove Message: userId: {message.UserId}, orgId: {message.OrganizationId}");
        await _roleService.InvokeOrgStaffRoleAsync(message.UserId, message.OrganizationId);
    }
}