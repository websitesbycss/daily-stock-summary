using DailyStockSummary.Api.Contracts;
using DailyStockSummary.Core.Abstractions;
using DailyStockSummary.Core.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DailyStockSummary.Api.Endpoints;

public static class StocksEndpoints
{
    public const string RateLimitPolicy = "api";

    public static IEndpointRouteBuilder MapStocksEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stocks/{symbol}/daily-summary", GetDailySummary)
            .WithName("GetDailySummary")
            .WithSummary("Daily low/high averages and volume for the last month of 15-minute bars.")
            .RequireRateLimiting(RateLimitPolicy)
            .Produces<DailySummaryDto[]>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return app;
    }

    private static async Task<Ok<DailySummaryDto[]>> GetDailySummary(
        string symbol,
        IStockSummaryService service,
        CancellationToken cancellationToken)
    {
        // Parsed here so an invalid symbol surfaces as InvalidSymbolException (400) inside the request pipeline.
        var summaries = await service.GetDailySummariesAsync(Symbol.Parse(symbol), cancellationToken);

        return TypedResults.Ok(summaries.Select(DailySummaryDto.From).ToArray());
    }
}
