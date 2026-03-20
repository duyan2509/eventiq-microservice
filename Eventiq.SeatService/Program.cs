using Eventiq.SeatService;
using Eventiq.SeatService.Extensions;
using Eventiq.SeatService.Hubs;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.MapControllers();
app.MapHub<SeatDesignHub>("/hubs/seat-design");
app.Run();
