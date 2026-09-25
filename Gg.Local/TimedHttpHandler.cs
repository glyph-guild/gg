namespace Gg.Local;

/// <summary>
/// Times every request, when somebody asked with <c>GG_TIMING</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A handler rather than a line at each call site.</b> The client sends from
/// more than forty places, and instrumenting the ones somebody already suspects
/// is how a measurement confirms whatever it was pointed at. This sees all of
/// them, including the ones nobody thought to look at - which is the whole
/// reason the first pass at this missed the board.
/// </para>
/// <para>
/// <b>The route and not the query.</b> A path can carry a flight id, which is
/// fine, and a query can carry what somebody typed into a filter - so the line
/// says the path and stops there.
/// </para>
/// </remarks>
public sealed class TimedHttpHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Timings.Active.Asked)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        using var timed = Timings.Active.Measure(
            $"http {request.Method.Method} {request.RequestUri?.AbsolutePath}");

        return await base.SendAsync(request, cancellationToken);
    }
}
