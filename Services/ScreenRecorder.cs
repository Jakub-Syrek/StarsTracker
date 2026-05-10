namespace StarsTracker.Services;

/// <summary>
/// Static façade for screen recording. The Android implementation lives in
/// `Platforms/Android/ScreenRecorderService.cs` and dispatches state changes
/// back through the events on this class.
/// </summary>
public static class ScreenRecorder
{
    public static event Action? Started;
    public static event Action<string?>? Stopped;
    public static event Action<string>? Error;

    internal static void NotifyStarted() => Started?.Invoke();
    internal static void NotifyStopped(string? path) => Stopped?.Invoke(path);
    internal static void NotifyError(string message) => Error?.Invoke(message);
}
