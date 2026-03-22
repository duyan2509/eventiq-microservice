using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Eventiq.SeatService.Application.Service.Implement;
using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Consumers;
using Eventiq.SeatService.Helper;
using Eventiq.SeatService.Infrastructure;
using Eventiq.Logging;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Eventiq.SeatService.Extensions;

public static class ServiceExtensions
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Host.UseEventiqSerilog();

        // Application services
        builder.Services.AddScoped<ISeatMapService, SeatMapService>();
        builder.Services.AddScoped<ISeatDesignService, SeatDesignService>();

        // Infrastructure
        builder.Services.AddInfrastructure(builder.Configuration);

        // AutoMapper
        builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

        // SignalR + Redis backplane
        var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
        builder.Services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = builder.Environment.IsDevelopment();
            options.MaximumReceiveMessageSize = 512 * 1024; // 512KB for large seat map operations
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        }).AddStackExchangeRedis(redisConnectionString, options =>
        {
            options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("SeatDesign");
        });

        // JWT Auth
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

                    // Allow SignalR to receive JWT from query string
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) &&
                                path.StartsWithSegments("/hubs/seat-design"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });
            builder.Services.AddAuthorization();
        }

        // CORS for SignalR
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("SignalRCors", policy =>
            {
                policy
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()
                    .SetIsOriginAllowed(_ => true); // In production, restrict origins
            });
        });

        // MassTransit + RabbitMQ
        builder.Services.AddMassTransit(x =>
        {
            x.AddConsumer<ChartDeletedConsumer>();
            x.AddConsumer<StaffRemovedConsumer>();

            if (builder.Environment.IsDevelopment())
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(new Uri(builder.Configuration["RabbitMq:ConnectionString"] ?? string.Empty));
                    cfg.ConfigureEndpoints(context);
                });
            }
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
