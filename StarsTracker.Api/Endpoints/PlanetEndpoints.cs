using StarsTracker.Api.Services;

namespace StarsTracker.Api.Endpoints;

/// <summary>
/// Planet/Sun/Moon endpoints. A single GET takes the observer's UTC and
/// returns the geocentric apparent (RA, Dec) of every supported body so
/// the client can run them through its existing astronomy pipeline.
/// </summary>
public static class PlanetEndpoints
{
    public static IEndpointRouteBuilder MapPlanetEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/planets", (
            IPlanetEphemerisService ephemeris,
            DateTime? utc) =>
        {
            var when = (utc ?? DateTime.UtcNow).ToUniversalTime();
            var positions = ephemeris.Compute(when);
            return Results.Ok(new
            {
                utc = when,
                bodies = positions,
            });
        })
        .WithName("Planets")
        .WithSummary("Geocentric apparent positions of Sun, Moon and visible planets at a given UTC.")
        .WithTags("Sky");

        return endpoints;
    }
}
