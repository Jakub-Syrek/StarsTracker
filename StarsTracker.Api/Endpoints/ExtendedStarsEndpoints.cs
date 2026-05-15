using StarsTracker.Api.Services;

namespace StarsTracker.Api.Endpoints;

public static class ExtendedStarsEndpoints
{
    public static IEndpointRouteBuilder MapExtendedStarsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/stars/extended", (IExtendedStarCatalog catalog) =>
            Results.Ok(catalog.All))
            .WithName("ExtendedStars")
            .WithSummary("Full naked-eye star catalogue (~5000 entries, mag ≤ 6) from HYG.")
            .WithTags("Sky");

        return endpoints;
    }
}
