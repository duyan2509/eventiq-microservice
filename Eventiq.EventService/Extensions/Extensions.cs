using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Eventiq.EventService.Application.Service;
using Eventiq.EventService.Helper;
using Eventiq.EventService.Infrastructure;
using Eventiq.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Eventiq.EventService.Extensions;

public static class Extensions
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Host.UseEventiqSerilog();
        builder.Services.AddServices(builder.Configuration)
            .AddInfrastructure(builder.Configuration);
        builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
        var publicKeyPath = builder.Configuration["Jwt:PublicKeyPath"];
        if (!string.IsNullOrEmpty(publicKeyPath))
        {
            var publicKey = RsaKeyLoader.LoadPublicKey(publicKeyPath);
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
            builder.Services.AddAuthorization();
        }
    }
}
