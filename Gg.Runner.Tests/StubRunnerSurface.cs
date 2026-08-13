using System.Net;
using System.Text;
using System.Text.Json;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Runner.Tests;

/// <summary>
/// A local HTTP server speaking the runner half of the declared surface.
/// </summary>
/// <remarks>
/// It exists to observe what the client actually PUT ON THE WIRE - paths,
/// methods, headers - so those can be checked against the declaration. It is
/// not a control plane and proves nothing about one; the control plane checks
/// itself against the same declaration in its own repo.
/// </remarks>
internal sealed class StubRunnerSurface : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _loop;

    internal List<(string Method, string Path)> Observed { get; } = [];

    internal List<Dictionary<string, string>> Headers { get; } = [];

    internal HttpStatusCode ClaimStatus { get; set; } = HttpStatusCode.OK;

    internal HttpStatusCode RenewStatus { get; set; } = HttpStatusCode.OK;

    internal HttpStatusCode ReleaseStatus { get; set; } = HttpStatusCode.OK;

    internal string BaseAddress { get; }

    /// <summary>
    /// Binds a port by BINDING it, never by asking whether one is free.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asking the operating system for a free port and then giving it back
    /// before binding it is a check whose answer expires. The window is small
    /// and it is not theoretical: two stub servers - this one and its twin in
    /// the other test assembly, so two PROCESSES, which no lock of ours spans -
    /// were handed the same port, and then either one threw <c>Address already
    /// in use</c> from <c>Dispose</c> or, worse, both bound and each answered
    /// the other's client.
    /// </para>
    /// <para>
    /// So there is no probe. A candidate is tried by starting the real listener
    /// on it, and the operating system arbitrates the only bind that happens.
    /// Losing is ordinary and cheap; the loop tries the next one.
    /// </para>
    /// </remarks>
    /// <summary>Ports this process has already bound, so it never picks one twice.</summary>
    /// <remarks>
    /// Random candidates collide by birthday - sixteen picks from forty thousand
    /// is a fraction of a percent - and a fraction of a percent is exactly the
    /// rate a flake lives at. The operating system arbitrates between processes;
    /// this removes the chance inside one.
    /// </remarks>
    private static readonly HashSet<int> _taken = [];

    /// <summary>
    /// A listener already bound to a loopback port nobody else holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asking the operating system for a free port and then giving it back
    /// before binding it is a check whose answer expires. So there is no probe:
    /// a candidate is tried by starting the real listener on it, and the
    /// operating system arbitrates the only bind that happens.
    /// </para>
    /// <para>
    /// <b>A FRESH listener per attempt.</b> A failed <c>Start</c> disposes the
    /// one it failed on, so reusing it to try the next port throws
    /// <c>ObjectDisposedException</c> from <c>Prefixes</c> - out of a
    /// constructor, in whichever test was unlucky. That is a fault this retry
    /// introduced and the measurement loop found.
    /// </para>
    /// </remarks>
    private static (HttpListener Listener, string Address) BindLoopback()
    {
        for (var attempt = 1; ; attempt++)
        {
            int candidate;
            lock (_taken)
            {
                do
                {
                    // Above the range the operating system hands out for
                    // ephemeral sockets, so this is not competing with every
                    // outbound connection on the machine as well.
                    candidate = Random.Shared.Next(20000, 60000);
                }
                while (!_taken.Add(candidate));
            }

            var address = $"http://127.0.0.1:{candidate}/";
            var listener = new HttpListener();

            try
            {
                listener.Prefixes.Add(address);
                listener.Start();
                return (listener, address);
            }
            catch (HttpListenerException) when (attempt < 40)
            {
                // Another process had it. The listener is spent; the next
                // attempt builds another.
                listener.Close();
            }
        }
    }


    internal StubRunnerSurface()
    {
        (_listener, BaseAddress) = BindLoopback();
        _loop = Task.Run(ServeAsync);
    }

    private async Task ServeAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (_stopping.IsCancellationRequested)
            {
                return;
            }

            var path = context.Request.Url!.AbsolutePath;
            Observed.Add((context.Request.HttpMethod, path));
            Headers.Add(context.Request.Headers.AllKeys.Where(k => k is not null)
                .ToDictionary(k => k!, k => context.Request.Headers[k] ?? "", StringComparer.OrdinalIgnoreCase));

            if (path.EndsWith("/heartbeat", StringComparison.Ordinal))
            {
                await WriteJsonAsync(context, 200, new HeartbeatAccepted { NextHeartbeatSeconds = 5 });
            }
            else if (path.EndsWith(":claim", StringComparison.Ordinal))
            {
                if (ClaimStatus == HttpStatusCode.NoContent)
                {
                    await WriteAsync(context, 204, "");
                }
                else
                {
                    await WriteJsonAsync(context, 200, new LeaseGranted
                    {
                        LeaseId = "lease-9",
                        Generation = 3,
                        FlightId = "flight-9",
                        FlightNumber = FlightRef.Format(9),
                        Repos = [],
                        // References, never secrets - and this stub could not
                        // send one if it wanted to.
                        Credentials = [],
                        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
                        ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                        RenewWithinSeconds = 30,
                    });
                }
            }
            else if (path.EndsWith("/renew", StringComparison.Ordinal))
            {
                if (RenewStatus == HttpStatusCode.OK)
                {
                    await WriteJsonAsync(context, 200, new LeaseRenewed
                    {
                        ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                        Generation = 3,
                    });
                }
                else
                {
                    await WriteAsync(context, (int)RenewStatus, "");
                }
            }
            else if (path.EndsWith("/release", StringComparison.Ordinal))
            {
                if (ReleaseStatus == HttpStatusCode.OK)
                {
                    await WriteJsonAsync(context, 200, new LeaseReleased
                    {
                        FlightId = "flight-9",
                        Disposition = "completed",
                    });
                }
                else
                {
                    await WriteAsync(context, (int)ReleaseStatus, "");
                }
            }
            else
            {
                await WriteAsync(context, 404, "");
            }
        }
    }

    private static async Task WriteJsonAsync<T>(HttpListenerContext context, int status, T body)
    {
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        context.Response.ContentType = "application/json";
        await WriteAsync(context, status, json);
    }

    private static async Task WriteAsync(HttpListenerContext context, int status, string body)
    {
        context.Response.StatusCode = status;
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Stop();
        try { await _loop; } catch (Exception) { /* shutting down */ }

        try
        {
            _listener.Close();
        }
        catch (HttpListenerException)
        {
            // THE FLAKE. .NET's managed HttpListener - the implementation on
            // anything that is not Windows - re-enters its endpoint manager on
            // close, and that path BINDS a socket to look one up. When the entry
            // has already gone it tries to bind a port something else now holds
            // and throws "Address already in use", from Close, during teardown.
            //
            // Swallowed here and nowhere else. The request and response this
            // stub existed for have already happened and already been asserted;
            // failing a test on its own teardown reports a defect in whichever
            // test happened to be disposing, which is why the failures looked
            // scattered across a file and unrelated to each other.
        }

        _stopping.Dispose();
    }
}
