using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// A control plane that answers everything the console's boot asks, counts the
/// questions, and records how many of each kind were in the air together.
/// </summary>
/// <remarks>
/// <b>A yield rather than a delay, so concurrency is a fact here and not a
/// race.</b> The double answers on a continuation, so a loader that starts its
/// requests before awaiting them has all of them inside the handler at once and
/// a loader that awaits each in turn has exactly one. There is no clock in it.
/// </remarks>
internal sealed class AConsolePlane : HttpMessageHandler
{
    internal const int DefaultFlights = 24;

    private static readonly DateTimeOffset T0 = new(2026, 9, 6, 9, 0, 0, TimeSpan.Zero);

    private readonly int _flights;
    private readonly int _inTheAir;

    private int _live;
    private int _liveLogs;

    internal AConsolePlane(int flights = DefaultFlights, int inTheAir = 0)
    {
        _flights = flights;
        _inTheAir = inTheAir;
    }

    internal int Peak;
    internal int PeakLogs;
    internal int Requests;

    internal List<string> Paths { get; } = [];

    internal IReadOnlyList<string> LogsRead =>
        [.. Paths.Where(p => p.EndsWith("/log", StringComparison.Ordinal))];

    /// <summary>
    /// A flight id the client will accept. <c>FlightCommands.Readable</c> refuses
    /// anything that is neither a GG number nor an id, so "f-1" never reaches a
    /// request at all.
    /// </summary>
    internal static string Id(int n) => new Guid(n, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]).ToString();

    /// <summary>The first <c>inTheAir</c> are open; the rest have landed.</summary>
    internal FlightSummary AFlight(int n) => new()
    {
        FlightId = Id(n),
        FlightNumber = FlightRef.Format(n),
        Name = $"work {n}",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "work" },
        CreatedAt = T0.AddMinutes(n),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.25.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v6",
        Attempts = 1,
        State = n <= _inTheAir ? FlightStates.Open : FlightStates.Landed,
        Facts = [],
    };

    internal static (ConsoleData Data, AConsolePlane Plane) Console(
        int flights = DefaultFlights, int inTheAir = 0)
    {
        var plane = new AConsolePlane(flights, inTheAir);
        var http = new HttpClient(plane) { BaseAddress = new Uri("http://console.test/") };
        var client = new ControlPlaneClient(http);
        var sessions = new HasSession();

        return (
            new ConsoleData(
                new FlightCommands(client, sessions),
                new CredentialCommands(client, sessions, new NoStore(), new NeverAsked()),
                new TakeCommands(client, sessions),
                new IdentityCommands(client, sessions),
                new EnvelopeCommands(client, sessions)),
            plane);
    }

    private static void Highest(ref int watermark, int candidate)
    {
        var seen = Volatile.Read(ref watermark);
        while (candidate > seen)
        {
            var was = Interlocked.CompareExchange(ref watermark, candidate, seen);
            if (was == seen)
            {
                return;
            }

            seen = was;
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        var log = path.EndsWith("/log", StringComparison.Ordinal);

        Interlocked.Increment(ref Requests);
        lock (Paths)
        {
            Paths.Add(path);
        }

        Highest(ref Peak, Interlocked.Increment(ref _live));
        if (log)
        {
            Highest(ref PeakLogs, Interlocked.Increment(ref _liveLogs));
        }

        await Task.Yield();

        Interlocked.Decrement(ref _live);
        if (log)
        {
            Interlocked.Decrement(ref _liveLogs);
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Body(path), Encoding.UTF8, "application/json"),
        };
    }

    private string Body(string path)
    {
        if (path.EndsWith("/log", StringComparison.Ordinal))
        {
            return JsonSerializer.Serialize(
                new FlightLog
                {
                    FlightId = "f",
                    FlightNumber = "GG-1",
                    Entries =
                    [
                        new FlightLogEntry
                        {
                            Kind = "read-on-demand",
                            At = T0,
                            Detail = "the log this flight was asked for",
                        },
                    ],
                },
                ProtocolJsonContext.Default.FlightLog);
        }

        if (path.EndsWith("/why", StringComparison.Ordinal))
        {
            return JsonSerializer.Serialize(
                new FlightAttribution
                {
                    FlightNumber = "GG-1",
                    EnvelopeVersion = "v6",
                    Obligations = [],
                },
                ProtocolJsonContext.Default.FlightAttribution);
        }

        if (path.EndsWith("/seed", StringComparison.Ordinal))
        {
            return JsonSerializer.Serialize(
                new TakeSeed
                {
                    Revision = 1,
                    FlightNumber = "GG-1",
                    FlightId = Id(1),
                    Measurements = new TakeMeasurements
                    {
                        FilesEdited = [],
                        FilesReadNotEdited = [],
                        Searches = [],
                        Errors = [],
                        UndeclaredMovesUsed = [],
                        Attempts = 1,
                        StopReason = "landed",
                    },
                    AccountState = "none",
                    TranscriptState = "none",
                },
                ProtocolJsonContext.Default.TakeSeed);
        }

        return path switch
        {
            "/v1/flights" => JsonSerializer.Serialize(
                new FlightList { Flights = [.. Enumerable.Range(1, _flights).Select(AFlight)] },
                ProtocolJsonContext.Default.FlightList),
            "/v1/runners" => JsonSerializer.Serialize(
                new RunnerList { Runners = [] }, ProtocolJsonContext.Default.RunnerList),
            "/v1/gates" => JsonSerializer.Serialize(
                new GateList { Gates = [] }, ProtocolJsonContext.Default.GateList),
            "/v1/credentials" => JsonSerializer.Serialize(
                new CredentialList { Credentials = [] },
                ProtocolJsonContext.Default.CredentialList),
            "/v1/auth/whoami" => JsonSerializer.Serialize(
                new WhoAmI
                {
                    PrincipalId = "p",
                    PrincipalDisplay = "somebody",
                    TenantId = "t",
                    ExpiresAt = T0.AddHours(8),
                    Notices = [],
                },
                ProtocolJsonContext.Default.WhoAmI),
            _ => "{}",
        };
    }

    /// <summary>A session, so the verbs ask rather than refusing.</summary>
    private sealed class HasSession : ISessionStore
    {
        public StoredSession? Read() => new()
        {
            SessionToken = "a-token",
            ExpiresAt = new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TenantId = Guid.Empty.ToString(),
            PrincipalDisplay = "somebody",
        };

        public void Write(StoredSession session) { }

        public void Clear() { }
    }

    private sealed class NoStore : ICredentialStore
    {
        public string Root => "(no store)";

        public string Protection => "nothing is stored";

        public string PathFor(string locator) => throw new InvalidOperationException("no store");

        public void Write(string locator, string secret) =>
            throw new InvalidOperationException("no store");

        public string? Read(string locator) => null;

        public bool Remove(string locator) => false;
    }

    private sealed class NeverAsked : ISecretPrompt
    {
        public string ReadSecret(string prompt) =>
            throw new InvalidOperationException("the console does not prompt for secrets.");

        public string ReadLine(string prompt) =>
            throw new InvalidOperationException("the console does not prompt for secrets.");
    }
}
