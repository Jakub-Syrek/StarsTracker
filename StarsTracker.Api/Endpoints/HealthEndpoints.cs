namespace StarsTracker.Api.Endpoints;

/// <summary>
/// Liveness and readiness probes. The liveness route is intentionally cheap —
/// a 200 OK as long as the process is alive — so Railway and similar platform
/// orchestrators don't restart the container during a transient blip.
/// </summary>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
            .WithName("HealthLiveness")
            .WithSummary("Liveness probe — returns 200 OK while the process is up.");

        return endpoints;
    }
}
