using DailyStockSummary.Api.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace DailyStockSummary.Tests.Api;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task A_client_that_disconnects_mid_request_is_closed_quietly_without_error_logs()
    {
        // A user closing the tab is routine, not an incident: no 500, nothing at warning or above.
        using var provider = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var logger = new FakeLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(provider.GetRequiredService<IProblemDetailsService>(), logger);
        using var disconnected = new CancellationTokenSource();
        await disconnected.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = disconnected.Token, RequestServices = provider };

        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(disconnected.Token), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(499, context.Response.StatusCode);
        Assert.DoesNotContain(logger.Collector.GetSnapshot(), record => record.Level >= LogLevel.Warning);
    }
}
