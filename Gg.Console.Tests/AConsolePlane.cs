using System.Collections.Generic;
using System.Globalization;
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
    private readonly int _nominations;

    private int _live;
    private int _liveLogs;

    internal AConsolePlane(int flights = DefaultFlights, int inTheAir = 0, int nominations = 0)
    {
        _flights = flights;
        _inTheAir = inTheAir;
        _nominations = nominations;
    }

    internal int Peak;
    internal int PeakLogs;
    internal int Requests;

    internal List<string> Paths { get; } = [];

    /// <summary>Every page size asked for, in the order it was asked.</summary>
    /// <remarks>
    /// <b>The whole point of a page is a number somebody chose</b>, and a double
    /// that answered every list identically could not tell a refresh asking for
    /// what is on screen from one asking for the first hundred rows.
    /// </remarks>
    internal List<int> Limits { get; } = [];

    internal IReadOnlyList<string> LogsRead =>
        [.. Paths.Where(p => p.EndsWith("/log", StringComparison.Ordinal))];

    /// <summary>The stories asked for, which is what opening a flight now reads.</summary>
    internal IReadOnlyList<string> StoriesRead =>
        [.. Paths.Where(p => p.EndsWith("/story", StringComparison.Ordinal))];

    /// <summary>
    /// A flight id the client will accept. <c>FlightCommands.Readable</c> refuses
    /// anything that is neither a GG number nor an id, so "f-1" never reaches a
    /// request at all.
    /// </summary>
    internal static string Id(int n) => new Guid(n, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]).ToString();

    /// <summary>The flight every launch this plane accepts is named.</summary>
    internal static readonly string Launched = Id(9001);

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

    /// <summary>A nomination a board page can carry.</summary>
    internal static NominationSummary ANomination(int n) => new()
    {
        NominationId = new Guid(n, 1, 0, [0, 0, 0, 0, 0, 0, 0, 0]),
        Nominator = "a watch",
        Subject = $"nominated {n}",
        Version = "1",
        WorkKind = "ticket",
        Mode = DestinationOpening.Gated,
        State = NominationStates.Standing,
        MadeAt = T0.AddMinutes(n),
    };

    /// <summary>A watch standing, which is a board row under the nominations.</summary>
    internal static WatchStanding AStanding(int n) => new()
    {
        Name = $"watch-{n}",
        Version = "1",
        Window = "24h",
    };

    internal static (ConsoleData Data, AConsolePlane Plane) Console(
        int flights = DefaultFlights, int inTheAir = 0, int nominations = 0)
    {
        var plane = new AConsolePlane(flights, inTheAir, nominations);
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
        var query = request.RequestUri?.Query ?? string.Empty;
        var log = path.EndsWith("/log", StringComparison.Ordinal);

        Interlocked.Increment(ref Requests);
        lock (Paths)
        {
            Paths.Add(path);

            if (Asked(query, "limit") is { } size
                && int.TryParse(size, CultureInfo.InvariantCulture, out var many))
            {
                Limits.Add(many);
            }
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

        // THE DOOR THAT OPENS A FLIGHT, answered the way the real one is: 202,
        // naming the flight and not its number, which nobody has minted yet.
        // Unserved, it fell through to `{}` and a 200, which is a launch
        // missing its required id rather than a launch.
        if (request.Method == HttpMethod.Post && path == "/v1/flights")
        {
            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(
                        new FlightLaunched { FlightId = Launched },
                        ProtocolJsonContext.Default.FlightLaunched),
                    Encoding.UTF8,
                    "application/json"),
            };
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Body(path, query), Encoding.UTF8, "application/json"),
        };
    }

    /// <summary>One value out of a query string, or null.</summary>
    /// <remarks>
    /// By hand because there is no query parser in this assembly's reach and a
    /// package reference for two parameters would be a dependency in the double.
    /// </remarks>
    private static string? Asked(string query, string name)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var at = pair.IndexOf('=', StringComparison.Ordinal);

            if (at > 0 && string.Equals(pair[..at], name, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(pair[(at + 1)..]);
            }
        }

        return null;
    }

    /// <summary>
    /// Newest first, as many as were asked for, and where it stopped.
    /// </summary>
    /// <remarks>
    /// <b>One more row than the page, to know whether there is another.</b> The
    /// control plane's own shape - <c>PageCursor</c> and the two endpoints that
    /// compose it take <c>size + 1</c> for exactly this reason - because a page
    /// exactly as long as the limit is otherwise indistinguishable from the last
    /// one, and a cursor on the last page makes a reader ask for ever.
    /// </remarks>
    private static (IReadOnlyList<int> Rows, string? Next) Page(
        int count, string query, int whenUnasked)
    {
        var newest = Enumerable.Range(1, count).Reverse();

        // WHERE THE LAST PAGE STOPPED. Opaque to the console, which hands it
        // back untouched; the number is this double's own composition.
        if (Asked(query, "after") is { } after
            && int.TryParse(after, CultureInfo.InvariantCulture, out var stopped))
        {
            newest = newest.Where(n => n < stopped);
        }

        var size = Asked(query, "limit") is { } limit
                && int.TryParse(limit, CultureInfo.InvariantCulture, out var many)
            ? many
            : whenUnasked;

        var taken = newest.Take(size + 1).ToList();
        var rows = taken.Take(size).ToList();

        return (rows, taken.Count > size && rows.Count > 0
            ? rows[^1].ToString(CultureInfo.InvariantCulture)
            : null);
    }

    private string Body(string path, string query)
    {
        if (path.EndsWith("/log", StringComparison.Ordinal))
        {
            // THE ID IT WAS ASKED FOR. A constant here keyed every log under
            // one flight, so a test could not tell a log that arrived for the
            // right flight from one that arrived for any flight.
            var asked = path.Split('/')[^2];

            return JsonSerializer.Serialize(
                new FlightLog
                {
                    FlightId = asked,
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

        if (path.EndsWith("/story", StringComparison.Ordinal))
        {
            // THE ID IT WAS ASKED FOR, like the log beside it: a constant here
            // would key every story to one flight, and a test could not tell a
            // story that arrived for the right flight from one that arrived for
            // any flight.
            var about = path.Split('/')[^2];

            return JsonSerializer.Serialize(
                new FlightStory
                {
                    FlightId = Id(int.TryParse(about[3..], out var n) ? n : 1),
                    FlightNumber = about,
                    Stage = FlightStages.Ended,
                    State = FlightStates.Landed,
                    Entries =
                    [
                        new StoryEntry
                        {
                            Kind = StoryKinds.Created,
                            At = T0,
                            Params = ["read-on-demand"],
                            Actor = new Actor { Kind = ActorKinds.Person, Name = "somebody" },
                        },
                    ],
                },
                ProtocolJsonContext.Default.FlightStory);
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
            "/v1/flights" => Listed(query),
            "/v1/board" => Nominated(query),
            "/v1/watches" => JsonSerializer.Serialize(
                new WatchStandingList { Standings = [] },
                ProtocolJsonContext.Default.WatchStandingList),
            "/v1/runners" => JsonSerializer.Serialize(
                new RunnerList { Runners = [] }, ProtocolJsonContext.Default.RunnerList),
            // EMPTY, WHICH IS WHAT A FLEET THAT NAMES NO ALLOWANCE REPORTS.
            // Serving it at all matters: this double answers an unknown route
            // with `{}` and a 200, which deserializes into a required-property
            // failure rather than into the "not there" the console is built to
            // survive.
            "/v1/allowances" => JsonSerializer.Serialize(
                new AllowanceList { Allowances = [] },
                ProtocolJsonContext.Default.AllowanceList),
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

    /// <summary>The flights, paged.</summary>
    /// <remarks>
    /// <b>Unpaged answers everything, as the route still does.</b> Every console
    /// read now names a size, so what this arm mostly serves is a page - but a
    /// double that forced one would make a test about the unpaged answer
    /// impossible to write.
    /// </remarks>
    private string Listed(string query)
    {
        var (rows, next) = Page(_flights, query, _flights);

        return JsonSerializer.Serialize(
            new FlightList { Flights = [.. rows.Select(AFlight)], Next = next },
            ProtocolJsonContext.Default.FlightList);
    }

    private string Nominated(string query)
    {
        var (rows, next) = Page(_nominations, query, _nominations);

        return JsonSerializer.Serialize(
            new BoardPage
            {
                Nominations = [.. rows.Select(ANomination)],

                // WHAT WAS ASKED, rather than a constant: the console reads
                // this to caption the pane, and a board that reported ended
                // rows it had not been asked for would caption the wrong
                // absence.
                IncludedEnded = Asked(query, "ended") is "true",
                Next = next,
            },
            ProtocolJsonContext.Default.BoardPage);
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

        // Presence without resolving; this double holds nothing to resolve.
        public bool Holds(string locator) => false;

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
