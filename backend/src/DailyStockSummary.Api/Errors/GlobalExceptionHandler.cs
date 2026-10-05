using DailyStockSummary.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DailyStockSummary.Api.Errors;

/// <summary>
/// Turns exceptions into problem responses. Responses only ever carry fixed text (or our own validated
/// symbol); upstream messages, bodies and stack traces go to the log, never to the caller.
/// </summary>
public sealed partial class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Non-standard "client closed request" status; nobody is left to read it, it just ends the request cleanly.</summary>
    public const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var problem = ToProblem(exception);
        httpContext.Response.StatusCode = problem.Status!.Value;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });
    }

    private ProblemDetails ToProblem(Exception exception)
    {
        switch (exception)
        {
            case InvalidSymbolException invalid:
                LogRejected(logger, invalid.Message);
                return Problems.InvalidSymbol(invalid.Message);

            case SymbolNotFoundException notFound:
                LogRejected(logger, notFound.Message);
                return Problems.SymbolNotFound(notFound.Symbol.Value);

            case UpstreamUnavailableException upstream:
                LogUpstreamFailure(logger, upstream, upstream.Kind);
                return upstream.Kind == UpstreamFailureKind.Timeout ? Problems.UpstreamTimeout() : Problems.UpstreamUnavailable();

            default:
                LogUnexpected(logger, exception);
                return Problems.Unexpected();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Request rejected: {Reason}")]
    private static partial void LogRejected(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Market data provider failed ({Kind}).")]
    private static partial void LogUpstreamFailure(ILogger logger, Exception exception, UpstreamFailureKind kind);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while serving a request.")]
    private static partial void LogUnexpected(ILogger logger, Exception exception);
}
