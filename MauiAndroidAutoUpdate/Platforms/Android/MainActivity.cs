using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.AppCompat.App;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Microsoft.Maui.Controls.PlatformConfiguration;

namespace MauiAndroidAutoUpdate;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
    ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
public class MainActivity : MauiAppCompatActivity
{

    readonly int REQUEST_INSTALL_PERMISSION = 10;

    static string[] PERMISSIONS = {
            Manifest.Permission.ReadExternalStorage,
            Manifest.Permission.WriteExternalStorage,
            Manifest.Permission.RequestInstallPackages
        };

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        AppCompatDelegate.DefaultNightMode = AppCompatDelegate.ModeNightNo; //여기서 설정하면 MainActivity 두번 실행되는지 확인.  
        this.Window.AddFlags(Android.Views.WindowManagerFlags.KeepScreenOn);

        string deviceId = Android.Provider.Settings.Secure.GetString(this.ContentResolver, Android.Provider.Settings.Secure.AndroidId);

        //알수 없는 소스 설치 여부 확인
        if (PackageManager.CanRequestPackageInstalls())
        {
            SettingPermission();
        }
        else
        {
            //알수 없는 소스 설치 설정 화면 이동
            var intent = new Intent(Android.Provider.Settings.ActionManageUnknownAppSources, Android.Net.Uri.Parse(string.Format("{0}{1}", "package:", PackageName)));
            StartActivityForResult(intent, REQUEST_INSTALL_PERMISSION);
        }
    }


    protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == REQUEST_INSTALL_PERMISSION)
        {
            SettingPermission();
        }
    }

    private void SettingPermission()
    {
        //if (Build.VERSION.SdkInt >= BuildVersionCodes.M && Build.VERSION.SdkInt <= BuildVersionCodes.P) //23~28
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            var permissionList = new List<string>();

            foreach (var item in PERMISSIONS)
            {
                var result = ContextCompat.CheckSelfPermission(this, item);

                if (result != Permission.Granted)
                {
                    permissionList.Add(item);
                }
            }

            if (permissionList.Count > 0)
            {
                //RequestPermissions하면  OnPause -> OnResume 처리됨.
                ActivityCompat.RequestPermissions(this, permissionList.ToArray(), 0);
            }
        }
    }
}
