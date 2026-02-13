using Eventiq.Contracts;
using Eventiq.EmailService.Services;
using Eventiq.EmailService.Templates;
using Eventiq.EmailService.Consumers;
using MassTransit;
using RazorLight;
using TemplateRenderer = Eventiq.EmailService.Templates.TemplateRenderer;

var builder = Host.CreateApplicationBuilder(args);

// Email services
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.AddSingleton<ITemplateRenderer>(sp =>
{
    var env = sp.GetRequiredService<IHostEnvironment>();
    var templatesRoot = Path.Combine(env.ContentRootPath, "Templates");

    var engine = new RazorLightEngineBuilder()
        .UseFileSystemProject(templatesRoot)
        .UseMemoryCachingProvider()
        .Build();

    return new TemplateRenderer(engine);
});

// MassTransit configuration
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<InvitationCreatedConsumer>();

    if (builder.Environment.IsDevelopment())
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            var connectionString = builder.Configuration["RabbitMq:ConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("RabbitMq:ConnectionString is not configured.");
            }

            cfg.Host(new Uri(connectionString));
            cfg.ConfigureEndpoints(context);
        });
    }
    else
    {
        x.UsingAzureServiceBus((context, cfg) =>
        {
            var connectionString = builder.Configuration["AzureServiceBus:ConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("AzureServiceBus:ConnectionString is not configured.");
            }

            cfg.Host(connectionString);
            cfg.ConfigureEndpoints(context);
        });
    }
});

var host = builder.Build();
await host.RunAsync();
