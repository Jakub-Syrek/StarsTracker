using StarsTracker.Shared.Contracts;

namespace StarsTracker.Api.Services;

/// <summary>
/// Full naked-eye star catalogue (~5000 entries, mag ≤ 6) generated from
/// HYG v4.1 and embedded as a JSON file in the deployed image.
/// </summary>
public interface IExtendedStarCatalog
{
    IReadOnlyList<StarRecordDto> All { get; }
}
