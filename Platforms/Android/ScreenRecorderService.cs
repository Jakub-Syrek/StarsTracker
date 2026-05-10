using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Display;
using Android.Media;
using Android.Media.Projection;
using Android.OS;
using Android.Util;
using Android.Views;
using AndroidX.Core.App;
using StarsTracker.Services;

namespace StarsTracker.Platforms.Android;

[Service(
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeMediaProjection)]
public sealed class ScreenRecorderService : Service
{
    public const string ActionStart = "com.jakubsyrek.starstracker.START_RECORDING";
    public const string ActionStop = "com.jakubsyrek.starstracker.STOP_RECORDING";
    public const string ExtraResultCode = "result_code";
    public const string ExtraResultData = "result_data";

    private const string ChannelId = "stars_tracker_recording";
    private const int NotificationId = 4242;
    private const string Tag = "StarsTracker";

    private MediaProjection? _projection;
    private MediaRecorder? _recorder;
    private VirtualDisplay? _virtualDisplay;
    private string? _outputPath;
    private Surface? _inputSurface;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionStart)
        {
            int resultCode = intent.GetIntExtra(ExtraResultCode, (int)Result.Canceled);
            Intent? data = intent.GetParcelableExtra(ExtraResultData) as Intent;
            EnsureNotificationChannel();
            StartForeground(NotificationId, BuildNotification());
            try
            {
                StartRecording(resultCode, data);
                ScreenRecorder.NotifyStarted();
            }
            catch (Java.Lang.Exception ex)
            {
                Log.Error(Tag, $"StartRecording failed: {ex.Message}");
                ScreenRecorder.NotifyError(ex.Message ?? "unknown");
                StopForeground(StopForegroundFlags.Remove);
                StopSelf();
            }
        }
        else if (intent?.Action == ActionStop)
        {
            string? path = StopRecording();
            ScreenRecorder.NotifyStopped(path);
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
        }

        return StartCommandResult.NotSticky;
    }

    private void StartRecording(int resultCode, Intent? data)
    {
        if (data is null) throw new Java.Lang.IllegalArgumentException("missing projection data");

        var mpm = (MediaProjectionManager?)GetSystemService(MediaProjectionService)
                  ?? throw new Java.Lang.IllegalStateException("MediaProjectionManager unavailable");
        _projection = mpm.GetMediaProjection(resultCode, data)
                      ?? throw new Java.Lang.IllegalStateException("GetMediaProjection returned null");

        // MediaProjection requires a Callback registered before any virtual display is created (Android 14+)
        _projection.RegisterCallback(new ProjectionCallback(this), null);

        var metrics = Resources!.DisplayMetrics!;
        int width = metrics.WidthPixels;
        int height = metrics.HeightPixels;
        int density = (int)metrics.DensityDpi;

        // Cap resolution to keep file size sane on phones with QHD/4K displays
        const int maxLong = 1920;
        if (Math.Max(width, height) > maxLong)
        {
            double scale = (double)maxLong / Math.Max(width, height);
            width = (int)(width * scale) & ~1;
            height = (int)(height * scale) & ~1;
        }

        _outputPath = NewOutputPath();
        _recorder = OperatingSystem.IsAndroidVersionAtLeast(31)
            ? new MediaRecorder(this)
            : new MediaRecorder();

        _recorder.SetVideoSource(VideoSource.Surface);
        _recorder.SetOutputFormat(OutputFormat.Mpeg4);
        _recorder.SetVideoEncoder(VideoEncoder.H264);
        _recorder.SetVideoSize(width, height);
        _recorder.SetVideoFrameRate(30);
        _recorder.SetVideoEncodingBitRate(6_000_000);
        _recorder.SetOutputFile(_outputPath);
        _recorder.Prepare();

        _inputSurface = _recorder.Surface;
        _virtualDisplay = _projection.CreateVirtualDisplay(
            "StarsTrackerScreen",
            width, height, density,
            DisplayFlags.Presentation,
            _inputSurface, callback: null, handler: null);

        _recorder.Start();
        Log.Info(Tag, $"Recording started → {_outputPath} @ {width}x{height}");
    }

    private string? StopRecording()
    {
        try { _recorder?.Stop(); }
        catch (Java.Lang.Exception ex) { Log.Warn(Tag, $"recorder.Stop: {ex.Message}"); }

        try { _recorder?.Reset(); } catch { }
        _recorder?.Release();
        _recorder = null;

        _virtualDisplay?.Release();
        _virtualDisplay = null;

        try { _projection?.Stop(); } catch { }
        _projection = null;

        _inputSurface?.Release();
        _inputSurface = null;

        if (_outputPath is not null)
        {
            PublishToGallery(_outputPath);
            Log.Info(Tag, $"Recording stopped → {_outputPath}");
        }
        return _outputPath;
    }

    private static string NewOutputPath()
    {
        var dir = global::Android.OS.Environment.GetExternalStoragePublicDirectory(
            global::Android.OS.Environment.DirectoryMovies);
        if (dir is null || !dir.Exists()) dir?.Mkdirs();

        string subdir = System.IO.Path.Combine(dir!.AbsolutePath, "StarsTracker");
        System.IO.Directory.CreateDirectory(subdir);

        string filename = $"StarsTracker_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
        return System.IO.Path.Combine(subdir, filename);
    }

    private void PublishToGallery(string path)
    {
        try
        {
            var values = new ContentValues();
            values.Put(global::Android.Provider.MediaStore.IMediaColumns.DisplayName, System.IO.Path.GetFileName(path));
            values.Put(global::Android.Provider.MediaStore.IMediaColumns.MimeType, "video/mp4");
            values.Put(global::Android.Provider.MediaStore.IMediaColumns.RelativePath,
                System.IO.Path.Combine(global::Android.OS.Environment.DirectoryMovies, "StarsTracker"));

            var uri = ContentResolver?.Insert(global::Android.Provider.MediaStore.Video.Media.ExternalContentUri!, values);
            if (uri is null) return;

            using var output = ContentResolver?.OpenOutputStream(uri);
            using var input = System.IO.File.OpenRead(path);
            if (output is not null) input.CopyTo(output);
        }
        catch (Java.Lang.Exception ex) { Log.Warn(Tag, $"PublishToGallery: {ex.Message}"); }
        catch (System.Exception ex) { Log.Warn(Tag, $"PublishToGallery (sys): {ex.Message}"); }
    }

    private void EnsureNotificationChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
        var nm = (NotificationManager?)GetSystemService(NotificationService);
        if (nm?.GetNotificationChannel(ChannelId) != null) return;

        var channel = new NotificationChannel(ChannelId, "Nagrywanie ekranu", NotificationImportance.Low)
        {
            Description = "Pokazywane podczas nagrywania ekranu w StarsTracker"
        };
        nm?.CreateNotificationChannel(channel);
    }

    private Notification BuildNotification()
    {
        return new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("StarsTracker")
            .SetContentText("Nagrywanie ekranu...")
            .SetSmallIcon(global::Android.Resource.Drawable.PresenceVideoOnline)
            .SetOngoing(true)
            .Build();
    }

    private sealed class ProjectionCallback : MediaProjection.Callback
    {
        private readonly ScreenRecorderService _service;
        public ProjectionCallback(ScreenRecorderService service) { _service = service; }
        public override void OnStop()
        {
            base.OnStop();
            // User cancelled the projection from the system UI
            _service.StopRecording();
            ScreenRecorder.NotifyStopped(null);
            _service.StopForeground(StopForegroundFlags.Remove);
            _service.StopSelf();
        }
    }
}
