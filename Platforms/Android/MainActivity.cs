using Android.App;
using Android.Content.PM;
using Android.OS;

namespace StarsTracker;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Request camera + location permissions up-front so CameraX never hits a permission wall
        // Only needed on API 23+; RequestPermissions is a no-op on earlier versions via the check below
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
        {
            var perms = new[]
            {
                Android.Manifest.Permission.Camera,
                Android.Manifest.Permission.AccessFineLocation,
                Android.Manifest.Permission.AccessCoarseLocation,
            };
            RequestPermissions(perms, 0);
        }
    }
}
