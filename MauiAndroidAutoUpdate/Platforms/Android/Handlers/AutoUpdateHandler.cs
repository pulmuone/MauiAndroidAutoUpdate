using Android.Content;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Application = Android.App.Application;

namespace MauiAndroidAutoUpdate.Platforms.Android.Handlers
{
    public class AutoUpdateHandler : PageHandler
    {
        //#1, 
        public AutoUpdateHandler(Context context) : base()
        {

        }

        //#2,
        public override void SetVirtualView(IView view)
        {
            base.SetVirtualView(view);
        }

        //#3,
        protected override ContentViewGroup CreatePlatformView()
        {
            return new ContentViewGroup(base.Context);
        }

        //#4, 네이티브 뷰 설정
        protected override void ConnectHandler(ContentViewGroup platformView)
        {
            base.ConnectHandler(platformView);
            // Perform any control setup here

            var activity = this.Context;
            var intent = new Intent(activity, typeof(AutoUpdateActivity));
            intent.SetPackage(Application.Context.PackageName);

            try
            {
                AutoUpdateActivity.OnUpdateCompleted += () =>
                {
                    if (VirtualView != null)
                    {
                        (VirtualView as ContentPage).Navigation.PopAsync(); // InstallApkSession 방식을 사용할 경우 주석처리
                    }
                };

                activity.StartActivity(intent);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        //NET Maui would never internally call this you need to!!!
        protected override void DisconnectHandler(ContentViewGroup platformView)
        {
            try
            {
                base.DisconnectHandler(platformView); //base를 먼저 호출한다.
                platformView?.Dispose();
            }
            catch (Exception ex)
            {

            }
        }

    }
}
