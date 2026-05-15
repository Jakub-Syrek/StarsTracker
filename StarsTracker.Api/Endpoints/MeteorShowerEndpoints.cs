using StarsTracker.Api.Services;

namespace StarsTracker.Api.Endpoints;

public static class MeteorShowerEndpoints
{
    public static IEndpointRouteBuilder MapMeteorShowerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/meteorshowers", (
            IMeteorShowerCatalog catalog,
            DateTime? utc) =>
        {
            var when = (utc ?? DateTime.UtcNow).ToUniversalTime();
            return Results.Ok(catalog.ActiveAround(when));
        })
        .WithName("MeteorShowers")
        .WithSummary("Annual meteor showers active around the supplied UTC (±7 days).")
        .WithTags("Sky");

        return endpoints;
    }
}
