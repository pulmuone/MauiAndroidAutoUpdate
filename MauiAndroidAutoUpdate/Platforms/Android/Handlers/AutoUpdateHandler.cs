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

            var activity = this.Context;
            var intent = new Intent(activity, typeof(AutoUpdateActivity));
            intent.SetPackage(Application.Context.PackageName);

            try
            {
                AutoUpdateActivity.OnUpdateCompleted += () =>
                {
                    (view as ContentPage).Navigation.PopAsync();
                };

                activity.StartActivity(intent);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        //#3,
        protected override ContentViewGroup CreatePlatformView()
        {
            return new ContentViewGroup(base.Context);
        }

        //#4, 네이티브 뷰 설정
        protected override void ConnectHandler(ContentViewGroup platformView)
        {
            base.ConnectHandler(PlatformView);
            // Perform any control setup here

        }

        protected override void DisconnectHandler(ContentViewGroup platformView)
        {
            platformView.Dispose();
            base.DisconnectHandler(platformView);
        }

    }
}
