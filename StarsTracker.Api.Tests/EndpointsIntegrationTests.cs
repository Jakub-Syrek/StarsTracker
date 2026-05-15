using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StarsTracker.Shared.Contracts;

namespace StarsTracker.Api.Tests;

/// <summary>
/// Smoke tests against the actual HTTP pipeline — boots a WebApplicationFactory
/// and exercises each endpoint at the protocol level (status code + payload
/// shape).
/// </summary>
public sealed class EndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EndpointsIntegrationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Healthz_Returns200()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/healthz");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Planets_ReturnsSevenBodies()
    {
        using var client = _factory.CreateClient();
        var payload = await client.GetFromJsonAsync<PlanetsResponse>("/api/v1/planets?utc=2026-06-15T22:00:00Z");
        payload.Should().NotBeNull();
        payload!.Bodies.Should().HaveCount(7);
    }

    [Fact]
    public async Task Constellations_ReturnsNonEmptyCatalog()
    {
        using var client = _factory.CreateClient();
        var payload = await client.GetFromJsonAsync<List<ConstellationDto>>("/api/v1/constellations");
        payload.Should().NotBeNull();
        payload!.Should().HaveCountGreaterThan(10);
        payload.Should().AllSatisfy(c =>
        {
            c.Name.Should().NotBeNullOrWhiteSpace();
            c.Abbreviation.Should().HaveLength(3);
            c.Lines.Should().NotBeEmpty();
        });
    }

    private sealed record PlanetsResponse(DateTime Utc, IReadOnlyList<PlanetPositionDto> Bodies);
}
