using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Eventiq.EventService.Application.Service;
using Eventiq.EventService.Consumers;
using Eventiq.EventService.Infrastructure.Persistence;
using Eventiq.EventService.Helper;
using Eventiq.EventService.Infrastructure;
using Eventiq.EventService.Infrastructure.Http;
using Eventiq.Logging;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Eventiq.EventService.Extensions;

public static class Extensions
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Host.UseEventiqSerilog();
        builder.Services.AddHttpClient();
        var seatServiceUrl = builder.Configuration["InternalServices:SeatServiceBaseUrl"] ?? "http://localhost:5334";
        builder.Services.AddGrpcClient<Eventiq.Contracts.Grpc.SeatInternal.SeatInternalClient>(o =>
        {
            o.Address = new Uri(seatServiceUrl);
        });
        builder.Services.AddScoped<ISeatServiceClient, SeatServiceClient>();
        builder.Services.AddServices(builder.Configuration)
            .AddInfrastructure(builder.Configuration);
        builder.Services.AddAutoMapper(_ => { }, AppDomain.CurrentDomain.GetAssemblies());
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

        // MassTransit configuration
        builder.Services.AddMassTransit(x =>
        {
            x.AddConsumer<PaymentConfiguredConsumer>();
            // Saga orchestration: saga sends IssueTicketsCommand after seats are marked sold.
            // Consumer issues ticket records and publishes TicketsIssued so saga can finalize.
            x.AddConsumer<IssueTicketsConsumer>(c =>
            {
                c.UseMessageRetry(r =>
                    r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));
            })
                .Endpoint(e => e.Name = "event-service-issue-tickets");

            x.AddEntityFrameworkOutbox<EvtEventDbContext>(o =>
            {
                o.UsePostgres();
                o.QueryDelay = TimeSpan.FromSeconds(1);
                o.UseBusOutbox();
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(
                    new Uri(builder.Configuration["MessageBus:RabbitMq:ConnectionString"] ?? string.Empty)
                );
                cfg.ConfigureEndpoints(context);
            });
        });
    }
}
