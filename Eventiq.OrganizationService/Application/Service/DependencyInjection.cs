using Eventiq.OrganizationService.Domain.Entity;

namespace Eventiq.OrganizationService.Application.Service;

public static class DependencyInjection
{
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IPermissionService, PermissionService>();
        return services;
    }
}
