using Eventiq.EventService;
using Eventiq.EventService.Extensions;
using Eventiq.EventService.Grpc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Split ports: REST on 5232 (HTTP/1.1), gRPC on 5332 (HTTP/2 plaintext).
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5232, o => o.Protocols = HttpProtocols.Http1AndHttp2);
    options.ListenLocalhost(5332, o => o.Protocols = HttpProtocols.Http2);
});

builder.Services.AddControllers();
builder.Services.AddGrpc();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.AddApplicationServices();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<GlobalExceptionMiddleware>();
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.MapControllers();
app.MapGrpcService<EventInternalGrpcService>();
app.Run();
