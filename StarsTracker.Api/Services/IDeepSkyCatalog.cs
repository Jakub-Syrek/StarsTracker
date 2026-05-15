using StarsTracker.Shared.Contracts;

namespace StarsTracker.Api.Services;

public interface IDeepSkyCatalog
{
    IReadOnlyList<DeepSkyObjectDto> All { get; }
}
