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
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _loop;

    internal List<(string Method, string Path)> Observed { get; } = [];

    internal List<Dictionary<string, string>> Headers { get; } = [];

    internal HttpStatusCode ClaimStatus { get; set; } = HttpStatusCode.OK;

    internal HttpStatusCode RenewStatus { get; set; } = HttpStatusCode.OK;

    internal HttpStatusCode ReleaseStatus { get; set; } = HttpStatusCode.OK;

    internal string BaseAddress { get; }

    internal StubRunnerSurface()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        BaseAddress = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(BaseAddress);
        _listener.Start();
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
                        ClassificationCeiling = "internal",
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
        _listener.Close();
        _stopping.Dispose();
    }
}
