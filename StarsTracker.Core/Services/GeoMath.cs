namespace StarsTracker.Services;

/// <summary>
/// Geographic math helpers for short-range bearing/distance between two
/// (lat, lon) points using the spherical-earth approximation. Accurate to
/// ~0.5% within a few hundred kilometres — far better than what magnetometer
/// calibration needs.
/// </summary>
public static class GeoMath
{
    private const double EarthRadiusMeters = 6_371_000.0;

    /// <summary>
    /// True bearing in degrees (0–360, clockwise from North) from point 1 to point 2.
    /// </summary>
    public static double BearingDeg(double lat1, double lon1, double lat2, double lon2)
    {
        double phi1 = lat1 * Math.PI / 180.0;
        double phi2 = lat2 * Math.PI / 180.0;
        double dLambda = (lon2 - lon1) * Math.PI / 180.0;

        double y = Math.Sin(dLambda) * Math.Cos(phi2);
        double x = Math.Cos(phi1) * Math.Sin(phi2) -
                   Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(dLambda);

        return (Math.Atan2(y, x) * 180.0 / Math.PI + 360.0) % 360.0;
    }

    /// <summary>Great-circle distance in metres.</summary>
    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        double phi1 = lat1 * Math.PI / 180.0;
        double phi2 = lat2 * Math.PI / 180.0;
        double dPhi = (lat2 - lat1) * Math.PI / 180.0;
        double dLambda = (lon2 - lon1) * Math.PI / 180.0;

        double a = Math.Sin(dPhi / 2) * Math.Sin(dPhi / 2) +
                   Math.Cos(phi1) * Math.Cos(phi2) *
                   Math.Sin(dLambda / 2) * Math.Sin(dLambda / 2);

        return 2 * EarthRadiusMeters * Math.Asin(Math.Sqrt(a));
    }

    /// <summary>
    /// Approximate elevation angle (degrees, positive = above horizon) of a point
    /// at given elevation, viewed from the observer at observerElevation.
    /// </summary>
    public static double ElevationAngleDeg(
        double observerLat, double observerLon, double observerElevationM,
        double targetLat, double targetLon, double targetElevationM)
    {
        double horizontal = DistanceMeters(observerLat, observerLon, targetLat, targetLon);
        if (horizontal < 1) return 0;
        double dh = targetElevationM - observerElevationM;
        return Math.Atan2(dh, horizontal) * 180.0 / Math.PI;
    }
}
