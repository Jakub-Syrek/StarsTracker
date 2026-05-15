using StarsTracker.Api.Endpoints;
using StarsTracker.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Honour the inbound port advertised by container hosts (Railway, Render,
// Fly) through $PORT. Locally the env var is unset and Kestrel falls back
// to launchSettings / ASPNETCORE_URLS as usual.
var injectedPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(injectedPort) &&
    int.TryParse(injectedPort, out var port) && port > 0)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddResponseCaching();

builder.Services.AddSingleton<IPlanetEphemerisService, PlanetEphemerisService>();
builder.Services.AddSingleton<IConstellationCatalog, ConstellationCatalog>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseResponseCaching();
app.UseStatusCodePages();

app.MapHealthEndpoints();
app.MapPlanetEndpoints();
app.MapConstellationEndpoints();

app.Run();

// Make Program visible to the test project for WebApplicationFactory.
public partial class Program;
