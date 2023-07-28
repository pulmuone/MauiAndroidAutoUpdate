using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace MauiAndroidAutoUpdate;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureMauiHandlers((handlers) =>
            {
#if ANDROID
                handlers.AddHandler(typeof(Views.AutoUpdateView), typeof(MauiAndroidAutoUpdate.Platforms.Android.Handlers.AutoUpdateHandler));
#endif

#if IOS

#endif
            })
            .ConfigureEssentials(essentials =>
            {
                essentials.UseVersionTracking();
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
