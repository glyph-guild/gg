using System.Text.Json;
using Gg.Client;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// What the console's panes actually show, against a real control plane.
/// </summary>
/// <remarks>
/// <para>
/// <b>The claims this slice is built on were read off source, and source can be
/// read wrongly.</b> That a pane says <c>loading…</c> for ever and that the
/// evidence pane reports no selection while one is made are the two findings a
/// person notices first, and they are the two most likely to have another
/// cause. This is where they stop being inferences.
/// </para>
/// <para>
/// <b>No terminal is needed, and that is not a compromise.</b>
/// <c>ConsoleStart.LoadAsync</c> IS the boot - the whole of the console's data
/// plane is one call to it - and <c>PaneText</c> is a pure function of
/// <c>AppState</c>. Driving those two against a live control plane exercises
/// the real load path and the real renderers; a screenshot would show the same
/// strings through Terminal.Gui and could not be diffed. What this cannot see
/// is layout, and layout is not what this slice is about.
/// </para>
/// <para>
/// <b>Seeded here rather than by a script.</b> The tenant, its envelope and its
/// flights are created over HTTP by this test, so the measurement is
/// reproducible by anybody with the stack up and needs no second artefact to
/// keep in step.
/// </para>
/// </remarks>
[Category("RealStack")]
public class AgainstARealControlPlaneTests
{
    private static string Api =>
        Environment.GetEnvironmentVariable("GG_CONSOLE_API")
        ?? throw new InvalidOperationException(
            "GG_CONSOLE_API is not set. This drives the console's real boot against a real "
          + "control plane; skipping it would leave the findings this slice is built on as "
          + "readings of source. Bring up Gg.AppHost and set it to the api's address.");

    private static string Web =>
        Environment.GetEnvironmentVariable("GG_CONSOLE_WEB")
        ?? Api.Replace(":5199", ":5200", StringComparison.Ordinal);

    /// <summary>Counts what the boot asks for, which is S28.0-04's whole subject.</summary>
    internal sealed class Counting : DelegatingHandler
    {
        internal int Requests;

        internal List<string> Paths { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Requests);
            lock (Paths)
            {
                Paths.Add($"{request.Method} {request.RequestUri?.AbsolutePath}");
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }

    internal sealed record Seeded(
        ConsoleData Data, string Principal, Counting Counter, int Flights, HttpClient Session);

    /// <summary>
    /// A tenant with an envelope and a few flights, and a console pointed at it.
    /// </summary>
    private static async Task<Seeded> SeedAsync(int flights = 3)
    {
        // NOT disposed: the returned Seeded carries it, because staging a
        // stranded runner needs the same developer session the seeding used.
        var plain = new HttpClient { BaseAddress = new Uri(Api) };
        plain.DefaultRequestHeaders.Add("GG-Protocol-Version", "1");

        // A FRESH TENANT PER RUN. The fake identity provider maps a code to a
        // subject, so reusing one re-enters a previous run's tenant - whose
        // flights would make the counts below somebody else's measurement.
        var code = "console-walk-" + Guid.NewGuid().ToString("N")[..8];
        using var web = new HttpClient { BaseAddress = new Uri(Web) };
        var start = JsonDocument.Parse(await web.GetStringAsync("/signup/start"));
        var state = start.RootElement.GetProperty("state").GetString();
        var signup = JsonDocument.Parse(
            await web.GetStringAsync($"/signup/callback?code={code}&name={code}&state={state}"));
        var token = signup.RootElement.GetProperty("sessionToken").GetString()!;

        plain.DefaultRequestHeaders.Add("X-Gg-Session", token);

        var envelope = """
            {"context":{"scope":"**","constitution":"1.0.0"},
             "obligations":[{"id":"in-scope","check":"machine","rule":"no-file-outside-scope"},
                            {"id":"widen-root","check":"human","approver":"platform-owner",
                             "when":"envelope widens"}],
             "loops":[{"id":"implement","executor":"frontier","discharges":["in-scope"],
                       "moves":["read","edit"],"budget":{"wallClock":"10m"},
                       "onExhaustion":"handoff-to-human"}],
             "destinations":[{"id":"pull-request","kind":"pull-request","requires":["in-scope"]}]}
            """;
        (await plain.PutAsync("/v1/envelope",
            new StringContent(envelope, System.Text.Encoding.UTF8, "application/json")))
            .EnsureSuccessStatusCode();

        for (var at = 1; at <= flights; at++)
        {
            var body =
                $"{{\"name\":\"console walk {at}\",\"intent\":"
              + $"{{\"kind\":\"text\",\"text\":\"look at thing {at}\"}}}}";
            var opened = await plain.PostAsync("/v1/flights",
                new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

            if (opened.StatusCode is not (System.Net.HttpStatusCode.OK
                or System.Net.HttpStatusCode.Accepted))
            {
                throw new InvalidOperationException(
                    $"opening flight {at} answered {(int)opened.StatusCode}: "
                  + await opened.Content.ReadAsStringAsync());
            }
        }

        // THE FLIGHTS HAVE TO BE VISIBLE before the boot is measured, or the
        // count below is of an empty tenant and every pane is trivially empty.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var listed = JsonDocument.Parse(await plain.GetStringAsync("/v1/flights?all=true"));
            if (listed.RootElement.GetProperty("flights").GetArrayLength() >= flights)
            {
                break;
            }

            await Task.Delay(500);
        }

        var counter = new Counting { InnerHandler = new HttpClientHandler() };
        var http = new HttpClient(counter) { BaseAddress = new Uri(Api) };
        var home = Path.Combine(Path.GetTempPath(), "gg-console-walk-" + code);
        Directory.CreateDirectory(home);

        // The real stores, at a path of this test's own - so nothing here reads
        // or writes the machine's actual session.
        var sessions = new FileSessionStore(Path.Combine(home, "session.json"));
        sessions.Write(new StoredSession
        {
            SessionToken = token,
            ExpiresAt = new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TenantId = Guid.Empty.ToString(),
            PrincipalDisplay = code,
        });

        var client = new ControlPlaneClient(http);
        var takes = new TakeCommands(client, sessions);
        var data = new ConsoleData(
            new FlightCommands(client, sessions),
            new CredentialCommands(
                client, sessions, new FileCredentialStore(home), new NoPrompt()),
            takes);

        return new Seeded(data, takes.Principal(), counter, flights, plain);
    }

