using Eventiq.UserService;
using Eventiq.UserService.Application.Dto;
using Eventiq.UserService.Extensions;
using Eventiq.UserService.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.AddApplicationServices();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
// log request HTTP
app.UseSerilogRequestLogging();
app.UseAuthorization();
app.UseMiddleware<GlobalExceptionMiddleware>();

app.MapControllers();
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<EvtUserDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    await EvtUserDbSeeder.SeedAsync(context, logger, new RegisterDto()
    {
        Email= builder.Configuration["SeedAdmin:Email"],
        Password = builder.Configuration["SeedAdmin:Password"],
    });
}
app.Run();
