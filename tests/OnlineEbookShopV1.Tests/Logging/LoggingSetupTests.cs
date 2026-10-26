using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnlineEbookShopV1.Api.Logging;

namespace OnlineEbookShopV1.Tests.Logging;

public class LoggingSetupTests
{
    private static IConfiguration Config(string? isLocal) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(isLocal is null ? [] : new Dictionary<string, string?> { ["Logging:IsLocal"] = isLocal })
            .Build();

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, true)]
    public void IsLocal_selects_serilog(string? isLocal, bool expected) =>
        Assert.Equal(expected, LoggingSetup.UseSerilog(Config(isLocal)));

    [Fact]
    public void IsLocal_false_registers_application_insights()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Logging:IsLocal"] = "false";

        builder.AddShopLogging();

        Assert.Contains(builder.Services, s => s.ServiceType == typeof(TelemetryClient));
        Assert.DoesNotContain(builder.Services, s => s.ServiceType == typeof(Serilog.IDiagnosticContext));
    }

    [Fact]
    public void IsLocal_true_registers_serilog_not_application_insights()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Logging:IsLocal"] = "true";

        builder.AddShopLogging();

        Assert.Contains(builder.Services, s => s.ServiceType == typeof(Serilog.IDiagnosticContext));
        Assert.DoesNotContain(builder.Services, s => s.ServiceType == typeof(TelemetryClient));
    }
}
