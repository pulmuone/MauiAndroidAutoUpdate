using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.OS;
using Android.Widget;
using Java.IO;
using Application = Android.App.Application;
using DownloadStatus = Android.App.DownloadStatus;
using Net = Android.Net;
using OS = Android.OS;
using PM = Android.Content.PM;
using ProgressBar = Android.Widget.ProgressBar;

namespace MauiAndroidAutoUpdate.Platforms.Android
{
    [Activity(Label = "자동 업데이트", ScreenOrientation = PM.ScreenOrientation.Portrait)]
    public class AutoUpdateActivity : Activity
    {
        public static event Action OnUpdateCompleted;
        private DownloadReceiver receiver;
        public static long downloadId = -1L;

        private ProgressBar progressBar;
        private TextView textView1;

        private static String PACKAGE_INSTALLED_ACTION = "com.example.android.apis.content.SESSION_API_PACKAGE_INSTALLED";
        public object Content { get; private set; }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.AutoUpdateLayout);

            progressBar = FindViewById<ProgressBar>(Resource.Id.progressBar1);
            textView1 = FindViewById<TextView>(Resource.Id.textView1);

            progressBar.Max = 100;
            textView1.Text = "0 %";

            FileDelete();

            var manager = DownloadManager.FromContext(this);

            var url = new System.Uri(GlobalSetting.Instance.ApkUri);//apk 다운로드 주소

            var request = new DownloadManager.Request(Net.Uri.Parse(url.ToString()));
            request.SetMimeType("application/vnd.android.package-archive");
            request.SetTitle("App Download");
            request.SetDescription("File Downloading...");
            request.SetAllowedNetworkTypes(DownloadNetwork.Wifi | DownloadNetwork.Mobile);
            request.SetNotificationVisibility(DownloadVisibility.Visible);
            request.SetAllowedOverMetered(true);
            request.SetAllowedOverRoaming(true);

            Java.IO.File path = this.GetExternalFilesDir(OS.Environment.DirectoryDownloads);
            request.SetDestinationUri(Net.Uri.FromFile(new Java.IO.File(path, string.Format("{0}{1}", Application.Context.PackageName, ".apk"))));

            downloadId = manager.Enqueue(request);

