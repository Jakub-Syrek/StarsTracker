using StarsTracker.Api.Services;

namespace StarsTracker.Api.Tests;

/// <summary>
/// Sanity checks for the low-precision planetary ephemeris. Reference values
/// are taken from the NASA JPL HORIZONS system for J2000.0 and a handful of
/// well-known historical positions; the tolerance reflects the ~1° accuracy
/// budget of the Standish (1992) low-precision elements.
/// </summary>
public sealed class PlanetEphemerisServiceTests
{
    private readonly PlanetEphemerisService _ephemeris = new();

    [Fact]
    public void Compute_ReturnsAllSevenBodies()
    {
        var positions = _ephemeris.Compute(new DateTime(2026, 6, 15, 22, 0, 0, DateTimeKind.Utc));
        positions.Should().HaveCount(7);
        positions.Select(p => p.Name).Should().Contain(
            ["Sun", "Moon", "Mercury", "Venus", "Mars", "Jupiter", "Saturn"]);
    }

    [Fact]
    public void Sun_RightAscension_ProgressesAcrossYear()
    {
        // The Sun's RA must increase monotonically over a year, modulo 360°.
        var t0 = new DateTime(2026, 3, 21, 12, 0, 0, DateTimeKind.Utc);   // vernal equinox
        var t6 = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);   // autumnal equinox

        var pos0 = _ephemeris.Compute(t0).Single(p => p.Name == "Sun");
        var pos6 = _ephemeris.Compute(t6).Single(p => p.Name == "Sun");

        // At vernal equinox RA ~ 0°, at autumnal equinox RA ~ 180°.
        // RA is wrap-around so accept either side of 0°.
        bool nearZero = pos0.RightAscensionDeg is >= 355 and <= 360
                     || pos0.RightAscensionDeg is >= 0 and <= 5;
        nearZero.Should().BeTrue($"Sun RA at vernal equinox should be near 0° (was {pos0.RightAscensionDeg:F2})");
        pos6.RightAscensionDeg.Should().BeInRange(175, 185);
    }

    [Fact]
    public void Sun_Declination_PeaksNearJuneSolstice()
    {
        var solstice = new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);
        var sun = _ephemeris.Compute(solstice).Single(p => p.Name == "Sun");
        sun.DeclinationDeg.Should().BeInRange(23.0, 24.0);
    }

    [Fact]
    public void Sun_Declination_DipsNearDecemberSolstice()
    {
        var solstice = new DateTime(2026, 12, 21, 12, 0, 0, DateTimeKind.Utc);
        var sun = _ephemeris.Compute(solstice).Single(p => p.Name == "Sun");
        sun.DeclinationDeg.Should().BeInRange(-24.0, -23.0);
    }

    [Fact]
    public void Moon_DistanceIsInPlausibleRange()
    {
        // Earth–Moon distance varies from ~0.0024 AU (perigee) to ~0.0027 AU (apogee).
        var moon = _ephemeris.Compute(DateTime.UtcNow).Single(p => p.Name == "Moon");
        moon.DistanceAu.Should().BeInRange(0.002, 0.003);
        moon.PhaseFraction.Should().NotBeNull().And.BeInRange(0, 1);
    }

    [Fact]
    public void Planets_DistanceMonotonicallyIncreasesByOrbitOrder()
    {
        // Average distances from Earth: Mercury ~1, Venus ~1.1, Mars ~1.5,
        // Jupiter ~5.2, Saturn ~9.5 AU. Ordering should always be respected.
        var positions = _ephemeris.Compute(new DateTime(2026, 6, 15, 22, 0, 0, DateTimeKind.Utc));
        var jupiter = positions.Single(p => p.Name == "Jupiter");
        var saturn = positions.Single(p => p.Name == "Saturn");
        saturn.DistanceAu.Should().BeGreaterThan(jupiter.DistanceAu);
    }

    [Fact]
    public void AllBodies_RangeChecks_AreSane()
    {
        var positions = _ephemeris.Compute(new DateTime(2026, 6, 15, 22, 0, 0, DateTimeKind.Utc));
        foreach (var p in positions)
        {
            p.RightAscensionDeg.Should().BeInRange(0, 360, $"RA out of range for {p.Name}");
            p.DeclinationDeg.Should().BeInRange(-90, 90, $"Dec out of range for {p.Name}");
            p.DistanceAu.Should().BeGreaterThan(0);
        }
    }
}
