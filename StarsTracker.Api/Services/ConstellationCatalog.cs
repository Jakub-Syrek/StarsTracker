using StarsTracker.Shared.Contracts;

namespace StarsTracker.Api.Services;

/// <summary>
/// Hand-curated IAU stick-figure lines for the most recognisable
/// constellations visible from Northern Hemisphere mid-latitudes. Star
/// names match the proper names shipped in the client's <c>stars.json</c>.
/// When the client cannot resolve a name, the segment is silently skipped.
/// </summary>
public sealed class ConstellationCatalog : IConstellationCatalog
{
    public IReadOnlyList<ConstellationDto> All { get; } =
    [
        new("Ori", "Orion",
        [
            new("Betelgeuse", "Bellatrix"),
            new("Bellatrix",  "Mintaka"),
            new("Mintaka",    "Alnilam"),
            new("Alnilam",    "Alnitak"),
            new("Alnitak",    "Saiph"),
            new("Saiph",      "Rigel"),
            new("Rigel",      "Mintaka"),
            new("Betelgeuse", "Alnitak"),
        ]),
        new("UMa", "Ursa Major",
        [
            new("Alkaid",  "Mizar"),
            new("Mizar",   "Alioth"),
            new("Alioth",  "Megrez"),
            new("Megrez",  "Phecda"),
            new("Phecda",  "Merak"),
            new("Merak",   "Dubhe"),
            new("Dubhe",   "Megrez"),
        ]),
        new("UMi", "Ursa Minor",
        [
            new("Polaris",   "Yildun"),
            new("Yildun",    "Epsilon UMi"),
            new("Epsilon UMi", "Zeta UMi"),
            new("Zeta UMi",  "Eta UMi"),
            new("Eta UMi",   "Pherkad"),
            new("Pherkad",   "Kochab"),
            new("Kochab",    "Zeta UMi"),
        ]),
        new("Cas", "Cassiopeia",
        [
            new("Caph",    "Schedar"),
            new("Schedar", "Gamma Cas"),
            new("Gamma Cas", "Ruchbah"),
            new("Ruchbah", "Segin"),
        ]),
        new("Cyg", "Cygnus",
        [
            new("Deneb",   "Sadr"),
            new("Sadr",    "Albireo"),
            new("Sadr",    "Gienah Cygni"),
            new("Sadr",    "Fawaris"),
        ]),
        new("Lyr", "Lyra",
        [
            new("Vega",       "Sheliak"),
            new("Sheliak",    "Sulafat"),
            new("Sulafat",    "Vega"),
        ]),
        new("Aql", "Aquila",
        [
            new("Altair",   "Tarazed"),
            new("Altair",   "Alshain"),
            new("Tarazed",  "Deneb el Okab"),
            new("Alshain",  "Theta Aql"),
        ]),
        new("Boo", "Bootes",
        [
            new("Arcturus", "Izar"),
            new("Izar",     "Seginus"),
            new("Seginus",  "Nekkar"),
            new("Nekkar",   "Izar"),
        ]),
        new("Leo", "Leo",
        [
            new("Regulus",   "Algieba"),
            new("Algieba",   "Adhafera"),
            new("Adhafera",  "Ras Elased Australis"),
            new("Regulus",   "Denebola"),
            new("Denebola",  "Zosma"),
            new("Zosma",     "Chertan"),
            new("Chertan",   "Regulus"),
        ]),
        new("Sco", "Scorpius",
        [
            new("Antares",     "Acrab"),
            new("Acrab",       "Dschubba"),
            new("Antares",     "Larawag"),
            new("Larawag",     "Sargas"),
            new("Sargas",      "Shaula"),
        ]),
        new("Tau", "Taurus",
        [
            new("Aldebaran", "Elnath"),
            new("Aldebaran", "Ain"),
        ]),
        new("Gem", "Gemini",
        [
            new("Castor",  "Pollux"),
            new("Castor",  "Tejat"),
            new("Pollux",  "Wasat"),
            new("Wasat",   "Alhena"),
        ]),
        new("Per", "Perseus",
        [
            new("Mirfak", "Algol"),
            new("Mirfak", "Atik"),
            new("Algol",  "Misam"),
        ]),
        new("And", "Andromeda",
        [
            new("Alpheratz", "Mirach"),
            new("Mirach",    "Almach"),
        ]),
        new("Peg", "Pegasus",
        [
            new("Markab",   "Scheat"),
            new("Scheat",   "Alpheratz"),
            new("Alpheratz","Algenib"),
            new("Algenib",  "Markab"),
            new("Markab",   "Enif"),
        ]),
        new("CMa", "Canis Major",
        [
            new("Sirius",  "Mirzam"),
            new("Sirius",  "Adhara"),
            new("Adhara",  "Wezen"),
            new("Wezen",   "Aludra"),
        ]),
        new("CMi", "Canis Minor",
        [
            new("Procyon", "Gomeisa"),
        ]),
        new("Aur", "Auriga",
        [
            new("Capella",  "Menkalinan"),
            new("Menkalinan","Hassaleh"),
            new("Hassaleh", "Elnath"),
        ]),
        new("Vir", "Virgo",
        [
            new("Spica",     "Heze"),
            new("Heze",      "Vindemiatrix"),
            new("Vindemiatrix","Porrima"),
        ]),
    ];
}
