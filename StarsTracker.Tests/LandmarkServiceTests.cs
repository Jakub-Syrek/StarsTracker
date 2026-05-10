using StarsTracker.Services;

namespace StarsTracker.Tests;

/// <summary>
/// Sanity checks for the hard-coded landmark catalog: every entry should have
/// a unique non-empty name, plausible Krakow-area coordinates, and a
/// non-negative elevation. Bearings between landmarks should be self-consistent.
/// </summary>
public sealed class LandmarkServiceTests
{
    private readonly LandmarkService _service = new();

    [Fact]
    public void Catalog_IsNotEmpty()
    {
        _service.Landmarks.Should().NotBeEmpty();
    }

    [Fact]
    public void All_Names_AreNonEmpty()
    {
        _service.Landmarks.Should().AllSatisfy(l =>
            l.Name.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void All_Names_AreUnique()
    {
        var names = _service.Landmarks.Select(l => l.Name).ToList();
        names.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void All_CoordinatesAreInKrakowMetropolitanBoundingBox()
    {
        // Loose bounding box around Krakow: 50.00..50.10 N, 19.85..20.10 E
        _service.Landmarks.Should().AllSatisfy(l =>
        {
            l.Latitude.Should().BeInRange(49.95, 50.15);
            l.Longitude.Should().BeInRange(19.80, 20.15);
        });
    }

    [Fact]
    public void All_ElevationsAreNonNegative()
    {
        _service.Landmarks.Should().AllSatisfy(l =>
            l.ElevationMeters.Should().BeGreaterThanOrEqualTo(0));
    }

    [Fact]
    public void KopiecKosciuszki_IsPresent_AndWestOfWawel()
    {
        var kopiec = _service.Landmarks.FirstOrDefault(l => l.Name.Contains("Kościuszki"));
        var wawel = _service.Landmarks.FirstOrDefault(l => l.Name == "Wawel");
        kopiec.Should().NotBeNull();
        wawel.Should().NotBeNull();
        kopiec!.Longitude.Should().BeLessThan(wawel!.Longitude);
    }

    [Fact]
    public void BearingsBetweenLandmarks_AreReasonable()
    {
        var wawel = _service.Landmarks.First(l => l.Name == "Wawel");
        // Pick another landmark and confirm bearing is well-defined (not NaN).
        foreach (var other in _service.Landmarks.Where(l => l != wawel))
        {
            double bearing = GeoMath.BearingDeg(
                wawel.Latitude, wawel.Longitude,
                other.Latitude, other.Longitude);
            bearing.Should().BeInRange(0, 360);
            double distance = GeoMath.DistanceMeters(
                wawel.Latitude, wawel.Longitude,
                other.Latitude, other.Longitude);
            distance.Should().BeInRange(50, 50_000,
                "all configured landmarks live within Krakow + suburbs");
        }
    }
}
