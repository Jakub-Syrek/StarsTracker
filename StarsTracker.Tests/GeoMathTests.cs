using StarsTracker.Services;

namespace StarsTracker.Tests;

/// <summary>
/// Validates great-circle bearing, distance and elevation-angle helpers
/// against well-known reference points (Krakow, Warsaw, Wawel, Kopiec
/// Kosciuszki, etc.).
/// </summary>
public sealed class GeoMathTests
{
    // Approximate reference coordinates
    private const double KrakowLat = 50.0647, KrakowLon = 19.9450;
    private const double WarsawLat = 52.2297, WarsawLon = 21.0122;

    [Fact]
    public void DistanceFromPointToItself_IsZero()
    {
        GeoMath.DistanceMeters(KrakowLat, KrakowLon, KrakowLat, KrakowLon)
            .Should().BeApproximately(0, 1);
    }

    [Fact]
    public void DistanceKrakowToWarsaw_IsApproximately252Km()
    {
        // Reference: ~252 km great-circle
        double m = GeoMath.DistanceMeters(KrakowLat, KrakowLon, WarsawLat, WarsawLon);
        m.Should().BeInRange(248_000, 256_000);
    }

    [Fact]
    public void BearingKrakowToWarsaw_IsRoughlyNorthEast()
    {
        // Warsaw is north-northeast of Krakow — bearing should be ~10..15°
        double bearing = GeoMath.BearingDeg(KrakowLat, KrakowLon, WarsawLat, WarsawLon);
        bearing.Should().BeInRange(0, 30);
    }

    [Fact]
    public void BearingWarsawToKrakow_IsRoughlySouthWest()
    {
        // Reverse: Krakow is south-southwest of Warsaw — bearing should be ~190..200°
        double bearing = GeoMath.BearingDeg(WarsawLat, WarsawLon, KrakowLat, KrakowLon);
        bearing.Should().BeInRange(180, 210);
    }

    [Fact]
    public void Bearing_IsAlwaysInZeroTo360()
    {
        double[] bearings =
        [
            GeoMath.BearingDeg(0, 0, 0, 1),
            GeoMath.BearingDeg(0, 0, 1, 0),
            GeoMath.BearingDeg(0, 0, 0, -1),
            GeoMath.BearingDeg(0, 0, -1, 0),
            GeoMath.BearingDeg(45, 90, 45, 91),
        ];
        bearings.Should().AllSatisfy(b => b.Should().BeInRange(0, 360));
    }

    [Theory]
    [InlineData(0, 0, 0, 1, 90)]    // due East
    [InlineData(0, 0, 1, 0, 0)]     // due North
    [InlineData(0, 0, -1, 0, 180)]  // due South
    [InlineData(0, 0, 0, -1, 270)]  // due West
    public void BearingForCardinalDirections_IsCorrect(
        double lat1, double lon1, double lat2, double lon2, double expected)
    {
        GeoMath.BearingDeg(lat1, lon1, lat2, lon2)
            .Should().BeApproximately(expected, 1);
    }

    /// <summary>
    /// Bearing from Wawel to Kopiec Kosciuszki: due west-southwest. The mound
    /// sits west of Wawel and slightly south, so the bearing is ~263°.
    /// </summary>
    [Fact]
    public void BearingFromWawelToKopiecKosciuszki_IsWestSouthWest()
    {
        const double wawelLat = 50.0544, wawelLon = 19.9355;
        const double kopiecLat = 50.0547, kopiecLon = 19.8806;
        double bearing = GeoMath.BearingDeg(wawelLat, wawelLon, kopiecLat, kopiecLon);
        bearing.Should().BeInRange(255, 280);
    }

    [Fact]
    public void ElevationAngle_TargetAtSameHeight_IsZero()
    {
        double angle = GeoMath.ElevationAngleDeg(
            observerLat: 50, observerLon: 19, observerElevationM: 200,
            targetLat: 50.05, targetLon: 19, targetElevationM: 200);
        angle.Should().BeApproximately(0, 0.01);
    }

    [Fact]
    public void ElevationAngle_TargetHigher_IsPositive()
    {
        double angle = GeoMath.ElevationAngleDeg(
            observerLat: 50, observerLon: 19, observerElevationM: 200,
            targetLat: 50, targetLon: 19.001, targetElevationM: 400);
        angle.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ElevationAngle_TargetLower_IsNegative()
    {
        double angle = GeoMath.ElevationAngleDeg(
            observerLat: 50, observerLon: 19, observerElevationM: 400,
            targetLat: 50, targetLon: 19.001, targetElevationM: 200);
        angle.Should().BeLessThan(0);
    }

    [Fact]
    public void ElevationAngle_VeryCloseTarget_IsZero_NotNaN()
    {
        // Avoid division-by-zero when observer ~= target
        double angle = GeoMath.ElevationAngleDeg(
            50, 19, 0, 50, 19, 50);
        angle.Should().Be(0, "function clamps near-zero horizontal distance");
    }

    [Fact]
    public void Distance_IsSymmetric()
    {
        double a = GeoMath.DistanceMeters(KrakowLat, KrakowLon, WarsawLat, WarsawLon);
        double b = GeoMath.DistanceMeters(WarsawLat, WarsawLon, KrakowLat, KrakowLon);
        a.Should().BeApproximately(b, 0.5);
    }
}