    /// <summary>
    /// A tenant whose console has a row in it, and the cheapest honest way to
    /// get one.
    /// </summary>
    /// <remarks>
    /// <b>A registration is a widening, and a widening rides a flight to its
    /// approver.</b> So registering a repository against an envelope that gates
    /// widenings opens a real gate on a real flight with no runner anywhere and
    /// no agent - which is a queue row, because a queue row is now a flight
    /// somebody is being asked about.
    /// </remarks>
    internal static async Task<Seeded> GatedAsync()
    {
        var seeded = await SeedAsync(flights: 2);

        var registered = await seeded.Session.PostAsync(
            "/v1/airspace/repositories",
            new StringContent(
                "{\"name\":\"widgets\",\"provider\":\"local\",\"id\":\"F_widgets01\","
              + "\"path\":\"acme/widgets\",\"credential\":\"none\"}",
                System.Text.Encoding.UTF8, "application/json"));

        var body = await registered.Content.ReadAsStringAsync();

        if (registered.StatusCode is not System.Net.HttpStatusCode.Accepted)
        {
            throw new InvalidOperationException(
                "registering a repository did not divert to the widening gate, so no gate was "
              + $"opened and there is no row to select: {(int)registered.StatusCode} {body}");
        }

        // THE GATE IS OPENED ON THE OTHER SIDE OF THE BROKER, so the console's
        // own boot is what waits for it - re-read until the queue fills, which
        // is the state under test rather than a proxy for it.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var booted = await ConsoleStart.LoadAsync(seeded.Data, seeded.Principal);
            if (booted.Queue.Count > 0)
            {
                return seeded;
            }

            await Task.Delay(1000);
        }

        throw new InvalidOperationException(
            "waited ninety seconds and the queue never filled, so the widening gate this test "
          + "opened never reached the console - which is a claim about the control plane "
          + "rather than about the console.");
    }

    private static Seeded? _stranded;

