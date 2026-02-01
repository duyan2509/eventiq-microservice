using Eventiq.Logging;
using Eventiq.UserService.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddApplicationServices();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
// log request HTTP
app.UseSerilogRequestLogging();
app.UseAuthorization();

app.MapControllers();
Console.WriteLine("USER APP STARTED");

app.Run();