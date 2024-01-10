using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Database;
using Java.IO;
using Application = Android.App.Application;
using DownloadStatus = Android.App.DownloadStatus;
using Net = Android.Net;
using OS = Android.OS;

namespace MauiAndroidAutoUpdate.Services
{
    public partial class InstallApkSessionApiService
    {
        private DownloadReceiver2 receiver;
        public static long downloadId = -1L;
        private static String PACKAGE_INSTALLED_ACTION = "com.example.android.apis.content.SESSION_API_PACKAGE_INSTALLED";

        public partial void InstallApkSessionApi()
        {
            var context = Platform.AppContext;

            FileDelete();

            var manager = DownloadManager.FromContext(context);
            var url = new System.Uri(GlobalSetting.Instance.ApkUri);//apk 다운로드 주소

            var request = new DownloadManager.Request(Net.Uri.Parse(url.ToString()));
            request.SetMimeType("application/vnd.android.package-archive");
            request.SetTitle("App Download");
            request.SetDescription("File Downloading...");
            request.SetAllowedNetworkTypes(DownloadNetwork.Wifi | DownloadNetwork.Mobile);
            request.SetNotificationVisibility(DownloadVisibility.Visible);
            request.SetAllowedOverMetered(true);
            request.SetAllowedOverRoaming(true);

            Java.IO.File path = context.GetExternalFilesDir(OS.Environment.DirectoryDownloads);
            request.SetDestinationUri(Net.Uri.FromFile(new Java.IO.File(path, string.Format("{0}{1}", Application.Context.PackageName, ".apk"))));

            downloadId = manager.Enqueue(request);



            // Registers BroadcastReceiver to respond to completed downloads.
            var filter = new IntentFilter(DownloadManager.ActionDownloadComplete);
            filter.AddCategory(Intent.CategoryDefault);

            receiver = new DownloadReceiver2();
            receiver.DownloadCompleted += Receiver_DownloadCompleted;

            //if (Build.VERSION.SdkInt >= OS.BuildVersionCodes.Tiramisu)
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                Platform.AppContext.RegisterReceiver(receiver, filter, ReceiverFlags.Exported);
            }
            else
            {
                Platform.AppContext.RegisterReceiver(receiver, filter);
            }
        }

        private void Receiver_DownloadCompleted()
        {
            Java.IO.File path = Platform.AppContext.GetExternalFilesDir(OS.Environment.DirectoryDownloads);

            PackageInstaller.Session session = null;

            try
            {
                PackageInstaller packageInstaller = Platform.AppContext.PackageManager.PackageInstaller;
                PackageInstaller.SessionParams _params = new PackageInstaller.SessionParams(PackageInstallMode.FullInstall);
                _params.SetAppPackageName(AppInfo.Current.PackageName);
                //_params.SetDontKillApp(true); //Android34이상 지원

                int sessionId = packageInstaller.CreateSession(_params);
                session = packageInstaller.OpenSession(sessionId);

                var packageInSession = session.OpenWrite(AppInfo.Current.PackageName, 0, -1);

                var apkUri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                                Platform.AppContext,
                                string.Format("{0}{1}", AppInfo.Current.PackageName, ".fileprovider"),
                                new Java.IO.File(path, string.Format("{0}{1}", AppInfo.Current.PackageName, ".apk")));

                var input = Platform.AppContext.ContentResolver.OpenInputStream(apkUri);

                if (input != null)
                {
                    input.CopyTo(packageInSession);
                }
                //session.Fsync(packageInSession);
                packageInSession.Close();
                input.Close();


                //Create an install status receiver.
                //Intent intent = new Intent(Platform.AppContext, Platform.AppContext.Class);
                //intent.SetAction(PACKAGE_INSTALLED_ACTION);
                // SingleTop만 있으면 된다고 하나 아래값 다 추가 해줌.
                //intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);

                //https://developer.android.com/topic/security/risks/pending-intent?hl=ko#kotlin
                PendingIntentFlags piFlag = 0;

                if (OperatingSystem.IsAndroidVersionAtLeast(31))
                {
                    piFlag = PendingIntentFlags.Mutable; //31이상에서 필수값 지정해야함. Mutable 아닐경우 업데이트 안 됨.
                }

                PendingIntent pendingIntent = PendingIntent.GetActivity(Platform.AppContext, 0, new Intent(PACKAGE_INSTALLED_ACTION), piFlag); ;
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

        private void FileDelete()
        {
            Java.IO.File file = Platform.AppContext.GetExternalFilesDir(OS.Environment.DirectoryDownloads);
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



        public delegate void DownloadCompletedEventHandler();

        //java.lang.SecurityException 버그 Issue 처리
        [BroadcastReceiver(Exported = true)] // Specify the export flag
        private class DownloadReceiver2 : BroadcastReceiver
        {
            public event DownloadCompletedEventHandler DownloadCompleted;

            public DownloadReceiver2()
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
