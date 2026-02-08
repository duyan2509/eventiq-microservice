using Eventiq.Contracts;
using Eventiq.UserService.Application.Service;
using MassTransit;

namespace Eventiq.UserService.Consumers;

public class OrganizationCreatedConsumer : IConsumer<OrganizationCreated>
{
    private readonly ILogger<OrganizationCreatedConsumer> _logger;
    private readonly IRoleService  _roleService;

    public OrganizationCreatedConsumer(ILogger<OrganizationCreatedConsumer> logger, IRoleService roleService)
    {
        _logger = logger;
        _roleService = roleService;
    }

    public async Task Consume(ConsumeContext<OrganizationCreated> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "OrganizationCreated received.  OwnerId={OwnerId}, OrganizationId={orgId}", message.OwnerId, message.OrganizationId);
        await _roleService.EnsureOrgRoleAsync(context.Message.OwnerId, context.Message.OrganizationId);
    }
}
