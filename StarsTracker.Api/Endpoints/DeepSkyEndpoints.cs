using StarsTracker.Api.Services;

namespace StarsTracker.Api.Endpoints;

public static class DeepSkyEndpoints
{
    public static IEndpointRouteBuilder MapDeepSkyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/deepsky", (IDeepSkyCatalog catalog) =>
            Results.Ok(catalog.All))
            .WithName("DeepSky")
            .WithSummary("Curated Messier + showpiece deep-sky catalogue.")
            .WithTags("Sky");

        return endpoints;
    }
}
