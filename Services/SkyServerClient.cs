using System.Net.Http.Json;
using StarsTracker.Shared.Contracts;

namespace StarsTracker.Services;

/// <summary>
/// Thin HTTP client for the StarsTracker.Api server. Wraps the planets and
/// constellations endpoints in async methods and caches the last successful
/// payload in app-local storage so the overlay keeps working offline.
/// </summary>
public sealed class SkyServerClient
{
    /// <summary>
    /// Production base URL — Railway deployment. Override at build time
    /// with <c>STARSTRACKER_API_BASE</c> environment variable during local
    /// development against a localhost server.
    /// </summary>
    public const string DefaultBaseUrl = "https://starstracker-production.up.railway.app";

    private static readonly TimeSpan PlanetCacheTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ConstellationCacheTtl = TimeSpan.FromDays(7);
    private static readonly TimeSpan DeepSkyCacheTtl = TimeSpan.FromDays(7);
    private static readonly TimeSpan MeteorCacheTtl = TimeSpan.FromHours(6);

    private readonly HttpClient _http;
    private readonly string _planetCachePath;
    private readonly string _constellationCachePath;
    private readonly string _deepSkyCachePath;
    private readonly string _meteorCachePath;

    public SkyServerClient(HttpClient http)
    {
        _http = http;
        if (_http.BaseAddress is null)
        {
            var configured = Environment.GetEnvironmentVariable("STARSTRACKER_API_BASE");
            _http.BaseAddress = new Uri(string.IsNullOrWhiteSpace(configured)
                ? DefaultBaseUrl
                : configured);
        }
        _http.Timeout = TimeSpan.FromSeconds(8);

        string cacheDir = Path.Combine(FileSystem.AppDataDirectory, "sky-cache");
        Directory.CreateDirectory(cacheDir);
        _planetCachePath = Path.Combine(cacheDir, "planets.json");
        _constellationCachePath = Path.Combine(cacheDir, "constellations.json");
        _deepSkyCachePath = Path.Combine(cacheDir, "deepsky.json");
        _meteorCachePath = Path.Combine(cacheDir, "meteors.json");
    }

