using StarsTracker.Shared.Contracts;

namespace StarsTracker.Api.Services;

/// <summary>
/// Low-precision planetary ephemeris based on the Standish (1992) Keplerian
/// orbital elements (Mercury–Saturn) and Meeus' simplified Sun/Moon
/// algorithms. Accurate to ~0.1° for the Sun, ~0.5° for the Moon, and ~1°
/// for the planets over 1800–2050 — well within the visual resolution of
/// the AR overlay.
/// </summary>
public sealed class PlanetEphemerisService : IPlanetEphemerisService
{
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    /// <summary>
    /// Mean obliquity of the ecliptic at J2000.0 (IAU 2000 value).
    /// </summary>
    private const double ObliquityDeg = 23.4392911;

    public IReadOnlyList<PlanetPositionDto> Compute(DateTime utc)
    {
        double jd = ToJulianDate(utc);
        double t = (jd - 2451545.0) / 36525.0; // Julian centuries since J2000

        var earth = HeliocentricEarth(t);

        var list = new List<PlanetPositionDto>(7)
        {
            ComputeSun(earth, jd),
            ComputeMoon(jd),
            ComputePlanet("Mercury", PlanetElements.Mercury, t, earth),
            ComputePlanet("Venus",   PlanetElements.Venus,   t, earth),
            ComputePlanet("Mars",    PlanetElements.Mars,    t, earth),
            ComputePlanet("Jupiter", PlanetElements.Jupiter, t, earth),
            ComputePlanet("Saturn",  PlanetElements.Saturn,  t, earth),
        };

        return list;
    }

    // ---------- Sun ----------

    private static PlanetPositionDto ComputeSun(EclipticVector earth, double jd)
    {
        // Sun's geocentric ecliptic position = negative of Earth's heliocentric.
        double x = -earth.X;
        double y = -earth.Y;
        double z = -earth.Z;

        double r = Math.Sqrt(x * x + y * y + z * z);
        double lambda = Math.Atan2(y, x) * RadToDeg;
        double beta = Math.Asin(z / r) * RadToDeg;

        var (ra, dec) = EclipticToEquatorial(lambda, beta);
        // Apparent magnitude of the Sun is constant from Earth.
        return new PlanetPositionDto("Sun", ra, dec, -26.74, r, PhaseFraction: null);
    }

    // ---------- Moon (Meeus, abridged) ----------

    private static PlanetPositionDto ComputeMoon(double jd)
    {
        double t = (jd - 2451545.0) / 36525.0;

        // Fundamental angles (degrees).
        double L = Wrap(218.316 + 481267.8813 * t);
        double M = Wrap(134.963 + 477198.8676 * t); // Moon mean anomaly
        double F = Wrap(93.272  + 483202.0175 * t);

        double lambda = L + 6.289 * Math.Sin(M * DegToRad);
        double beta   = 5.128 * Math.Sin(F * DegToRad);
        double deltaKm = 385001 - 20905 * Math.Cos(M * DegToRad);

        var (ra, dec) = EclipticToEquatorial(lambda, beta);

        // Distance in AU
        double distAu = deltaKm / 149_597_870.7;
        // Phase (very rough): elongation from the Sun.
        double sunMeanLong = Wrap(280.460 + 36000.770 * t);
        double elong = Math.Abs(Wrap(lambda - sunMeanLong + 180) - 180);
        double phase = (1 - Math.Cos(elong * DegToRad)) / 2.0;

        return new PlanetPositionDto("Moon", ra, dec, magnitudeForMoon(phase), distAu, phase);

        static double magnitudeForMoon(double phase) =>
            // Full moon ~ -12.7, new moon dims fast. Smooth approximation.
            -12.7 + 2.5 * Math.Log10(1.0 / Math.Max(phase, 0.01));
    }

    // ---------- Planets (Mercury–Saturn) ----------

    private static PlanetPositionDto ComputePlanet(
        string name, KeplerElements el, double t, EclipticVector earth)
    {
        double a = el.A0 + el.ADot * t;
        double e = el.E0 + el.EDot * t;
        double i = el.I0 + el.IDot * t;
        double L = el.L0 + el.LDot * t;
        double wbar = el.WBar0 + el.WBarDot * t;
        double omega = el.Omega0 + el.OmegaDot * t;

        // Argument of perihelion and mean anomaly.
        double w = wbar - omega;
        double M = Wrap(L - wbar);

        double E = SolveKepler(M * DegToRad, e);

        // Heliocentric coordinates in the planet's own orbital plane.
        double xPrime = a * (Math.Cos(E) - e);
        double yPrime = a * Math.Sqrt(1 - e * e) * Math.Sin(E);

        // Rotate to the J2000 ecliptic.
        double cosW = Math.Cos(w * DegToRad);
        double sinW = Math.Sin(w * DegToRad);
        double cosO = Math.Cos(omega * DegToRad);
        double sinO = Math.Sin(omega * DegToRad);
        double cosI = Math.Cos(i * DegToRad);
        double sinI = Math.Sin(i * DegToRad);

        double xH = (cosW * cosO - sinW * sinO * cosI) * xPrime
                  + (-sinW * cosO - cosW * sinO * cosI) * yPrime;
        double yH = (cosW * sinO + sinW * cosO * cosI) * xPrime
                  + (-sinW * sinO + cosW * cosO * cosI) * yPrime;
        double zH = (sinW * sinI) * xPrime + (cosW * sinI) * yPrime;

        // Geocentric = planet heliocentric − Earth heliocentric.
        double xG = xH - earth.X;
        double yG = yH - earth.Y;
        double zG = zH - earth.Z;

        double distance = Math.Sqrt(xG * xG + yG * yG + zG * zG);
        double lambda = Math.Atan2(yG, xG) * RadToDeg;
        double beta = Math.Asin(zG / distance) * RadToDeg;
        var (ra, dec) = EclipticToEquatorial(lambda, beta);

        double mag = ApproxPlanetMagnitude(name, distance,
            heliocentric: Math.Sqrt(xH * xH + yH * yH + zH * zH));

        // Phase only for inferior planets (Mercury, Venus).
        double? phase = (name is "Mercury" or "Venus")
            ? InferiorPlanetPhase(xH, yH, zH, earth, distance)
            : null;

        return new PlanetPositionDto(name, ra, dec, mag, distance, phase);
    }

