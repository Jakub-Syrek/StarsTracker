using StarsTracker.Shared.Contracts;

namespace StarsTracker.Api.Services;

/// <summary>
/// Computes geocentric apparent positions of the Sun, Moon and naked-eye
/// planets for a given UTC instant. Output coordinates are equatorial
/// (RA, Dec) at the same epoch as the input — the same convention the
/// client uses for the star catalogue.
/// </summary>
public interface IPlanetEphemerisService
{
    /// <summary>
    /// Returns one <see cref="PlanetPositionDto"/> per requested body
    /// (Sun, Moon, Mercury, Venus, Mars, Jupiter, Saturn).
    /// </summary>
    IReadOnlyList<PlanetPositionDto> Compute(DateTime utc);
}
