using System.Net;
using System.Text;
using System.Text.Json;
using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The boot's reads overlap. It makes a request per flight and it does not
/// wait for each one before starting the next.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thirteen and a half seconds before the console drew anything.</b>
/// <c>ConsoleStart.LoadAsync</c> awaited every read in turn: the flight list,
/// the runners, then a log for each of fifty-two flights, then the gates, the
/// seed, the credentials, the identity and the why - fifty-nine round trips
/// laid end to end. Against a control plane 200ms away that is the whole
/// delay, and it grows by one round trip for every flight the tenant has ever
/// flown.
/// </para>
/// <para>
/// <b>The count is not the problem and is deliberately not changed.</b> A log
/// per flight is what fills the queue's two log-derived reasons and what makes
/// the detail modal free when somebody presses enter; dropping the reads would
/// take back the thing that was just asked for. What was wrong is that they
/// were sequential, and nothing about them says they have to be.
/// </para>
/// <para>
/// <b>A yield rather than a delay, so this is not a race.</b> The double
/// answers on a continuation, so a loader that starts its requests before
/// awaiting them has all of them inside the handler at once and a loader that
/// awaits each in turn has exactly one. There is no clock in it - the repo's
/// rule against sleeping in tests is also what makes this deterministic.
/// </para>
/// </remarks>
public class TheBootDoesNotReadOneLogAtATimeTests
{
    private const int Flights = 24;

    private static readonly DateTimeOffset T0 = new(2026, 9, 6, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A control plane that answers everything the boot asks and counts how many
    /// of each kind of question were in the air together.
    /// </summary>
    private sealed class Answering : HttpMessageHandler
    {
        private int _live;
        private int _liveLogs;

        internal int Peak;
        internal int PeakLogs;
        internal int Requests;
        internal List<string> Paths { get; } = [];

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

        private static string Body(string path)
        {
            if (path.EndsWith("/log", StringComparison.Ordinal))
            {
                return JsonSerializer.Serialize(
                    new FlightLog { FlightId = "f", FlightNumber = "GG-1", Entries = [] },
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
                        FlightId = "f-1",
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
                    new FlightList { Flights = [.. Enumerable.Range(1, Flights).Select(AFlight)] },
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
    }

    /// <summary>
    /// A flight id the client will accept. <c>FlightCommands.Readable</c> refuses
    /// anything that is neither a GG number nor an id, so "f-1" never reaches a
    /// request at all.
    /// </summary>
    private static string Id(int n) => new Guid(n, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]).ToString();

    private static FlightSummary AFlight(int n) => new()
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
        State = FlightStates.Landed,
        Facts = [],
    };

    private static (ConsoleData Data, Answering Plane) Console()
    {
        var plane = new Answering();
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

    [Test]
    public async Task More_than_one_log_is_in_the_air_at_once()
    {
        var (data, plane) = Console();

        await ConsoleStart.LoadAsync(data, "somebody");

        await Assert.That(plane.PeakLogs).IsGreaterThan(1)
            .Because($"a log per flight laid end to end is the delay. {Flights} flights, "
                   + $"and the most that were ever in the air together was {plane.PeakLogs}.");
    }

    [Test]
    public async Task Every_flight_still_gets_its_log()
    {
        // THE ANCHOR, and it is the half that must not move. Overlapping the
        // reads is free; fetching fewer of them would take the flight detail
        // back off the enter key, which is a different decision entirely.
        var (data, plane) = Console();

        var loaded = await ConsoleStart.LoadAsync(data, "somebody");

        await Assert.That(loaded.Logs.Count).IsEqualTo(Flights)
            .Because("every flight's log is still fetched, and the modal reads it from here.");
        await Assert.That(plane.Paths.Count(p => p.EndsWith("/log", StringComparison.Ordinal)))
            .IsEqualTo(Flights)
            .Because("one request each, not one and a retry.");
    }

    [Test]
    public async Task The_reads_that_need_nothing_from_each_other_do_not_queue_up()
    {
        // THE FLIGHT LIST, THE RUNNERS, THE GATES, THE CREDENTIALS AND THE
        // IDENTITY. Five answers, none of which is an input to any of the
        // others, and the boot asked for them one at a time.
        var (data, plane) = Console();

        await ConsoleStart.LoadAsync(data, "somebody");

        await Assert.That(plane.Peak).IsGreaterThan(1)
            .Because("nothing in the boot is serial by necessity except what reads the flight "
                   + $"list first. The most in the air at once was {plane.Peak}.");
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
