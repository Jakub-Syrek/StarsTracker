using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using StarsTracker.Services;
using StarsTracker.ViewModels;
using StarsTracker.Views;

namespace StarsTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitCamera()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Core services
        builder.Services.AddSingleton<StarCatalogService>();
        builder.Services.AddSingleton<OrientationService>();
        builder.Services.AddSingleton<LandmarkService>();

        // Sky-server API client (planets, constellations).
        builder.Services.AddHttpClient<SkyServerClient>();

        // ViewModel & Page
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<MainPage>(sp =>
            new MainPage(sp.GetRequiredService<MainViewModel>()));

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
