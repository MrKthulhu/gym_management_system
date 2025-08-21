using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using gym_management_system.Data;

namespace gym_management_system
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            var cs = builder.Configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("Missing ConnectionStrings:Db");
            Db.Init(cs); // init DB

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
