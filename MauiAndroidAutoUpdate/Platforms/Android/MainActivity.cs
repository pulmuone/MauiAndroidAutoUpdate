using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace MauiAndroidAutoUpdate;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
    ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
public class MainActivity : MauiAppCompatActivity
{

    static string[] PERMISSIONS = {
            Manifest.Permission.ReadExternalStorage,
            Manifest.Permission.WriteExternalStorage,
            Manifest.Permission.Camera
        };

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        this.Window.AddFlags(Android.Views.WindowManagerFlags.KeepScreenOn);

        string deviceId = Android.Provider.Settings.Secure.GetString(this.ContentResolver, Android.Provider.Settings.Secure.AndroidId);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.M) //23이상부터
        {
            ActivityCompat.RequestPermissions(this, PERMISSIONS, 0);
        }
    }
}
