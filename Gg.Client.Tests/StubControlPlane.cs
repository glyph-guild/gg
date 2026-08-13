using System.Net;
using System.Text;
using System.Text.Json;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Client.Tests;

/// <summary>
/// A local HTTP server that speaks the contract in <c>Gg.Contracts</c>.
/// </summary>
/// <remarks>
/// <para>
/// This repo cannot reference the control plane, so this is how gg's side of
/// the protocol is exercised. It is the honest consequence of the repo split,
/// not a shortcut: both sides are tested against the same published contract
/// types rather than against each other, and NO automated test spans the two
/// repositories.
/// </para>
/// <para>
/// It records the headers it receives, so the version-header requirement is
/// asserted against real requests rather than by reading the client's source.
/// </para>
/// </remarks>
public sealed class StubControlPlane : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _loop;

    /// <summary>Headers seen on every request, most recent last.</summary>
    public List<Dictionary<string, string>> ObservedHeaders { get; } = [];

    /// <summary>Paths seen, in order.</summary>
    public List<string> ObservedPaths { get; } = [];

    /// <summary>How many polls to answer 202 before completing.</summary>
    public int PendingPolls { get; set; }

    /// <summary>When set, polls answer 410 instead of ever completing.</summary>
    public bool Declined { get; set; }

    /// <summary>When set, every request is refused with 426.</summary>
    public string? ProtocolFloorMessage { get; set; }

    /// <summary>Degradations whoami reports for this tenant.</summary>
    public IReadOnlyList<TenantNotice> Notices { get; set; } = [];

    /// <summary>Where this stub claims the control plane exports to, if anywhere.</summary>
    public string? TelemetryDestination { get; set; }

    /// <summary>When set, every flight lookup answers 404.</summary>
    public bool FlightNotFound { get; set; }

    /// <summary>The body of the most recent request that carried one.</summary>
    /// <remarks>
    /// Recorded so what gg actually PUT ON THE WIRE is assertable, rather than
    /// what a reading of the client's source suggests it would.
    /// </remarks>
    public string LastBody { get; private set; } = "";

    /// <summary>
    /// Every body, kept.
    /// </summary>
    /// <remarks>
    /// "The secret is in no request body" is a claim about all of them. Only
    /// keeping the most recent one would make the assertion true of whichever
    /// call happened to come last.
    /// </remarks>
    public List<string> ObservedBodies { get; } = [];

    /// <summary>The credential references this stub is holding.</summary>
    public List<CredentialSummary> Credentials { get; } = [];

    /// <summary>When set, credential registration answers 400 with this diagnosis.</summary>
    public string? RefuseCredential { get; set; }

    /// <summary>Session tokens revoked through /v1/auth/logout.</summary>
    public HashSet<string> RevokedTokens { get; } = [];

    public string BaseAddress { get; }

    public const string IssuedSessionToken = "stub-session-token";

    private const string StubFlightId = "019fe815-6136-7518-bb57-b06d6d3f411a";

    private static FlightSummary AFlight() => new()
    {
        FlightId = StubFlightId,
        FlightNumber = FlightRef.Format(42),
        Name = "stub-flight",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "fix the login bug" },
        CreatedAt = DateTimeOffset.UtcNow,
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.1.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "none",
        Facts = [],
    };

    public StubControlPlane()
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

            try
            {
                await HandleAsync(context);
            }
            catch (Exception)
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var path = context.Request.Url!.AbsolutePath;
        ObservedPaths.Add(path);
        if (context.Request.HasEntityBody)
        {
            using var body = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            LastBody = await body.ReadToEndAsync();
            ObservedBodies.Add(LastBody);
        }

        ObservedHeaders.Add(context.Request.Headers.AllKeys
            .Where(k => k is not null)
            .ToDictionary(k => k!, k => context.Request.Headers[k] ?? "", StringComparer.OrdinalIgnoreCase));

        if (ProtocolFloorMessage is { } refusal)
        {
            await WriteAsync(context, 426, refusal);
            return;
        }

        switch (path)
        {
            case "/v1/auth/device":
                await WriteJsonAsync(context, 200, new DeviceAuthorizationStarted
                {
                    DeviceCode = "stub-device-code",
                    UserCode = "WXYZ-1234",
                    VerificationUri = "https://control-plane.invalid/activate",
                    PollIntervalSeconds = 1,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                });
                return;

            case "/v1/auth/device/token" when Declined:
                await WriteAsync(context, 410, "declined");
                return;

            case "/v1/auth/device/token" when PendingPolls > 0:
                PendingPolls--;
                await WriteAsync(context, 202, "");
                return;

            case "/v1/auth/device/token":
                await WriteJsonAsync(context, 200, new SessionIssued
                {
                    SessionToken = IssuedSessionToken,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
                    PrincipalDisplay = "stub-principal",
                    TenantId = "019fe062-d000-730c-a37d-7247342cd810",
                });
                return;

            case "/v1/auth/whoami":
                {
                    var token = context.Request.Headers["X-Gg-Session"] ?? "";
                    if (string.IsNullOrEmpty(token) || RevokedTokens.Contains(token))
                    {
                        await WriteAsync(context, 401, "");
                        return;
                    }
                    await WriteJsonAsync(context, 200, new WhoAmI
                    {
                        PrincipalId = "019fe8a2-0707-70c2-9ff8-be3adb54cef0",
                        PrincipalDisplay = "stub-principal",
                        TenantId = "019fe062-d000-730c-a37d-7247342cd810",
                        ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
                        Notices = Notices,
                    });
                    return;
                }

            case "/v1/auth/logout":
                RevokedTokens.Add(context.Request.Headers["X-Gg-Session"] ?? "");
                await WriteAsync(context, 204, "");
                return;

            case "/v1/flights" when context.Request.HttpMethod == "POST":
                // 202: the number is minted after this answers, so there is
                // none to return. Null rather than "" - see FlightLaunched.
                await WriteJsonAsync(context, 202, new FlightLaunched
                {
                    FlightId = StubFlightId,
                    FlightNumber = null,
                });
                return;

            case "/v1/flights":
                await WriteJsonAsync(context, 200, new FlightList { Flights = [AFlight()] });
                return;

            // The credential surface. This stub stores a REFERENCE, exactly as
            // the control plane does, and has nowhere to put anything else -
            // the request type it deserializes has no field for one.
            case "/v1/credentials" when context.Request.HttpMethod == "POST":
                {
                    if (RefuseCredential is { } credentialRefusal)
                    {
                        await WriteAsync(context, 400, credentialRefusal);
                        return;
                    }

                    var request = JsonSerializer.Deserialize<CredentialRegistrationRequest>(
                        LastBody, JsonSerializerOptions.Web)!;

                    var summary = new CredentialSummary
                    {
                        CredentialId = Guid.NewGuid().ToString(),
                        Repo = request.Repo,
                        Reference = request.Reference,
                        AddedAt = DateTimeOffset.UtcNow,
                    };
                    Credentials.Add(summary);

                    await WriteJsonAsync(context, 200, new CredentialRegistered
                    {
                        CredentialId = summary.CredentialId,
                        Reference = summary.Reference,
                        AddedAt = summary.AddedAt,
                    });
                    return;
                }

            case "/v1/credentials":
                await WriteJsonAsync(context, 200, new CredentialList { Credentials = [.. Credentials] });
                return;

            case var _ when path.StartsWith("/v1/credentials/", StringComparison.Ordinal)
                         && context.Request.HttpMethod == "DELETE":
                {
                    var id = path["/v1/credentials/".Length..];
                    var held = Credentials.SingleOrDefault(c => c.CredentialId == id);
                    if (held is null)
                    {
                        await WriteAsync(context, 404, "");
                        return;
                    }

                    Credentials.Remove(held);
                    // The reference comes back, so gg can clean up the local
                    // secret the reference pointed at. There is no other way
                    // for it to know which file that was.
                    await WriteJsonAsync(context, 200, new CredentialRemoved
                    {
                        CredentialId = held.CredentialId,
                        Reference = held.Reference,
                    });
                    return;
                }

            case "/v1/telemetry":
                await WriteJsonAsync(context, 200, new TelemetryDisclosure
                {
                    Exporting = TelemetryDestination is not null,
                    Destination = TelemetryDestination,
                });
                return;

            case "/v1/runners":
                await WriteJsonAsync(context, 200, new RunnerList
                {
                    Runners =
                    [
                        new RunnerSummary
                        {
                            RunnerId = "019fe8a2-0707-70c2-9ff8-be3adb54cef0",
                            Label = "stub-runner",
                            State = RunnerStates.Idle,
                            LastHeartbeatAt = DateTimeOffset.UtcNow,
                        },
                    ],
                });
                return;

            case var _ when path.StartsWith("/v1/flights/", StringComparison.Ordinal) && FlightNotFound:
                await WriteAsync(context, 404, "");
                return;

            case var _ when path.EndsWith("/log", StringComparison.Ordinal)
                         && path.StartsWith("/v1/flights/", StringComparison.Ordinal):
                await WriteJsonAsync(context, 200, new FlightLog
                {
                    FlightId = StubFlightId,
                    FlightNumber = FlightRef.Format(42),
                    Entries =
                    [
                        new FlightLogEntry
                        {
                            At = DateTimeOffset.UtcNow,
                            Kind = "lease-granted",
                            Detail = "{\"generation\":1}",
                        },
                    ],
                });
                return;

            case var _ when path.StartsWith("/v1/flights/", StringComparison.Ordinal):
                await WriteJsonAsync(context, 200, AFlight());
                return;

            default:
                await WriteAsync(context, 404, "");
                return;
        }
    }

    private static async Task WriteJsonAsync<T>(HttpListenerContext context, int status, T body)
    {
        var json = JsonSerializer.Serialize(body, JsonSerializerOptions.Web);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        var bytes = Encoding.UTF8.GetBytes(json);
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private static async Task WriteAsync(HttpListenerContext context, int status, string body)
    {
        context.Response.StatusCode = status;
        if (body.Length > 0)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            await context.Response.OutputStream.WriteAsync(bytes);
        }
        context.Response.Close();
    }

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


    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Stop();

        // The loop first, THEN the close. Closing while the serve loop is still
        // holding the listener hands it a disposed object, and the order is the
        // only thing that prevents it.
        try
        {
            await _loop;
        }
        catch (Exception)
        {
            // Shutting down; the listener loop's cancellation is expected.
        }

        try
        {
            _listener.Close();
        }
        catch (HttpListenerException)
        {
            // The same teardown fault the runner stub documents: .NET's managed
            // HttpListener re-enters its endpoint manager on close, and that
            // path binds a socket to look one up. Swallowed here and nowhere
            // else - what this stub existed to serve has already been asserted.
        }

        _stopping.Dispose();
    }
}