    /// <summary>
    /// A runner that holds a flight and then stops answering - the console's
    /// second queue reason, staged exactly as it would really happen.
    /// </summary>
    /// <remarks>
    /// It needs no agent: register a runner, let it claim, and say nothing. The
    /// control plane declares it offline on its own clock. What that produces is
    /// the subject of the test below.
    /// </remarks>
    private static async Task<Seeded> StrandedAsync()
    {
        if (_stranded is { } already)
        {
            return already;
        }

        var seeded = await SeedAsync(flights: 2);

        using var runner = new HttpClient { BaseAddress = new Uri(Api) };
        runner.DefaultRequestHeaders.Add("GG-Protocol-Version", "1");

        var registered = JsonDocument.Parse(await (await seeded.Session.PostAsync(
            "/v1/runners",
            new StringContent(
                "{\"label\":\"console-walk\",\"protocolVersion\":1}",
                System.Text.Encoding.UTF8, "application/json")))
            .Content.ReadAsStringAsync());

        var runnerId = registered.RootElement.GetProperty("runnerId").GetString();
        runner.DefaultRequestHeaders.Add(
            "X-Gg-Runner", registered.RootElement.GetProperty("runnerToken").GetString());

        // A CLAIM IS ACCEPTED, NOT ANSWERED. It settles on the other side of the
        // pump, so asking with no window comes back `expired` before anything
        // has looked - which reads as "no work" and is really "not yet".
        var accepted = JsonDocument.Parse(await (await runner.PostAsync(
            "/v1/leases:claim",
            new StringContent(
                $"{{\"runnerId\":\"{runnerId}\",\"labels\":[],\"maxWaitSeconds\":30}}",
                System.Text.Encoding.UTF8, "application/json")))
            .Content.ReadAsStringAsync());

        var request = accepted.RootElement.GetProperty("requestId").GetString();
        var settleBy = DateTimeOffset.UtcNow.AddSeconds(90);
        JsonDocument claimed;
        while (true)
        {
            claimed = JsonDocument.Parse(
                await runner.GetStringAsync($"/v1/leases/claims/{request}"));
            var state = claimed.RootElement.GetProperty("state").GetString();

            if (state is not ("waiting" or "accepted" or "pending") || DateTimeOffset.UtcNow > settleBy)
            {
                break;
            }

            await Task.Delay(1000);
        }

        if (!claimed.RootElement.TryGetProperty("lease", out var lease)
            || lease.ValueKind == JsonValueKind.Null)
        {
            throw new InvalidOperationException(
                "no flight was granted, so this runner holds nothing and going quiet strands "
              + "nobody: " + claimed.RootElement);
        }

        _stranded = seeded;
        return seeded;
    }

    /// <summary>Never asked: this walk registers no credential.</summary>
    private sealed class NoPrompt : ISecretPrompt
    {
        public string ReadSecret(string prompt) => throw Wandered();

        public string ReadLine(string prompt) => throw Wandered();

        private static InvalidOperationException Wandered() =>
            new("this walk types no secret, so a prompt reaching here is a read path that "
              + "wandered into a write one.");
    }

    // ---- S28.0-01 ----

    [Test]
    public async Task Every_pane_is_rendered_against_real_flights_and_written_down()
    {
        var seeded = await SeedAsync(flights: 3);
        var booted = await ConsoleStart.LoadAsync(seeded.Data, seeded.Principal);

        // WITH A ROW SELECTED, because that is the state a person is in a
        // keystroke after the console opens, and it is the state both findings
        // are about. The reducer does the selecting - not this test - so what
        // is rendered is what the product would render.
        var selected = booted.Queue.Count > 0
            ? Reducer.Reduce(booted, Command.SelectNext)
            : booted;

        var record = new System.Text.StringBuilder();
        record.AppendLine("# The console's panes, against a real control plane");
        record.AppendLine();
        record.AppendLine($"queue rows          {booted.Queue.Count}");
        record.AppendLine($"selected            {selected.Selected?.ToString() ?? "(none)"}");
        record.AppendLine($"boot requests       {seeded.Counter.Requests}");
        record.AppendLine();

        foreach (var (name, text) in ((string, string)[])
                 [("QUEUE", string.Join("\n", PaneText.QueueRows(selected))),
                  ("FLIGHT", PaneText.Flight(selected)),
                  ("EVIDENCE", PaneText.Evidence(selected)),
                  ("LIVE", PaneText.Live(selected)),
                  ("ACTIVITY", PaneText.Activity(selected)),
                  ("MODAL", PaneText.Modal(selected))])
        {
            record.AppendLine($"--- {name} ---");
            record.AppendLine(text.Length == 0 ? "(empty)" : text);
            record.AppendLine();
        }

        var kept = Environment.GetEnvironmentVariable("GG_CONSOLE_WALK_OUT");
        if (kept is { Length: > 0 })
        {
            await File.WriteAllTextAsync(kept, record.ToString());
        }

        await Assert.That(booted.Queue).IsEmpty()
            .Because("and this is the record's headline: three flights, an envelope in force, "
                   + "and an empty console. The record is:\n" + record);

        // SAID OUT LOUD IN THE FAILURE, because this test's product is the
        // record and a green run prints nothing.
        await Assert.That(record.ToString()).IsNotEmpty();
        System.Console.WriteLine(record.ToString());
    }

