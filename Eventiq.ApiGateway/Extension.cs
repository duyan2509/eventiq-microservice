using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Eventiq.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Eventiq.ApiGateway;

public static class Extension
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {

        // serilog 
        builder.Host.UseEventiqSerilog();
        var publicKey = RsaKeyLoader.LoadPublicKey(
            builder.Configuration["Jwt:PublicKeyPath"]
        );
        builder.Services.AddCors(options =>
        {
            var allowedOrigins = new List<string> { "http://localhost:3000" };
            
            // Add Vercel domain from configuration if available
            var vercelUrl = builder.Configuration["Cors:VercelUrl"];
            if (!string.IsNullOrEmpty(vercelUrl))
            {
                allowedOrigins.Add(vercelUrl);
            }
            
            // Allow any Vercel preview deployments (wildcard)
            var vercelPreviewPattern = builder.Configuration["Cors:VercelPreviewPattern"];
            if (!string.IsNullOrEmpty(vercelPreviewPattern))
            {
                allowedOrigins.Add(vercelPreviewPattern);
            }
            
            options.AddPolicy("AllowFrontend",
                policy => policy.WithOrigins(allowedOrigins.ToArray())
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials());
        });
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        builder.Services
            .AddAuthentication("Bearer")
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
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

                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = JwtRegisteredClaimNames.Sub,

                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });


        
    }
    public class LowercaseControllerTransformer : IOutboundParameterTransformer
    {
        public string TransformOutbound(object value)
        {
            return value?.ToString().ToLowerInvariant();
        }
    }
}