using OnlineEbookShopV1.Mvc.Api;
using OnlineEbookShopV1.Mvc.Logging;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.AddShopLogging();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddControllersWithViews();
    builder.Services.AddHttpClient<ShopApiClient>(c =>
    {
        var baseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5144";
        c.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        c.Timeout = TimeSpan.FromSeconds(30);
    });

    var app = builder.Build();
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
    }

    app.UseRouting();
    app.MapStaticAssets();
    app.MapControllerRoute(name: "default", pattern: "{controller=Catalog}/{action=Index}/{id?}").WithStaticAssets();
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "MVC terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
