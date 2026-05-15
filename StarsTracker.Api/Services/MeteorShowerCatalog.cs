using StarsTracker.Shared.Contracts;

namespace StarsTracker.Api.Services;

/// <summary>
/// IMO-maintained catalogue of the major annual meteor showers. Peak dates
/// shift by a day or two between years; we use a fixed reference set and
/// roll the dates forward to the current calendar year on request. Radiant
/// coordinates are mean values — they actually drift across the activity
/// period but the deviation is small for AR-overlay purposes.
/// </summary>
public sealed class MeteorShowerCatalog : IMeteorShowerCatalog
{
    private static readonly ShowerSeed[] Seeds =
    [
        // code, name, peak (month, day), zhr, radiant RA deg, radiant Dec deg
        new("QUA", "Quadrantids",          1,  4, 110, 230.00, 49.50),
        new("LYR", "Lyrids",               4, 22,  18, 271.40, 33.30),
        new("ETA", "Eta Aquariids",        5,  6,  50, 338.00, -1.00),
        new("SDA", "Southern Delta Aquariids", 7, 30, 25, 339.00, -16.40),
        new("PER", "Perseids",             8, 12, 100,  48.00, 58.00),
        new("DRA", "Draconids",           10,  8,  10, 262.00, 54.00),
        new("ORI", "Orionids",            10, 21,  25,  95.50, 15.60),
        new("NTA", "Northern Taurids",    11, 12,   5,  58.00, 22.00),
        new("LEO", "Leonids",             11, 17,  15, 153.50, 21.60),
        new("GEM", "Geminids",            12, 14, 140, 112.20, 32.30),
        new("URS", "Ursids",              12, 22,  10, 217.00, 75.00),
    ];

    public IReadOnlyList<MeteorShowerDto> ActiveAround(DateTime utc)
    {
        utc = utc.ToUniversalTime();
        var year = utc.Year;
        var list = new List<MeteorShowerDto>(Seeds.Length);
        foreach (var seed in Seeds)
        {
            // The shower peak nearest to `utc`: either this year, last year
            // (for Quadrantids when we're in late December) or next year.
            DateTime[] candidates =
            [
                new(year - 1, seed.PeakMonth, seed.PeakDay, 0, 0, 0, DateTimeKind.Utc),
                new(year,     seed.PeakMonth, seed.PeakDay, 0, 0, 0, DateTimeKind.Utc),
                new(year + 1, seed.PeakMonth, seed.PeakDay, 0, 0, 0, DateTimeKind.Utc),
            ];

            DateTime closest = candidates[0];
            double bestAbsDays = double.MaxValue;
            foreach (var c in candidates)
            {
                double delta = Math.Abs((c - utc).TotalDays);
                if (delta < bestAbsDays)
                {
                    bestAbsDays = delta;
                    closest = c;
                }
            }

            int daysUntil = (int)Math.Round((closest - utc).TotalDays);
            if (Math.Abs(daysUntil) > 7) continue; // outside the active window

            list.Add(new MeteorShowerDto(
                seed.Code, seed.Name, closest,
                seed.ZenithalHourlyRate,
                seed.RadiantRaDeg, seed.RadiantDecDeg,
                daysUntil));
        }
        return list;
    }

    private readonly record struct ShowerSeed(
        string Code, string Name,
        int PeakMonth, int PeakDay,
        int ZenithalHourlyRate,
        double RadiantRaDeg, double RadiantDecDeg);
}
