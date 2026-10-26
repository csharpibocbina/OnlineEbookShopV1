using Serilog;

namespace OnlineEbookShopV1.Api.Logging;

public static class LoggingSetup
{
    public static bool UseSerilog(IConfiguration configuration) =>
        configuration.GetValue("Logging:IsLocal", true);

    public static void AddShopLogging(this WebApplicationBuilder builder)
    {
        if (UseSerilog(builder.Configuration))
        {
            builder.Host.UseSerilog((ctx, services, cfg) => cfg
                .ReadFrom.Configuration(ctx.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext());
        }
        else
        {
            builder.Services.AddApplicationInsightsTelemetry();
        }
    }

    public static void UseShopRequestLogging(this WebApplication app)
    {
        if (UseSerilog(app.Configuration))
            app.UseSerilogRequestLogging();
    }
}
