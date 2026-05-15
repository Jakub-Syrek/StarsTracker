using StarsTracker.Shared.Contracts;

namespace StarsTracker.Api.Services;

/// <summary>Catalogue of IAU constellation stick-figure lines.</summary>
public interface IConstellationCatalog
{
    IReadOnlyList<ConstellationDto> All { get; }
}
