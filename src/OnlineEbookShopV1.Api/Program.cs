using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OnlineEbookShopV1.Api.Data;
using OnlineEbookShopV1.Api.Logging;
using OnlineEbookShopV1.Api.Messaging;
using OnlineEbookShopV1.Api.Middleware;
using OnlineEbookShopV1.Api.Payments;
using OnlineEbookShopV1.Api.Services;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.AddShopLogging();

    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
    builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.Section));
    builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.Section));
    builder.Services.Configure<PayPalOptions>(builder.Configuration.GetSection(PayPalOptions.Section));

    var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
    if (jwt.Key.Length < 32)
        throw new InvalidOperationException("Jwt:Key must be at least 32 chars. Set it with user-secrets or the Jwt__Key environment variable.");

    builder.Services.AddDbContext<AppDbContext>(o =>
        o.UseNpgsql(builder.Configuration.GetConnectionString("Supabase"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.SchemaName)
                .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IEbookService, EbookService>();
    builder.Services.AddScoped<ICartService, CartService>();
    builder.Services.AddScoped<ICheckoutService, CheckoutService>();
    builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
    builder.Services.AddSingleton<IDomainEventHandler, AuthEventHandler>();
    builder.Services.AddSingleton<IDomainEventHandler, PaymentEventHandler>();
    builder.Services.AddHostedService<AuditEventConsumer>();
    builder.Services.AddSingleton<IStripeGateway, StripePaymentGateway>();
    builder.Services.AddHttpClient<IPayPalGateway, PayPalPaymentGateway>();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                RoleClaimType = ClaimTypes.Role,
                NameClaimType = "sub",
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
    builder.Services.AddAuthorization();

    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5234"];
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    app.UseShopRequestLogging();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    if (app.Environment.IsDevelopment())
        app.UseSwagger().UseSwaggerUI();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    await DatabaseInitializer.InitializeAsync(app);
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