            ThreadPool.QueueUserWorkItem(async o =>
            {
                var query = new DownloadManager.Query();
                for (; ; )
                {
                    query.SetFilterById(downloadId);
                    ICursor cursor = manager.InvokeQuery(query);
                    if (cursor.MoveToFirst())
                    {
                        var soFar = cursor.GetDouble(cursor.GetColumnIndex(DownloadManager.ColumnBytesDownloadedSoFar));
                        var total = cursor.GetDouble(cursor.GetColumnIndex(DownloadManager.ColumnTotalSizeBytes));

                        RunOnUiThread(() =>
                        {
                            //if (Build.VERSION.SdkInt >= OS.BuildVersionCodes.N)
                            if (OperatingSystem.IsAndroidVersionAtLeast(24))
                            {
                                progressBar.SetProgress(System.Convert.ToInt32(soFar / total * 100), true);
                            }
                            else
                            {
                                progressBar.Progress = System.Convert.ToInt32(soFar / total * 100);
                            }

                            textView1.Text = string.Format("{0} %", Convert.ToInt32(soFar / total * 100));
                        });

                        //System.Console.WriteLine(String.Format("==> {0} {1}", total.ToString(), soFar.ToString()));

                        if (soFar.Equals(total))
                        {
                            break;
                        }
                    }
                    await Task.Delay(200);
                }
            });
        }

        private void Receiver_DownloadCompleted()
        {
            //1. 기존 사용하던 방법, "설치중입니다." 가운데 알림창 표시됨.
            //InstallApk();

            //2. package installer Session 사용, "설치중입니다." 가운데 표시되는 알림창을 보여주지 않고 설치함.
            // progressBar 보여주지 않고 DownloadManager만 사용하여 업데이트된 apk파일 다운로드 되는 것을 보여주지 않고 개발.
            InstallApkSession();
        }

        //https://android.googlesource.com/platform/development/+/refs/heads/main/samples/ApiDemos/src/com/example/android/apis/content/InstallApkSessionApi.java
        private void InstallApkSession()
        {
            Java.IO.File path = this.GetExternalFilesDir(OS.Environment.DirectoryDownloads);

            PackageInstaller.Session session = null;

            try
            {
                PackageInstaller packageInstaller = PackageManager.PackageInstaller;
                PackageInstaller.SessionParams _params = new PackageInstaller.SessionParams(PackageInstallMode.FullInstall);
                _params.SetAppPackageName(AppInfo.Current.PackageName);
                //_params.SetDontKillApp(true); //Android34이상 지원

                int sessionId = packageInstaller.CreateSession(_params);
                session = packageInstaller.OpenSession(sessionId);

                var packageInSession = session.OpenWrite(AppInfo.Current.PackageName, 0, -1);

                var apkUri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                                this.ApplicationContext,
                                string.Format("{0}{1}", AppInfo.Current.PackageName, ".fileprovider"),
                                new Java.IO.File(path, string.Format("{0}{1}", AppInfo.Current.PackageName, ".apk")));

                var input = this.ContentResolver.OpenInputStream(apkUri);

                if (input != null)
                {
                    input.CopyTo(packageInSession);
                }
                //session.Fsync(packageInSession);
                packageInSession.Close();
                input.Close();

                // Create an install status receiver.
                Intent intent = new Intent(this, this.Class);
                intent.SetAction(PACKAGE_INSTALLED_ACTION);
                // SingleTop만 있으면 된다고 하나 아래값 다 추가 해줌.
                intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);

                //https://developer.android.com/topic/security/risks/pending-intent?hl=ko#kotlin
                PendingIntentFlags piFlag = 0;

                if (OperatingSystem.IsAndroidVersionAtLeast(31))
                {
                    piFlag = PendingIntentFlags.Mutable; //31이상에서 필수값 지정해야함. Mutable 아닐경우 업데이트 안 됨.
                }

                PendingIntent pendingIntent = PendingIntent.GetActivity(this, 0, intent, piFlag);
                IntentSender statusReceiver = pendingIntent.IntentSender;

                // commit하면 백그라운드에서 설치작업 진행되고, 완료 되면 앱 강제 종료 됨.
                session.Commit(statusReceiver);
                //https://developer.android.com/reference/android/content/pm/PackageInstaller.Session#commit(android.content.IntentSender)
            }
            catch (Java.IO.IOException ex)
            {
                if (session != null)
                {
                    session.Abandon();
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex.Message);
            }
        }

        //기존 사용하던 방법
        //Android 기본 샘플 https://android.googlesource.com/platform/development/+/refs/heads/main/samples/ApiDemos/src/com/example/android/apis/content/InstallApk.java
        private void InstallApk()
        {
            Java.IO.File path = this.GetExternalFilesDir(OS.Environment.DirectoryDownloads);
            try
            {
                if (Build.VERSION.SdkInt >= OS.BuildVersionCodes.N)
                {
                    Intent intent = new Intent(Intent.ActionView);
                    intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);

                    var apkUri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                                        this.ApplicationContext,
                                        string.Format("{0}{1}", Application.Context.PackageName, ".fileprovider"),
                                        new Java.IO.File(path, string.Format("{0}{1}", Application.Context.PackageName, ".apk")));

                    intent.SetDataAndType(apkUri, this.ContentResolver.GetType(apkUri));
                    StartActivity(intent);
                }
                else
                {
                    Intent intent = new Intent(Intent.ActionView);
                    intent.SetDataAndType(Net.Uri.FromFile(new Java.IO.File(path, string.Format("{0}{1}", Application.Context.PackageName, ".apk")))
                                        , "application/vnd.android.package-archive");
                    intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop); // ActivityFlags.NewTask 이 옵션을 지정해 주어야 업데이트 완료 후에 [열기]라는 화면이 나온다.
                    StartActivity(intent);
                }
            }
            catch (Exception e)
            {
                //System.Console.WriteLine(e.Message);
            }
            finally
            {
                //await Task.Delay(500);
                //this.Finish();
            }
        }


        protected override void OnResume()
        {
            base.OnResume();

            // Registers BroadcastReceiver to respond to completed downloads.
            var filter = new IntentFilter(DownloadManager.ActionDownloadComplete);
            filter.AddCategory(Intent.CategoryDefault);

            receiver = new DownloadReceiver();
            receiver.DownloadCompleted += Receiver_DownloadCompleted;

            //if (Build.VERSION.SdkInt >= OS.BuildVersionCodes.Tiramisu)
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                RegisterReceiver(receiver, filter, ReceiverFlags.Exported);
            }
            else
            {
                RegisterReceiver(receiver, filter);
            }
        }

        protected override void OnPause()
        {
            base.OnPause();

            // Unregister the BroadcastReceiver when app is destroyed.
            if (receiver != null)
            {
                receiver.DownloadCompleted -= Receiver_DownloadCompleted;
                UnregisterReceiver(receiver);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            OnUpdateCompleted?.Invoke();
            OnUpdateCompleted = null;
        }
        private void FileDelete()
        {
            Java.IO.File file = this.GetExternalFilesDir(OS.Environment.DirectoryDownloads);
            IFilenameFilter filter = new AutoUpdateFileFilter();
            Java.IO.File[] files = file.ListFiles(filter);

            if (files != null && files.Length > 0)
            {
                foreach (var item in files)
                {
                    if (item.IsFile)
                    {
                        if (item.Name.ToString().StartsWith(Application.Context.PackageName))
                        {
                            using (Java.IO.File fileDel = new Java.IO.File(item.ToString()))
                            {
                                if (fileDel.Exists())
                                {
                                    fileDel.Delete();
                                }
                            }
                        }
                    }
                }
            }
        }

        //InstallApkSession 사용
        protected override void OnNewIntent(Intent intent)
        {
            Bundle extras = intent.Extras;
            if (PACKAGE_INSTALLED_ACTION.Equals(intent.Action))
            {
                var status = extras.GetInt(PackageInstaller.ExtraStatus);
                var message = extras.GetString(PackageInstaller.ExtraStatusMessage);
                switch (status)
                {
                    case (int)PackageInstallStatus.PendingUserAction:
                        // Ask user to confirm the installation
                        var confirmIntent = (Intent)extras.Get(Intent.ExtraIntent);
                        StartActivity(confirmIntent);
                        break;
                    case (int)PackageInstallStatus.Success:
                        //TODO: Handle success
                        break;
                    case (int)PackageInstallStatus.Failure:
                    case (int)PackageInstallStatus.FailureAborted:
                    case (int)PackageInstallStatus.FailureBlocked:
                    case (int)PackageInstallStatus.FailureConflict:
                    case (int)PackageInstallStatus.FailureIncompatible:
                    case (int)PackageInstallStatus.FailureInvalid:
                    case (int)PackageInstallStatus.FailureStorage:
                        //TODO: Handle failures
                        break;
                }
            }
        }

        public delegate void DownloadCompletedEventHandler();

        //java.lang.SecurityException 버그 Issue 처리
        [BroadcastReceiver(Exported = true)] // Specify the export flag
        private class DownloadReceiver : BroadcastReceiver
        {
            public event DownloadCompletedEventHandler DownloadCompleted;

            public DownloadReceiver()
            {

            }

            public override void OnReceive(Context context, Intent intent)
            {
                long id = intent.GetLongExtra(DownloadManager.ExtraDownloadId, -1);

                //System.Console.WriteLine(string.Format("Received intent for {0}:\n", id));

                if (DownloadManager.ActionDownloadComplete.Equals(intent.Action))
                {
                    if (downloadId == id)
                    {
                        var manager = DownloadManager.FromContext(context);
                        var query = new DownloadManager.Query();
                        query.SetFilterById(id);
                        ICursor cursor = manager.InvokeQuery(query);
                        if (cursor.MoveToFirst())
                        {
                            // get the status
                            var columnIndex = cursor.GetColumnIndex(DownloadManager.ColumnStatus);
                            var status = (DownloadStatus)cursor.GetInt(columnIndex);

                            //System.Console.WriteLine(string.Format("  Received status {0}\n", status));

                            if (status == DownloadStatus.Successful)
                            {
                                DownloadCompleted?.Invoke();
                            }
                        }
                    }
                }
            }
        }
    }

    public class AutoUpdateFileFilter : Java.Lang.Object, Java.IO.IFilenameFilter
    {
        public bool Accept(Java.IO.File dir, string name)
        {
            if (name.StartsWith(Application.Context.PackageName) && name.EndsWith(".apk"))
            {
                return true;
            }
            return false;
        }
    }
}
