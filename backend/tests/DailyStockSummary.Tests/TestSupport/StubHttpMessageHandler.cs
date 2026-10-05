namespace DailyStockSummary.Tests.TestSupport;

/// <summary>A real <see cref="HttpMessageHandler"/> that records requests and replies from a script.</summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<CancellationToken, Task<HttpResponseMessage>> _respond;
    private readonly List<HttpRequestMessage> _requests = [];

    private StubHttpMessageHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

    /// <summary>Every request received so far (safe to read while requests are in flight).</summary>
    public IReadOnlyList<HttpRequestMessage> Requests
    {
        get
        {
            lock (_requests)
            {
                return [.. _requests];
            }
        }
    }

    public static StubHttpMessageHandler Returning(HttpResponseMessage response) =>
        new(_ => Task.FromResult(response));

    /// <summary>Builds a fresh response per call; the argument is the 1-based call number. May throw to simulate network errors.</summary>
    public static StubHttpMessageHandler Responding(Func<int, HttpResponseMessage> respond)
    {
        var calls = 0;
        return new(_ => Task.FromResult(respond(Interlocked.Increment(ref calls))));
    }

    public static StubHttpMessageHandler Throwing(Exception exception) =>
        new(_ => throw exception);

    /// <summary>Never answers; only the cancellation token it is handed can end the request.</summary>
    public static StubHttpMessageHandler WaitingForCancellation() =>
        new(async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("unreachable");
        });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_requests)
        {
            _requests.Add(request);
        }

        return _respond(cancellationToken);
    }
}
