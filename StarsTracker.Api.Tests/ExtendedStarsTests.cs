using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StarsTracker.Shared.Contracts;

namespace StarsTracker.Api.Tests;

/// <summary>
/// Sanity checks for the embedded HYG-derived star catalogue served at
/// <c>/api/v1/stars/extended</c>.
/// </summary>
public sealed class ExtendedStarsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ExtendedStarsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task ExtendedStars_ReturnsAtLeastSeveralHundredEntries()
    {
        using var client = _factory.CreateClient();
        var stars = await client.GetFromJsonAsync<List<StarRecordDto>>("/api/v1/stars/extended");
        stars.Should().NotBeNull();
        // The HYG mag≤6 cut produces ~5000 rows; assert generously to
        // tolerate future trimming or noise updates.
        stars!.Count.Should().BeGreaterThan(2000);
    }

    [Fact]
    public async Task ExtendedStars_AreSortedByMagnitude_BrightestFirst()
    {
        using var client = _factory.CreateClient();
        var stars = await client.GetFromJsonAsync<List<StarRecordDto>>("/api/v1/stars/extended");
        stars.Should().NotBeNull();
        // Sirius (mag -1.44) should be in the very first few entries.
        stars!.Take(5).Should().Contain(s => s.Name == "Sirius");
        stars[0].Mag.Should().BeLessThan(stars[100].Mag);
    }

    [Fact]
    public async Task ExtendedStars_HaveCanonicalShape()
    {
        using var client = _factory.CreateClient();
        var stars = await client.GetFromJsonAsync<List<StarRecordDto>>("/api/v1/stars/extended");
        stars.Should().NotBeNull();
        stars!.Take(50).Should().AllSatisfy(s =>
        {
            s.Ra.Should().BeInRange(0, 24);
            s.Dec.Should().BeInRange(-90, 90);
            s.Name.Should().NotBeNullOrWhiteSpace();
        });
    }
}
