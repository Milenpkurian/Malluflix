using Malluflix.Core.DependencyInjection;
using Malluflix.StremioAddon.Endpoints;
using Malluflix.StremioAddon.Manifest;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) =>
    {
        cfg.ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console();

        var fileLogPath = ctx.Configuration["Logging:FilePath"];
        if (!string.IsNullOrWhiteSpace(fileLogPath))
        {
            cfg.WriteTo.File(fileLogPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7);
        }
    });

    builder.Services.AddMalluflixCore(builder.Configuration);
    builder.Services.AddSingleton<IManifestBuilder, ManifestBuilder>();

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    {
        p.AllowAnyMethod().AllowAnyHeader();
        if (allowedOrigins.Length == 0)
        {
            p.AllowAnyOrigin();
        }
        else
        {
            p.WithOrigins(allowedOrigins);
        }
    }));

    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    });

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.MapGet("/health", () => Results.Ok(new
    {
        status = "ok",
        service = "malluflix-stremio",
        version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
    }));

    app.MapStremio();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Malluflix.StremioAddon terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public sealed partial class Program;
