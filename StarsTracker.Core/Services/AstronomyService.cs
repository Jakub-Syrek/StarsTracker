namespace StarsTracker.Services;

/// <summary>
/// Converts equatorial star coordinates (RA/Dec) to horizontal coordinates (Azimuth/Altitude)
/// given observer location and time.
/// </summary>
public static class AstronomyService
{
    /// <summary>
    /// Converts a star's equatorial position to local Azimuth and Altitude.
    /// </summary>
    /// <param name="raDeg">Right Ascension in DEGREES (convert from hours: ra_h * 15)</param>
    /// <param name="decDeg">Declination in degrees</param>
    /// <param name="latitudeDeg">Observer latitude in degrees</param>
    /// <param name="longitudeDeg">Observer longitude in degrees (east positive)</param>
    /// <param name="utcNow">Current UTC time</param>
    /// <param name="azimuthDeg">Output: Azimuth in degrees (0=N, 90=E, 180=S, 270=W)</param>
    /// <param name="altitudeDeg">Output: Altitude in degrees (0=horizon, 90=zenith)</param>
    public static void EquatorialToHorizontal(
        double raDeg,
        double decDeg,
        double latitudeDeg,
        double longitudeDeg,
        DateTime utcNow,
        out double azimuthDeg,
        out double altitudeDeg)
    {
        // --- 1. Julian Date ---
        double jd = ToJulianDate(utcNow);

        // --- 2. Greenwich Mean Sidereal Time (GMST) in degrees ---
        double t = (jd - 2451545.0) / 36525.0;
        double gmstDeg = 280.46061837
                         + 360.98564736629 * (jd - 2451545.0)
                         + t * t * 0.000387933
                         - t * t * t / 38710000.0;
        gmstDeg = NormalizeAngle(gmstDeg);

        // --- 3. Local Sidereal Time (LST) ---
        double lstDeg = NormalizeAngle(gmstDeg + longitudeDeg);

        // --- 4. Hour Angle ---
        double haDeg = NormalizeAngle(lstDeg - raDeg);

        // --- 5. Convert to radians ---
        double haRad = ToRad(haDeg);
        double decRad = ToRad(decDeg);
        double latRad = ToRad(latitudeDeg);

        // --- 6. Altitude ---
        double sinAlt = Math.Sin(decRad) * Math.Sin(latRad)
                      + Math.Cos(decRad) * Math.Cos(latRad) * Math.Cos(haRad);
        altitudeDeg = ToDeg(Math.Asin(sinAlt));

        // --- 7. Azimuth ---
        double cosAlt = Math.Cos(ToRad(altitudeDeg));
        if (Math.Abs(cosAlt) < 1e-10)
        {
            azimuthDeg = 0.0;
            return;
        }

        double cosAz = (Math.Sin(decRad) - Math.Sin(latRad) * sinAlt)
                     / (Math.Cos(latRad) * cosAlt);
        cosAz = Math.Clamp(cosAz, -1.0, 1.0);
        azimuthDeg = ToDeg(Math.Acos(cosAz));

        if (Math.Sin(haRad) > 0.0)
            azimuthDeg = 360.0 - azimuthDeg;
    }

    // --- Helpers ---

    private static double ToJulianDate(DateTime utc)
    {
        int y = utc.Year;
        int m = utc.Month;
        double d = utc.Day
                   + utc.Hour / 24.0
                   + utc.Minute / 1440.0
                   + utc.Second / 86400.0;

        if (m <= 2) { y--; m += 12; }
        int a = y / 100;
        int b = 2 - a + a / 4;
        return Math.Floor(365.25 * (y + 4716))
             + Math.Floor(30.6001 * (m + 1))
             + d + b - 1524.5;
    }

    private static double NormalizeAngle(double deg)
    {
        deg %= 360.0;
        if (deg < 0) deg += 360.0;
        return deg;
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
    private static double ToDeg(double rad) => rad * 180.0 / Math.PI;
}
