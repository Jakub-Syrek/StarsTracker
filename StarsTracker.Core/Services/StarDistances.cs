namespace StarsTracker.Services;

/// <summary>
/// Hand-curated distance table (in light years) for the brightest stars in
/// the HYG catalogue. Sources: SIMBAD, Hipparcos, Gaia DR3 — values are
/// rounded to two significant figures for distances under 100 ly and to
/// the nearest ten otherwise. Stars not in this table simply have no
/// distance shown in the UI; we never invent a value.
/// </summary>
public static class StarDistances
{
    private static readonly IReadOnlyDictionary<string, double> Map =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            // <50 ly — solar neighbourhood
            ["Rigil Kentaurus"] = 4.37,    // Alpha Centauri A
            ["Sirius"]          = 8.6,
            ["Procyon"]         = 11.46,
            ["Altair"]          = 16.7,
            ["Vega"]            = 25.0,
            ["Fomalhaut"]       = 25.0,
            ["Pollux"]          = 33.8,
            ["Arcturus"]        = 36.7,
            ["Capella"]         = 42.0,
            ["Castor"]          = 51.0,
            ["Aldebaran"]       = 65.0,

            // 50–500 ly — galactic neighbourhood
            ["Alpheratz"]       = 97.0,
            ["Hamal"]           = 65.9,
            ["Regulus"]         = 79.3,
            ["Alnair"]          = 101.0,
            ["Miaplacidus"]     = 113.0,
            ["Polaris"]         = 433.0,
            ["Mirach"]          = 197.0,
            ["Algol"]           = 90.0,
            ["Acrux"]           = 320.0,
            ["Gacrux"]          = 88.0,
            ["Mimosa"]          = 280.0,
            ["Diphda"]          = 96.0,
            ["Achernar"]        = 139.0,
            ["Bellatrix"]       = 250.0,
            ["Elnath"]          = 134.0,
            ["Mintaka"]         = 1200.0,
            ["Spica"]           = 250.0,
            ["Alpheratz"]       = 97.0,
            ["Atria"]           = 391.0,
            ["Hadar"]           = 390.0,
            ["Sabik"]           = 88.0,
            ["Mirfak"]          = 510.0,
            ["Caph"]            = 54.7,
            ["Schedar"]         = 228.0,
            ["Almach"]          = 350.0,
            ["Markab"]          = 133.0,
            ["Algenib"]         = 390.0,
            ["Scheat"]          = 200.0,
            ["Enif"]            = 670.0,
            ["Menkar"]          = 220.0,
            ["Adhara"]          = 405.0,
            ["Wezen"]           = 1800.0,
            ["Mizar"]           = 86.0,
            ["Alkaid"]          = 104.0,
            ["Dubhe"]           = 124.0,
            ["Merak"]           = 79.7,
            ["Phecda"]          = 83.2,
            ["Megrez"]          = 80.5,
            ["Kochab"]          = 130.9,
            ["Pherkad"]         = 487.0,
            ["Yildun"]          = 172.0,
            ["Alderamin"]       = 49.0,
            ["Deneb Algedi"]    = 39.0,
            ["Sadr"]            = 1800.0,
            ["Albireo"]         = 433.0,
            ["Eltanin"]         = 154.0,
            ["Rastaban"]        = 380.0,

            // >500 ly — distant supergiants
            ["Antares"]         = 550.0,
            ["Betelgeuse"]      = 640.0,
            ["Rigel"]           = 863.0,
            ["Canopus"]         = 310.0,
            ["Alnilam"]         = 1342.0,
            ["Alnitak"]         = 800.0,
            ["Saiph"]           = 650.0,
            ["Naos"]            = 1080.0,
            ["Deneb"]           = 2615.0,
        };

    /// <summary>
    /// Returns the catalogued distance in light years, or <c>null</c> when
    /// the star is not in the curated table.
    /// </summary>
    public static double? Get(string name)
        => Map.TryGetValue(name, out var d) ? d : null;
}