    // ---- S28.0-02 and S28.0-03, and both are worse than predicted ----

    [Test]
    public async Task Nothing_can_be_selected_so_neither_pane_is_reachable_at_all()
    {
        // THE PLAN PREDICTED the Flight pane saying `loading…` with a row
        // selected, and the Evidence pane saying "No flight selected" while one
        // was. Neither symptom is reachable: the queue is derived from two
        // troubles that cannot occur, so an ordinary tenant has no rows, and a
        // pane below a row that does not exist is not shown a wrong sentence -
        // it is not shown at all.
        var seeded = await SeedAsync(flights: 3);
        var booted = await ConsoleStart.LoadAsync(seeded.Data, seeded.Principal);
        var selected = Reducer.Reduce(booted, Command.SelectNext);

        await Assert.That(booted.Queue).IsEmpty()
            .Because("three flights, an envelope in force, and nothing in the pane called "
                   + "'flights needing me'.");
        await Assert.That(selected.Selected).IsNull()
            .Because("selection moves within the queue, and there is nothing to move to. The "
                   + "console opens onto a blank list and the arrow keys do nothing.");

        await Assert.That(PaneText.Evidence(selected)).Contains("No flight selected")
            .Because("which is TRUE here, and that is the sting: the sentence the plan "
                   + "called wrong is right, because nothing can be selected. It becomes "
                   + "wrong only once the queue can fill.");
    }

    [Test]
    public async Task A_stranded_runner_is_reported_holding_nothing_so_the_queue_stays_empty()
    {
        // THE SECOND QUEUE REASON, staged for real, and it cannot fire. The
        // console asks for a runner that is Offline AND holding this flight;
        // RunnerStatus.Derive returns Offline with a NULL flight, always - the
        // comment there argues that a stale holder must not read Busy, which is
        // right, and drops the flight id on the way, which is what makes the
        // console's rule unsatisfiable.
        var seeded = await StrandedAsync();
        var booted = await ConsoleStart.LoadAsync(seeded.Data, seeded.Principal);

        var runners = (VerbResult.Runners)await seeded.Data.RunnersAsync();
        var offline = runners.Value.Runners
            .Where(r => r.State == Gg.Contracts.RunnerStates.Offline)
            .ToList();

        await Assert.That(offline).IsNotEmpty()
            .Because("the runner this test stranded has to read offline, or it is measuring "
                   + "a fleet that is fine.");
        await Assert.That(offline.All(r => r.CurrentFlightId is null)).IsTrue()
            .Because("an offline runner is reported holding nothing, even while its lease is "
                   + "live - so 'the runner working this flight has died' is a sentence the "
                   + "control plane cannot say and the console cannot derive. Held: "
                   + string.Join(", ", offline.Select(r => r.CurrentFlightId ?? "null")));

        await Assert.That(booted.Queue).IsEmpty()
            .Because("which leaves the queue empty with a genuinely crashed runner holding a "
                   + "flight - the exact case its second reason was written for.");
    }

    // ---- S28.0-04 ----

    [Test]
    public async Task The_boot_costs_a_request_per_flight_and_the_number_is_written_down()
    {
        // STEP 2 ADDS TO THIS LOOP, so the number has to be known before it
        // does. LoadAsync fetches a log for every flight and discards them.
        var few = await SeedAsync(flights: 2);
        await ConsoleStart.LoadAsync(few.Data, few.Principal);
        var forTwo = few.Counter.Requests;

        var more = await SeedAsync(flights: 5);
        await ConsoleStart.LoadAsync(more.Data, more.Principal);
        var forFive = more.Counter.Requests;

        System.Console.WriteLine(
            $"boot requests: {forTwo} for 2 flights, {forFive} for 5. "
          + $"Paths for 5:\n  {string.Join("\n  ", more.Counter.Paths)}");

        await Assert.That(forFive).IsGreaterThan(forTwo)
            .Because("the boot is per-flight rather than constant, which is the shape step 2 "
                   + $"must not multiply. Measured: {forTwo} for two, {forFive} for five.");

        await Assert.That(forFive - forTwo).IsGreaterThanOrEqualTo(3)
            .Because("three more flights, at least three more requests - a log each. "
                   + $"Measured a difference of {forFive - forTwo}.");
    }
}