    private static double InferiorPlanetPhase(double xH, double yH, double zH,
        EclipticVector earth, double earthDistance)
    {
        // cos(elongation) using law of cosines.
        double rH = Math.Sqrt(xH * xH + yH * yH + zH * zH);
        double rE = Math.Sqrt(earth.X * earth.X + earth.Y * earth.Y + earth.Z * earth.Z);
        double cosPhase = (earthDistance * earthDistance + rH * rH - rE * rE) /
                          (2 * earthDistance * rH);
        return (1 + cosPhase) / 2.0; // simple Lambertian phase
    }

    private static double ApproxPlanetMagnitude(string name, double distance, double heliocentric)
    {
        // Coarse magnitudes at unit distance, tuned for visibility heuristics.
        // Real magnitudes vary with phase; for an AR overlay these are fine.
        double absoluteMag = name switch
        {
            "Mercury" => -0.42,
            "Venus"   => -4.40,
            "Mars"    => -1.52,
            "Jupiter" => -9.40,
            "Saturn"  => -8.88,
            _         => 5.0,
        };
        return absoluteMag + 5 * Math.Log10(distance * heliocentric);
    }

    // ---------- Earth heliocentric ecliptic (J2000) ----------

    private static EclipticVector HeliocentricEarth(double t)
    {
        // Use the EMB (Earth-Moon Barycentre) Standish elements; close enough.
        var el = PlanetElements.EarthMoonBarycenter;

        double a = el.A0 + el.ADot * t;
        double e = el.E0 + el.EDot * t;
        double i = el.I0 + el.IDot * t;
        double L = el.L0 + el.LDot * t;
        double wbar = el.WBar0 + el.WBarDot * t;
        double omega = el.Omega0 + el.OmegaDot * t;
        double w = wbar - omega;
        double M = Wrap(L - wbar);

        double E = SolveKepler(M * DegToRad, e);
        double xPrime = a * (Math.Cos(E) - e);
        double yPrime = a * Math.Sqrt(1 - e * e) * Math.Sin(E);

        double cosW = Math.Cos(w * DegToRad), sinW = Math.Sin(w * DegToRad);
        double cosO = Math.Cos(omega * DegToRad), sinO = Math.Sin(omega * DegToRad);
        double cosI = Math.Cos(i * DegToRad), sinI = Math.Sin(i * DegToRad);

        double x = (cosW * cosO - sinW * sinO * cosI) * xPrime
                 + (-sinW * cosO - cosW * sinO * cosI) * yPrime;
        double y = (cosW * sinO + sinW * cosO * cosI) * xPrime
                 + (-sinW * sinO + cosW * cosO * cosI) * yPrime;
        double z = (sinW * sinI) * xPrime + (cosW * sinI) * yPrime;
        return new EclipticVector(x, y, z);
    }

    // ---------- Helpers ----------

    private static double SolveKepler(double mRad, double e)
    {
        // Newton-Raphson, ~5 iterations is plenty for naked-eye accuracy.
        double E = mRad;
        for (int n = 0; n < 8; n++)
        {
            double dE = (E - e * Math.Sin(E) - mRad) / (1 - e * Math.Cos(E));
            E -= dE;
            if (Math.Abs(dE) < 1e-9) break;
        }
        return E;
    }

    private static (double ra, double dec) EclipticToEquatorial(double lambdaDeg, double betaDeg)
    {
        double lambda = lambdaDeg * DegToRad;
        double beta = betaDeg * DegToRad;
        double eps = ObliquityDeg * DegToRad;

        double sinDec = Math.Sin(beta) * Math.Cos(eps) +
                        Math.Cos(beta) * Math.Sin(eps) * Math.Sin(lambda);
        double dec = Math.Asin(sinDec);

        double y = Math.Sin(lambda) * Math.Cos(eps) - Math.Tan(beta) * Math.Sin(eps);
        double x = Math.Cos(lambda);
        double ra = Math.Atan2(y, x);
        double raDeg = (ra * RadToDeg + 360.0) % 360.0;

        return (raDeg, dec * RadToDeg);
    }

    private static double Wrap(double deg)
    {
        deg %= 360.0;
        if (deg < 0) deg += 360.0;
        return deg;
    }

    private static double ToJulianDate(DateTime utc)
    {
        int y = utc.Year;
        int m = utc.Month;
        double d = utc.Day
                   + utc.Hour / 24.0
                   + utc.Minute / 1440.0
                   + utc.Second / 86400.0
                   + utc.Millisecond / 86_400_000.0;
        if (m <= 2) { y--; m += 12; }
        int a = y / 100;
        int b = 2 - a + a / 4;
        return Math.Floor(365.25 * (y + 4716))
             + Math.Floor(30.6001 * (m + 1))
             + d + b - 1524.5;
    }

    private readonly record struct EclipticVector(double X, double Y, double Z);
}
