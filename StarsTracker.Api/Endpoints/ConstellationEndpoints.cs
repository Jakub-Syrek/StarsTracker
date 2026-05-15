using StarsTracker.Api.Services;

namespace StarsTracker.Api.Endpoints;

/// <summary>
/// Static constellation catalogue. Response is fully cacheable on the
/// client — the data is hand-curated and changes only when the API ships
/// a new release.
/// </summary>
public static class ConstellationEndpoints
{
    public static IEndpointRouteBuilder MapConstellationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/constellations", (IConstellationCatalog catalog) =>
            Results.Ok(catalog.All))
            .WithName("Constellations")
            .WithSummary("IAU constellation stick-figure lines keyed by star proper name.")
            .WithTags("Sky");

        return endpoints;
    }
}
