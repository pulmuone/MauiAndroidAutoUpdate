﻿using Android.App;
using Android.Content;
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
                this.Finish();
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
