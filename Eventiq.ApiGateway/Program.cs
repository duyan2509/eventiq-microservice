using Eventiq.ApiGateway;
using Eventiq.Logging;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Pick the environment-specific routing table when present (e.g. ocelot.Production.json
// rewrites downstream hosts to the Azure Container Apps internal DNS names), otherwise
// fall back to the local ocelot.json (localhost:52xx).
var ocelotFile = $"ocelot.{builder.Environment.EnvironmentName}.json";
if (!File.Exists(ocelotFile))
{
    ocelotFile = "ocelot.json";
}
builder.Configuration.AddJsonFile(ocelotFile, optional: false, reloadOnChange: true);
builder.Services.AddOcelot(builder.Configuration);
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
app.UseCors("AllowFrontend");       
app.UseAuthentication();
app.UseAuthorization();
await app.UseOcelot();

app.Run();

