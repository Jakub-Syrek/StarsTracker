namespace StarsTracker.Api.Services;

/// <summary>
/// Mean Keplerian orbital elements at J2000.0 (Standish 1992 / NASA JPL
/// low-precision table). Each element is paired with a centennial drift
/// rate (per Julian century). Validity ~1800–2050, accuracy ~1° for the
/// planets — sufficient for naked-eye AR overlay.
/// </summary>
internal readonly record struct KeplerElements(
    double A0, double ADot,           // semi-major axis (AU)
    double E0, double EDot,           // eccentricity
    double I0, double IDot,           // inclination (deg)
    double L0, double LDot,           // mean longitude (deg)
    double WBar0, double WBarDot,     // longitude of perihelion (deg)
    double Omega0, double OmegaDot);  // longitude of ascending node (deg)

internal static class PlanetElements
{
    public static readonly KeplerElements Mercury = new(
        0.38709927,  0.00000037,
        0.20563593,  0.00001906,
        7.00497902, -0.00594749,
        252.25032350, 149472.67411175,
        77.45779628,  0.16047689,
        48.33076593, -0.12534081);

    public static readonly KeplerElements Venus = new(
        0.72333566,  0.00000390,
        0.00677672, -0.00004107,
        3.39467605, -0.00078890,
        181.97909950, 58517.81538729,
        131.60246718, 0.00268329,
        76.67984255, -0.27769418);

    public static readonly KeplerElements EarthMoonBarycenter = new(
        1.00000261,  0.00000562,
        0.01671123, -0.00004392,
       -0.00001531, -0.01294668,
        100.46457166, 35999.37244981,
        102.93768193,  0.32327364,
        0.0,           0.0);

    public static readonly KeplerElements Mars = new(
        1.52371034,  0.00001847,
        0.09339410,  0.00007882,
        1.84969142, -0.00813131,
       -4.55343205, 19140.30268499,
       -23.94362959, 0.44441088,
        49.55953891, -0.29257343);

    public static readonly KeplerElements Jupiter = new(
        5.20288700, -0.00011607,
        0.04838624, -0.00013253,
        1.30439695, -0.00183714,
        34.39644051, 3034.74612775,
        14.72847983,  0.21252668,
        100.47390909, 0.20469106);

    public static readonly KeplerElements Saturn = new(
        9.53667594, -0.00125060,
        0.05386179, -0.00050991,
        2.48599187,  0.00193609,
        49.95424423, 1222.49362201,
        92.59887831, -0.41897216,
        113.66242448, -0.28867794);
}
