namespace DailyStockSummary.Infrastructure.Yahoo;

// Only the parts of the Yahoo v8 chart response this application reads.
// Everything is nullable because Yahoo omits or nulls fields for missing data.

internal sealed record YahooChartResponse(YahooChart? Chart);

internal sealed record YahooChart(IReadOnlyList<YahooChartResult>? Result, YahooChartError? Error);

internal sealed record YahooChartError(string? Code, string? Description);

internal sealed record YahooChartResult(
    YahooMeta? Meta,
    IReadOnlyList<long>? Timestamp,
    YahooIndicators? Indicators);

internal sealed record YahooMeta(string? ExchangeTimezoneName, int? Gmtoffset);

internal sealed record YahooIndicators(IReadOnlyList<YahooQuote>? Quote);

internal sealed record YahooQuote(
    IReadOnlyList<decimal?>? Low,
    IReadOnlyList<decimal?>? High,
    IReadOnlyList<long?>? Volume);
