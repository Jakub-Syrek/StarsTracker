namespace StarsTracker.Shared.Contracts;

/// <summary>
/// A single IAU constellation with stick-figure lines defined by pairs of
/// HIP / proper star names that the client looks up in its own star catalog.
/// </summary>
/// <param name="Abbreviation">Three-letter IAU code, e.g. "Ori" for Orion.</param>
/// <param name="Name">Full English name, e.g. "Orion".</param>
/// <param name="Lines">
/// Stick-figure segments. Each <see cref="StarLineDto"/> connects two stars
/// by their proper names as they appear in the client-side <c>stars.json</c>.
/// </param>
public sealed record ConstellationDto(
    string Abbreviation,
    string Name,
    IReadOnlyList<StarLineDto> Lines);

/// <summary>
/// A single line segment between two named stars in a constellation
/// stick figure. The client resolves names to projected screen coordinates.
/// </summary>
public sealed record StarLineDto(string FromStar, string ToStar);
