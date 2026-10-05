using System.Globalization;
using System.Threading.RateLimiting;
using DailyStockSummary.Api;
using DailyStockSummary.Api.Endpoints;
using DailyStockSummary.Api.Errors;
using DailyStockSummary.Api.Settings;
using DailyStockSummary.Infrastructure;
using Microsoft.AspNetCore.Cors.Infrastructure;
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

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

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

        // One window per client address. Behind a reverse proxy this needs forwarded-headers handling (deployment).
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
