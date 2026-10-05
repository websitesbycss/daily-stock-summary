using System.Globalization;
using System.Threading.RateLimiting;
using DailyStockSummary.Api;
using DailyStockSummary.Api.Endpoints;
using DailyStockSummary.Api.Errors;
using DailyStockSummary.Api.Settings;
using DailyStockSummary.Infrastructure;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

const string FrontendCorsPolicy = "frontend";

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    builder.Logging.AddJsonConsole();
}

builder.Services.AddStockSummary(builder.Configuration);

builder.Services.AddOptions<CorsSettings>()
    .Bind(builder.Configuration.GetSection(CorsSettings.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<CorsSettings>, CorsSettingsValidator>();

builder.Services.AddOptions<RateLimitingSettings>()
    .Bind(builder.Configuration.GetSection(RateLimitingSettings.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<RateLimitingSettings>, RateLimitingSettingsValidator>();

builder.Services.AddOptions<ForwardedHeadersSettings>()
    .Bind(builder.Configuration.GetSection(ForwardedHeadersSettings.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ForwardedHeadersSettings>, ForwardedHeadersSettingsValidator>();

builder.Services.AddSingleton<TimeZoneResolver>(TimeZoneInfo.FindSystemTimeZoneById);
builder.Services.AddHostedService<TimeZoneDataStartupCheck>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

// Only the configured networks are trusted (the defaults would trust loopback), so a client cannot pick its own address.
builder.Services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<ForwardedHeadersSettings>>((forwarded, settings) =>
{
    forwarded.KnownProxies.Clear();
    forwarded.KnownIPNetworks.Clear();

    if (!settings.Value.Enabled)
    {
        forwarded.ForwardedHeaders = ForwardedHeaders.None;
        return;
    }

    forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    forwarded.ForwardLimit = settings.Value.ForwardLimit;

    foreach (var network in settings.Value.KnownNetworks)
    {
        forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }
});

// Options are read lazily (after the host's final configuration is built), never from builder.Configuration here.
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>().Configure<IOptions<CorsSettings>>((cors, settings) =>
    cors.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(settings.Value.AllowedOrigins)
        .WithMethods(HttpMethods.Get)
        .WithHeaders(HeaderNames.Accept, HeaderNames.ContentType)));

builder.Services.AddRateLimiter(_ => { });
builder.Services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitingSettings>, IProblemDetailsService>(
    (limiter, settings, problemDetails) =>
    {
        limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // One window per client address (the real client when a trusted proxy forwards it; see ForwardedHeaders).
        limiter.AddPolicy(StocksEndpoints.RateLimitPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            ClientAddress.KeyFor(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.Value.PermitLimit,
                Window = settings.Value.Window,
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

        limiter.OnRejected = async (context, _) =>
        {
            var response = context.HttpContext.Response;
            response.StatusCode = StatusCodes.Status429TooManyRequests;

            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            }

            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context.HttpContext,
                ProblemDetails = Problems.RateLimited(),
            });
        };
    });

var app = builder.Build();

// First, so everything after it (rate limiting, logging) sees the real client address behind a trusted proxy.
app.UseForwardedHeaders();
// Added when the response starts, so they also survive the exception handler, which resets the response.
app.Use((context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
        return Task.CompletedTask;
    });

    return next(context);
});

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(FrontendCorsPolicy);
app.UseRateLimiter();

app.MapStocksEndpoints();
app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();

// Exposes the entry point to WebApplicationFactory in the integration tests.
public partial class Program;
