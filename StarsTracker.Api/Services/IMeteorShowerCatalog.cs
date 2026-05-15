using StarsTracker.Shared.Contracts;

namespace StarsTracker.Api.Services;

public interface IMeteorShowerCatalog
{
    /// <summary>
    /// Returns the showers whose peak is within ±7 days of the supplied UTC.
    /// </summary>
    IReadOnlyList<MeteorShowerDto> ActiveAround(DateTime utc);
}