    /// <summary>
    /// Fetches the planets payload for the supplied UTC. Falls back to the
    /// on-disk cache (if fresh) and then to whatever cached payload exists
    /// (even if stale) when the network is unreachable.
    /// </summary>
    public async Task<IReadOnlyList<PlanetPositionDto>?> GetPlanetsAsync(DateTime utc, CancellationToken ct = default)
    {
        if (TryReadCache<PlanetsResponse>(_planetCachePath, PlanetCacheTtl, out var fresh))
        {
            Log($"planets: cache hit ({fresh!.Bodies.Count} bodies)");
            return fresh!.Bodies;
        }

        try
        {
            string url = $"/api/v1/planets?utc={utc:O}";
            Log($"planets: GET {_http.BaseAddress}{url.TrimStart('/')}");
            var response = await _http.GetFromJsonAsync<PlanetsResponse>(url, ct);
            if (response is not null)
            {
                WriteCache(_planetCachePath, response);
                Log($"planets: OK ({response.Bodies.Count} bodies)");
                return response.Bodies;
            }
            Log("planets: null response body");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Log($"planets: network error — {ex.GetType().Name}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Log($"planets: unexpected error — {ex.GetType().Name}: {ex.Message}");
        }

        if (TryReadCache<PlanetsResponse>(_planetCachePath, TimeSpan.MaxValue, out var stale))
        {
            Log($"planets: stale cache ({stale!.Bodies.Count} bodies)");
            return stale!.Bodies;
        }
        Log("planets: no data");
        return null;
    }

    /// <summary>
    /// Fetches the constellation stick figures. The data is static, so we
    /// cache aggressively (7-day TTL) and reuse stale on offline.
    /// </summary>
    public async Task<IReadOnlyList<ConstellationDto>?> GetConstellationsAsync(CancellationToken ct = default)
    {
        if (TryReadCache<List<ConstellationDto>>(_constellationCachePath, ConstellationCacheTtl, out var fresh))
        {
            Log($"constellations: cache hit ({fresh!.Count})");
            return fresh!;
        }

        try
        {
            Log($"constellations: GET {_http.BaseAddress}api/v1/constellations");
            var response = await _http.GetFromJsonAsync<List<ConstellationDto>>("/api/v1/constellations", ct);
            if (response is not null)
            {
                WriteCache(_constellationCachePath, response);
                Log($"constellations: OK ({response.Count})");
                return response;
            }
            Log("constellations: null response body");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Log($"constellations: network error — {ex.GetType().Name}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Log($"constellations: unexpected error — {ex.GetType().Name}: {ex.Message}");
        }

        if (TryReadCache<List<ConstellationDto>>(_constellationCachePath, TimeSpan.MaxValue, out var stale))
        {
            Log($"constellations: stale cache ({stale!.Count})");
            return stale!;
        }
        Log("constellations: no data");
        return null;
    }

    /// <summary>
    /// Fetches the Messier-plus showpieces catalogue. Long-lived cache —
    /// the data is editorial and changes only when the server ships a
    /// new release.
    /// </summary>
    public async Task<IReadOnlyList<DeepSkyObjectDto>?> GetDeepSkyAsync(CancellationToken ct = default)
    {
        if (TryReadCache<List<DeepSkyObjectDto>>(_deepSkyCachePath, DeepSkyCacheTtl, out var fresh))
        {
            Log($"deepsky: cache hit ({fresh!.Count})");
            return fresh!;
        }

        try
        {
            Log($"deepsky: GET {_http.BaseAddress}api/v1/deepsky");
            var response = await _http.GetFromJsonAsync<List<DeepSkyObjectDto>>("/api/v1/deepsky", ct);
            if (response is not null)
            {
                WriteCache(_deepSkyCachePath, response);
                Log($"deepsky: OK ({response.Count})");
                return response;
            }
            Log("deepsky: null response body");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Log($"deepsky: network error — {ex.Message}");
        }
        catch (Exception ex)
        {
            Log($"deepsky: unexpected error — {ex.GetType().Name}: {ex.Message}");
        }

        return TryReadCache<List<DeepSkyObjectDto>>(_deepSkyCachePath, TimeSpan.MaxValue, out var stale)
            ? stale!
            : null;
    }

    /// <summary>
    /// Fetches the meteor showers active around the supplied UTC. Cached
    /// for 6 hours — short enough that newly-entering showers appear without
    /// a fresh install, long enough not to hammer the API.
    /// </summary>
    public async Task<IReadOnlyList<MeteorShowerDto>?> GetMeteorShowersAsync(DateTime utc, CancellationToken ct = default)
    {
        if (TryReadCache<List<MeteorShowerDto>>(_meteorCachePath, MeteorCacheTtl, out var fresh))
        {
            Log($"meteors: cache hit ({fresh!.Count})");
            return fresh!;
        }

        try
        {
            string url = $"/api/v1/meteorshowers?utc={utc:O}";
            Log($"meteors: GET {_http.BaseAddress}{url.TrimStart('/')}");
            var response = await _http.GetFromJsonAsync<List<MeteorShowerDto>>(url, ct);
            if (response is not null)
            {
                WriteCache(_meteorCachePath, response);
                Log($"meteors: OK ({response.Count})");
                return response;
            }
            Log("meteors: null response body");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Log($"meteors: network error — {ex.Message}");
        }
        catch (Exception ex)
        {
            Log($"meteors: unexpected error — {ex.GetType().Name}: {ex.Message}");
        }

        return TryReadCache<List<MeteorShowerDto>>(_meteorCachePath, TimeSpan.MaxValue, out var stale)
            ? stale!
            : null;
    }

    private static void Log(string msg)
    {
#if ANDROID
        Android.Util.Log.Debug("StarsTracker", $"SkyServer {msg}");
#endif
    }

    private static bool TryReadCache<T>(string path, TimeSpan ttl, out T? value)
    {
        value = default;
        if (!File.Exists(path)) return false;
        if (ttl != TimeSpan.MaxValue && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > ttl) return false;
        try
        {
            using var stream = File.OpenRead(path);
            value = System.Text.Json.JsonSerializer.Deserialize<T>(stream);
            return value is not null;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteCache<T>(string path, T value)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            File.WriteAllText(path, json);
        }
        catch
        {
            // Cache writes are best-effort.
        }
    }

    private sealed record PlanetsResponse(DateTime Utc, IReadOnlyList<PlanetPositionDto> Bodies);
}
