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

    /// <summary>
    /// Atmospheric refraction lifts the apparent altitude of a star above
    /// its true value, especially near the horizon. Uses Saemundsson's
    /// inverse-of-Bennett formula (1986), which expects the TRUE altitude
    /// as input and returns the refraction in degrees that should be added
    /// to obtain the APPARENT altitude as seen by an observer at sea level
    /// under standard atmospheric conditions.
    /// </summary>
    /// <param name="trueAltitudeDeg">Geometric altitude of the body, degrees.</param>
    /// <returns>Refraction in degrees (always non-negative). Below the
    /// horizon (alt &lt; -1°) the formula breaks down, so 0 is returned.</returns>
    public static double AtmosphericRefractionDeg(double trueAltitudeDeg)
    {
        if (trueAltitudeDeg < -1.0) return 0.0;
        double inner = trueAltitudeDeg + 10.3 / (trueAltitudeDeg + 5.11);
        double tan = Math.Tan(inner * Math.PI / 180.0);
        if (Math.Abs(tan) < 1e-12) return 0.0;
        // Saemundsson returns refraction in arcminutes; convert to degrees.
        return (1.02 / tan) / 60.0;
    }

    /// <summary>
    /// Linear precession from the J2000.0 epoch to the supplied UTC time.
    /// Accurate to a few arcseconds for the next ~50 years post-2000, which
    /// is good enough for visual AR overlay use. RA shifts by roughly
    /// 0.014° per year, Dec by up to ~20" per year depending on right
    /// ascension.
    /// </summary>
    /// <param name="raDeg">Right ascension at J2000, degrees (0–360).</param>
    /// <param name="decDeg">Declination at J2000, degrees (-90..+90).</param>
    /// <param name="utc">UTC time of observation.</param>
    /// <returns>(RA, Dec) in degrees at the observation epoch.</returns>
    public static (double raDeg, double decDeg) PrecessFromJ2000(
        double raDeg, double decDeg, DateTime utc)
    {
        var j2000 = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        double yearsSince = (utc.ToUniversalTime() - j2000).TotalDays / 365.25;

        double raRad = raDeg * Math.PI / 180.0;
        double decRad = decDeg * Math.PI / 180.0;

        // Precession rates per Julian year (J2000.0):
        //   m  = 3.07496 sec of time / yr  (RA constant term)
        //   n  = 1.33621 sec of time / yr  (RA latitude-dependent term)
        //   nd = 20.0431 arcsec        / yr  (Dec)
        const double mSecPerYear = 3.07496;
        const double nTSecPerYear = 1.33621;
        const double nDArcsecPerYear = 20.0431;

        double dRaSec = (mSecPerYear + nTSecPerYear * Math.Sin(raRad) * Math.Tan(decRad))
                        * yearsSince;
        double dDecArcsec = nDArcsecPerYear * Math.Cos(raRad) * yearsSince;

        // 1 sec of time = 15 arcseconds = 15/3600 degrees
        double dRaDeg = dRaSec * 15.0 / 3600.0;
        double dDecDeg = dDecArcsec / 3600.0;

        return (NormalizeAngle(raDeg + dRaDeg), decDeg + dDecDeg);
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
