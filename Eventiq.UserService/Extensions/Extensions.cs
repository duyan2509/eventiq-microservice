using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Eventiq.Logging;
using Eventiq.UserService.Application.Service;
using Eventiq.UserService.Helper;
using Eventiq.UserService.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Eventiq.UserService.Extensions;

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


    }
}