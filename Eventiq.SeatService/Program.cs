using Eventiq.SeatService;
using Eventiq.SeatService.Extensions;
using Eventiq.SeatService.Grpc;
using Eventiq.SeatService.Hubs;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Split ports: REST/SignalR on 5234 (HTTP/1.1), gRPC on 5334 (HTTP/2 plaintext).
// Http1AndHttp2 can't negotiate on plaintext (needs TLS+ALPN), so gRPC must have a dedicated h2 port.
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5234, o => o.Protocols = HttpProtocols.Http1AndHttp2);
    options.ListenLocalhost(5334, o => o.Protocols = HttpProtocols.Http2);
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

app.UseCors("SignalRCors");
app.UseOutputCache();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<GlobalExceptionMiddleware>();
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.MapControllers();
app.MapGrpcService<SeatInternalGrpcService>();
app.MapHub<SeatDesignHub>("/hubs/seat-design");
app.MapHub<SeatBookingHub>("/hubs/seat-booking");
app.Run();
