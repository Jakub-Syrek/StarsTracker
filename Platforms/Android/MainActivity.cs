using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media.Projection;
using Android.OS;
using AndroidX.Core.Content;
using StarsTracker.Platforms.Android;
using StarsTracker.Services;

namespace StarsTracker;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int RequestCodeMediaProjection = 4242;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
        {
            var perms = new System.Collections.Generic.List<string>
            {
                Android.Manifest.Permission.Camera,
                Android.Manifest.Permission.AccessFineLocation,
                Android.Manifest.Permission.AccessCoarseLocation,
            };
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
            {
                perms.Add(Android.Manifest.Permission.PostNotifications);
            }
            RequestPermissions(perms.ToArray(), 0);
        }
    }

    /// <summary>
    /// Triggers the Android system "allow screen capture" dialog, then starts
    /// the foreground service to do the recording. Called from .NET via
    /// <see cref="ScreenRecorderHost"/>.
    /// </summary>
    public void StartScreenRecording()
    {
        var mpm = (MediaProjectionManager?)GetSystemService(MediaProjectionService);
        if (mpm is null)
        {
            ScreenRecorder.NotifyError("MediaProjectionManager unavailable");
            return;
        }
        StartActivityForResult(mpm.CreateScreenCaptureIntent(), RequestCodeMediaProjection);
    }

    public void StopScreenRecording()
    {
        var intent = new Intent(this, typeof(ScreenRecorderService))
            .SetAction(ScreenRecorderService.ActionStop);
        StartService(intent);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != RequestCodeMediaProjection) return;

        if (resultCode != Result.Ok || data is null)
        {
            ScreenRecorder.NotifyError("User denied screen recording permission");
            return;
        }

        var intent = new Intent(this, typeof(ScreenRecorderService))
            .SetAction(ScreenRecorderService.ActionStart)
            .PutExtra(ScreenRecorderService.ExtraResultCode, (int)resultCode)
            .PutExtra(ScreenRecorderService.ExtraResultData, data);

        ContextCompat.StartForegroundService(this, intent);
    }
}
