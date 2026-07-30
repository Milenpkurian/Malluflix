using System.Threading.RateLimiting;
using Malluflix.Api.Endpoints;
using Malluflix.Core.DependencyInjection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
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
    builder.Services.AddOpenApi();

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

    builder.Services.AddRateLimiter(o =>
    {
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        o.AddPolicy("api", ctx => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    });

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseRateLimiter();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapGet("/", () => Results.Redirect("/openapi/v1.json"));
    }
    else
    {
        app.MapGet("/", () => Results.Ok(new { service = "malluflix-api", docs = "/openapi/v1.json (development only)" }));
    }

    app.MapGet("/health", () => Results.Ok(new
    {
        status = "ok",
        service = "malluflix-api",
        version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
    }));

    app.MapMovies();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Malluflix.Api terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public sealed partial class Program;
