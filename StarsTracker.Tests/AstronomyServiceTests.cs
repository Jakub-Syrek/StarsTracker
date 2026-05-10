using StarsTracker.Services;

namespace StarsTracker.Tests;

/// <summary>
/// Validates the core RA/Dec -> Az/Alt conversion against known geometric
/// invariants and a handful of well-published reference values.
/// </summary>
public sealed class AstronomyServiceTests
{
    private const double AltitudeToleranceDeg = 0.5;
    private const double AzimuthToleranceDeg = 1.0;

    /// <summary>
    /// Polaris (RA 2.530h, Dec +89.264°) is by definition almost on the
    /// celestial pole, so its altitude as seen from a given observer must
    /// equal that observer's geographic latitude (within a small fraction
    /// of a degree caused by the 0.74° offset of Polaris from the pole).
    /// </summary>
    [Theory]
    [InlineData(50.06, 19.94)]   // Krakow
    [InlineData(52.23, 21.01)]   // Warsaw
    [InlineData(0.00, 0.00)]     // Equator / Gulf of Guinea
    [InlineData(40.71, -74.00)]  // New York
    public void Polaris_AltitudeEqualsLatitude(double latitude, double longitude)
    {
        const double polarisRaHours = 2.530301;
        const double polarisDecDeg = 89.264109;

        AstronomyService.EquatorialToHorizontal(
            polarisRaHours * 15.0, polarisDecDeg,
            latitude, longitude,
            new DateTime(2026, 1, 15, 22, 0, 0, DateTimeKind.Utc),
            out _, out double altitude);

        altitude.Should().BeApproximately(latitude, 1.0,
            "Polaris sits ~0.74° from the celestial pole, so altitude tracks latitude with sub-degree error");
    }

    /// <summary>
    /// At the equator the celestial pole sits on the horizon, so Polaris must
    /// be at altitude ≈ 0° regardless of time of day or longitude.
    /// </summary>
    [Fact]
    public void Polaris_AtEquator_IsOnHorizon()
    {
        AstronomyService.EquatorialToHorizontal(
            raDeg: 2.530301 * 15.0,
            decDeg: 89.264109,
            latitudeDeg: 0,
            longitudeDeg: 0,
            utcNow: DateTime.UtcNow,
            out _, out double altitude);

        altitude.Should().BeInRange(-1.0, 1.5);
    }

    /// <summary>
    /// At the south pole, all stars with positive declination must be below
    /// the horizon (negative altitude).
    /// </summary>
    [Fact]
    public void NorthernStar_FromSouthPole_IsBelowHorizon()
    {
        AstronomyService.EquatorialToHorizontal(
            raDeg: 100.0,
            decDeg: 30.0,
            latitudeDeg: -90,
            longitudeDeg: 0,
            utcNow: DateTime.UtcNow,
            out _, out double altitude);

        altitude.Should().BeLessThan(0);
    }

    /// <summary>
    /// At the north pole, all stars with negative declination must be below
    /// the horizon.
    /// </summary>
    [Fact]
    public void SouthernStar_FromNorthPole_IsBelowHorizon()
    {
        AstronomyService.EquatorialToHorizontal(
            raDeg: 100.0,
            decDeg: -30.0,
            latitudeDeg: 90,
            longitudeDeg: 0,
            utcNow: DateTime.UtcNow,
            out _, out double altitude);

        altitude.Should().BeLessThan(0);
    }

    /// <summary>
    /// Returned azimuth must always be in [0, 360).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(180)]
    [InlineData(355)]
    public void Azimuth_AlwaysInZeroTo360(double raDeg)
    {
        AstronomyService.EquatorialToHorizontal(
            raDeg, 30.0,
            50.0, 20.0,
            new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            out double azimuth, out _);

        azimuth.Should().BeInRange(0, 360);
    }

    /// <summary>
    /// Returned altitude must always be in [-90, 90].
    /// </summary>
    [Theory]
    [InlineData(-89)]
    [InlineData(-30)]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(89)]
    public void Altitude_AlwaysInPlusMinus90(double decDeg)
    {
        AstronomyService.EquatorialToHorizontal(
            raDeg: 0,
            decDeg,
            latitudeDeg: 50,
            longitudeDeg: 20,
            utcNow: DateTime.UtcNow,
            out _, out double altitude);

        altitude.Should().BeInRange(-90, 90);
    }

    /// <summary>
    /// A celestial-equator star (Dec=0°) seen from latitude 50° N reaches
    /// its maximum altitude of (90 - 50) = 40° when it crosses the meridian,
    /// where its azimuth is exactly due South (180°). We sweep a full sidereal
    /// day to find that crossing rather than relying on a hand-picked GMST.
    /// </summary>
    [Fact]
    public void StarOnEquator_ReachesMaxAltitudeOf40_DueSouthFromLat50()
    {
        const double latitudeDeg = 50.0;
        const double raDeg = 0;
        const double decDeg = 0;

        var t0 = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        double maxAltitude = double.MinValue;
        double azAtMax = 0;

        for (int minute = 0; minute < 24 * 60; minute++)
        {
            AstronomyService.EquatorialToHorizontal(
                raDeg, decDeg,
                latitudeDeg, longitudeDeg: 0,
                utcNow: t0.AddMinutes(minute),
                out double az, out double alt);
            if (alt > maxAltitude)
            {
                maxAltitude = alt;
                azAtMax = az;
            }
        }

        maxAltitude.Should().BeApproximately(90 - latitudeDeg, AltitudeToleranceDeg);
        azAtMax.Should().BeApproximately(180, 2);
    }

    /// <summary>
    /// 24 hours later the same star (corrected for sidereal day) should be
    /// at virtually the same Az/Alt — sidereal day is 23h56m, so a calendar
    /// day later it shifts by ~1° but stays within the same hemisphere.
    /// </summary>
    [Fact]
    public void Repeatable_AcrossSiderealDay()
    {
        var t1 = new DateTime(2026, 6, 15, 22, 0, 0, DateTimeKind.Utc);
        var t2 = t1.AddDays(1).AddMinutes(-4); // approximate sidereal alignment

        AstronomyService.EquatorialToHorizontal(
            120, 30, 50, 20, t1,
            out double az1, out double alt1);
        AstronomyService.EquatorialToHorizontal(
            120, 30, 50, 20, t2,
            out double az2, out double alt2);

        az1.Should().BeApproximately(az2, 2);
        alt1.Should().BeApproximately(alt2, 2);
    }
}
