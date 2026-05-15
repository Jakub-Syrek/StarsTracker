using StarsTracker.Shared.Contracts;

namespace StarsTracker.Api.Services;

/// <summary>
/// Curated subset of the Messier catalogue plus a handful of well-known
/// non-Messier showpieces (Double Cluster, North America Nebula). Values are
/// taken from SIMBAD / NGC/IC; magnitudes are the integrated visual mag.
/// </summary>
public sealed class DeepSkyCatalog : IDeepSkyCatalog
{
    public IReadOnlyList<DeepSkyObjectDto> All { get; } =
    [
        // ---- Galaxies ----
        new("M31",  "Andromeda Galaxy",          "Galaxy",            10.6847, 41.2691, 3.4, 178),
        new("M32",  "Andromeda Satellite M32",   "Galaxy",            10.6743, 40.8654, 8.1,   8),
        new("M33",  "Triangulum Galaxy",         "Galaxy",            23.4621, 30.6602, 5.7,  73),
        new("M51",  "Whirlpool Galaxy",          "Galaxy",           202.4696, 47.1952, 8.4,  11),
        new("M64",  "Black Eye Galaxy",          "Galaxy",           194.1819, 21.6831, 8.5,  10),
        new("M77",  "Cetus A",                   "Galaxy",            40.6696, -0.0133, 8.9,   7),
        new("M81",  "Bode's Galaxy",             "Galaxy",           148.8882, 69.0653, 6.9,  27),
        new("M82",  "Cigar Galaxy",              "Galaxy",           148.9683, 69.6797, 8.4,  11),
        new("M101", "Pinwheel Galaxy",           "Galaxy",           210.8023, 54.3489, 7.9,  29),
        new("M104", "Sombrero Galaxy",           "Galaxy",           189.9977,-11.6231, 8.0,   9),

        // ---- Emission / Reflection Nebulae ----
        new("M1",   "Crab Nebula",               "Supernova Remnant",  83.6324, 22.0145, 8.4,   8),
        new("M8",   "Lagoon Nebula",             "Nebula",            271.0667,-24.3850, 6.0,  90),
        new("M16",  "Eagle Nebula",              "Nebula",            274.7000,-13.8067, 6.0,  35),
        new("M17",  "Omega Nebula",              "Nebula",            275.1958,-16.1772, 6.0,  46),
        new("M20",  "Trifid Nebula",             "Nebula",            270.6042,-23.0306, 6.3,  28),
        new("M42",  "Orion Nebula",              "Nebula",             83.8221, -5.3911, 4.0,  85),
        new("M43",  "De Mairan's Nebula",        "Nebula",             83.8810, -5.2700, 9.0,  20),
        new("M78",  "M78 Reflection Nebula",     "Nebula",             86.6900,  0.0492, 8.3,   8),

        // ---- Planetary Nebulae ----
        new("M27",  "Dumbbell Nebula",           "Planetary Nebula",  299.9013, 22.7211, 7.4,   8),
        new("M57",  "Ring Nebula",               "Planetary Nebula",  283.3962, 33.0292, 8.8,   1.4),
        new("M97",  "Owl Nebula",                "Planetary Nebula",  168.6996, 55.0192, 9.9,   3),

        // ---- Open Clusters ----
        new("M6",   "Butterfly Cluster",         "Open Cluster",      265.0833,-32.2167, 4.2,  25),
        new("M7",   "Ptolemy Cluster",           "Open Cluster",      268.4500,-34.7833, 3.3,  80),
        new("M11",  "Wild Duck Cluster",         "Open Cluster",      282.7708, -6.2700, 5.8,  14),
        new("M44",  "Beehive Cluster",           "Open Cluster",      130.0250, 19.6669, 3.7,  95),
        new("M45",  "Pleiades",                  "Open Cluster",       56.6017, 24.1136, 1.6, 110),
        new("M67",  "King Cobra Cluster",        "Open Cluster",      132.8458, 11.8133, 6.9,  29),

        // ---- Globular Clusters ----
        new("M2",   "Globular Cluster M2",       "Globular Cluster",  323.3625, -0.8231, 6.5,  16),
        new("M3",   "Globular Cluster M3",       "Globular Cluster",  205.5483, 28.3772, 6.2,  18),
        new("M4",   "Globular Cluster M4",       "Globular Cluster",  245.8967,-26.5258, 5.9,  26),
        new("M5",   "Globular Cluster M5",       "Globular Cluster",  229.6383,  2.0817, 5.6,  17),
        new("M13",  "Hercules Cluster",          "Globular Cluster",  250.4233, 36.4603, 5.8,  20),
        new("M15",  "M15 Globular Cluster",      "Globular Cluster",  322.4929, 12.1670, 6.2,  18),
        new("M22",  "Sagittarius Cluster",       "Globular Cluster",  279.0996,-23.9046, 5.1,  32),
        new("M92",  "M92 Globular Cluster",      "Globular Cluster",  259.2806, 43.1361, 6.4,  14),

        // ---- Non-Messier showpieces ----
        new("NGC 869/884", "Double Cluster (Perseus)", "Open Cluster", 35.5000, 57.1500, 3.7,  60),
        new("NGC 7000",    "North America Nebula",     "Nebula",      314.7333, 44.3500, 4.0, 120),
        new("NGC 4565",    "Needle Galaxy",            "Galaxy",      189.0867, 25.9875, 9.6,  16),
    ];
}
