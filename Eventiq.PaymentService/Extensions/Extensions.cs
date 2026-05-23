using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Eventiq.PaymentService.Application.Service.Implement;
using Eventiq.PaymentService.Application.Service.Interface;
using Eventiq.PaymentService.Helper;
using Eventiq.PaymentService.Infrastructure.Persistence;
using Eventiq.Logging;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Eventiq.PaymentService.Extensions;

public static class Extensions
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Host.UseEventiqSerilog();

        var connStr = builder.Configuration.GetConnectionString("Postgres")!;
        builder.Services.AddDbContext<PaymentDbContext>(opt =>
            opt.UseNpgsql(connStr).UseSnakeCaseNamingConvention());

        builder.Services.AddScoped<ICheckoutService, CheckoutService>();
        builder.Services.AddScoped<IWebhookService, WebhookService>();
        builder.Services.AddScoped<IOrderService, OrderService>();

        // gRPC clients
        var eventServiceUrl = builder.Configuration["InternalServices:EventServiceBaseUrl"] ?? "http://localhost:5232";
        var seatServiceUrl = builder.Configuration["InternalServices:SeatServiceBaseUrl"] ?? "http://localhost:5234";
        var orgServiceUrl = builder.Configuration["InternalServices:OrgServiceBaseUrl"] ?? "http://localhost:5230";

        builder.Services.AddGrpcClient<Eventiq.Contracts.Grpc.EventInternal.EventInternalClient>(o =>
            o.Address = new Uri(eventServiceUrl));
        builder.Services.AddGrpcClient<Eventiq.Contracts.Grpc.SeatInternal.SeatInternalClient>(o =>
            o.Address = new Uri(seatServiceUrl));
        builder.Services.AddGrpcClient<Eventiq.Contracts.Grpc.OrgInternal.OrgInternalClient>(o =>
            o.Address = new Uri(orgServiceUrl));

        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
        var publicKeyPath = builder.Configuration["Jwt:PublicKeyPath"];
        if (!string.IsNullOrEmpty(publicKeyPath))
        {
            var publicKey = RsaKeyLoader.LoadPublicKey(publicKeyPath);
            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;
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

        builder.Services.AddMassTransit(x =>
        {
            if (builder.Environment.IsDevelopment())
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(new Uri(builder.Configuration["MessageBus:RabbitMq:ConnectionString"] ?? string.Empty));
                    cfg.ConfigureEndpoints(context);
                });
            else
                x.UsingAzureServiceBus((context, cfg) =>
                {
                    cfg.Host(builder.Configuration["AzureServiceBus:ConnectionString"]);
                    cfg.ConfigureEndpoints(context);
                });
        });
    }
}
