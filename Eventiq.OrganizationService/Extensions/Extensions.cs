using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Eventiq.OrganizationService.Application.Service;
using Eventiq.OrganizationService.Infrastructure;
using Eventiq.Logging;
using Eventiq.OrganizationService.Helper;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Eventiq.OrganizationService.Extensions;

public static class Extensions
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Host.UseEventiqSerilog();
        builder.Services.AddServices(builder.Configuration)
            .AddInfrastructure(builder.Configuration);
        builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
        var publicKey = RsaKeyLoader.LoadPublicKey(
            builder.Configuration["Jwt:PublicKeyPath"]
        );
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "eventiq-auth",

                    ValidateAudience = true,
                    ValidAudience = "eventiq",

                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = publicKey,

                    NameClaimType = "sub",
                    RoleClaimType = ClaimTypes.Role
                };
            });

        builder.Services.AddMassTransit(x =>
        {
            if(builder.Environment.IsDevelopment())
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(
                        new Uri(builder.Configuration["MessageBus:RabbitMq:ConnectionString"] ?? string.Empty)
                    );
                    cfg.ConfigureEndpoints(context);
                });
            else
            {
                x.UsingAzureServiceBus((context, cfg) =>
                {
                    cfg.Host(builder.Configuration["AzureServiceBus:ConnectionString"]);
                    cfg.ConfigureEndpoints(context);
                });
            }
        });
        
    }

}
