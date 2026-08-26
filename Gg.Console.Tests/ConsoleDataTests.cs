using System.Reflection;
using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The console's data layer is the verb layer, structurally.
/// </summary>
/// <remarks>
/// <para>
/// 4a's property - human output is a rendering of the JSON, with no second
/// path - extends here, and should be structural rather than asserted. A pane
/// that could fetch by a route no verb uses is a pane whose output <c>--json</c>
/// cannot reproduce, and the two surfaces would drift with nothing noticing.
/// </para>
/// <para>
/// So these check the SHAPE of the console rather than its behaviour: what it
/// is able to hold, and what it is able to call.
/// </para>
/// </remarks>
public class ConsoleDataTests
{
    private static IEnumerable<Type> ConsoleTypes() =>
        typeof(AppState).Assembly.GetTypes().Where(t => !t.Name.StartsWith('<'));

    [Test]
    public async Task Nothing_in_the_console_can_reach_the_control_plane_directly()
    {
        // The whole property in one assertion. Without a client and without an
        // HttpClient there is no route out of this assembly except a verb.
        var forbidden = (Type[])[typeof(ControlPlaneClient), typeof(HttpClient), typeof(Uri)];
        var offenders = new List<string>();

        foreach (var type in ConsoleTypes())
        {
            foreach (var member in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic
                                                | BindingFlags.Instance | BindingFlags.Static))
            {
                if (forbidden.Contains(member.FieldType))
                {
                    offenders.Add($"{type.Name}.{member.Name} ({member.FieldType.Name})");
                }
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                                                 | BindingFlags.Instance | BindingFlags.Static
                                                 | BindingFlags.DeclaredOnly))
            {
                foreach (var parameter in method.GetParameters().Where(p => forbidden.Contains(p.ParameterType)))
                {
                    offenders.Add($"{type.Name}.{method.Name}({parameter.ParameterType.Name})");
                }
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("a pane that can fetch is a pane whose output --json cannot reproduce. Found: "
                   + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_structural_check_would_see_a_client_if_one_were_there()
    {
        // Poison twin. "No direct access found" is also what a reflection walk
        // over the wrong assembly returns, and this file would look diligent
        // either way.
        var planted = typeof(SmugglesAClient)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Any(f => f.FieldType == typeof(HttpClient));

        await Assert.That(planted).IsTrue()
            .Because("if the walk cannot see this, it cannot see anything.");
    }

    /// <summary>A type with the thing the walk hunts for, so it has something to find.</summary>
    private sealed class SmugglesAClient(HttpClient client)
    {
        private readonly HttpClient _client = client;

        public bool Reachable => _client is not null;
    }

    [Test]
    public async Task Every_load_the_console_can_do_is_a_verb()
    {
        // Each public method on the data layer returns a VerbResult, which is
        // the same value the verb hands to --json. The names match the verbs'
        // names deliberately: an equivalence you have to look up is one that
        // quietly stops holding. A method returning anything
        // else would be a second shape only the console can render.
        var loaders = typeof(ConsoleData)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .ToList();

        await Assert.That(loaders).IsNotEmpty();

        foreach (var method in loaders)
        {
            await Assert.That(method.ReturnType).IsEqualTo(typeof(Task<VerbResult>))
                .Because($"ConsoleData.{method.Name} returns {method.ReturnType.Name}, which no verb returns.");
        }
    }

    [Test]
    public async Task Every_verb_that_returns_a_result_has_a_console_equivalent()
    {
        // The other direction, and the half that rots first: a verb gains a
        // capability and the console silently does not. Over EVERY class that
        // holds verbs, not just the flight ones - a second verb class that the
        // rule did not reach would be a whole surface the console never grew.
        var verbs = VerbClasses
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName && m.ReturnType == typeof(Task<VerbResult>))
            .Select(m => m.Name)
            .ToHashSet();

        var console = typeof(ConsoleData)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToHashSet();

        // ALL THREE EXEMPTIONS ARE GONE, and what removed them was answering the
        // objection rather than overruling it.
        //
        // Each reason was about a MODAL. "A prompt inside a Terminal.Gui modal is a
        // keyboard path with its own escape-hatch rules"; "a link a person cannot
        // select out of a Terminal.Gui view is worse than no link at all". Both are
        // still true, and neither applies: these run in the shell, between UI
        // lifetimes, with the terminal provably free - the same arrangement $EDITOR
        // has always used. No modal is added and no escape hatch is needed.
        //
        // The clipboard was the other half of the invite objection - "clipboard
        // access is its own piece of work" - and slice seven did that work:
        // IClipboard, SystemClipboard and SeedPlacer, which places a value on the
        // clipboard or in a named file and never fails.
        //
        // And the secret is narrower than the objection assumed. CredentialCommands
        // prompts for the value itself, so it is never a parameter, a return value
        // or a local anywhere in Gg.Console - which matters because AppState is
        // written to disk under GG_STATE_DUMP and handed to the diagnostics bundle.
        // ONE EXEMPTION, and it is a READ rather than a write. `envelope show`
        // renders the rules governing a tenant's flights, and no pane does - so a
        // person can answer a gate in this console without being able to read what
        // governs it. That is a real gap and it is recorded here rather than left
        // invisible; it needs none of the machinery the three writes needed, which
        // is exactly why it did not come with them.
        var exempt = (string[])
        [
            // A READ, and a real gap rather than a decision: no pane renders the
            // rules governing a tenant's flights, so a person can answer a gate in
            // this console without being able to read what governs it. Recorded here
            // instead of left invisible. It needs none of the machinery the three
            // writes needed, which is why it did not arrive with them.
            "ShowAsync",

            // A WRITE TO A FILESYSTEM, and the one exemption here that is a
            // decision rather than a gap. Pull renders the estate into a git
            // working copy on the person's disk; a console pane has no working
            // copy, no cwd a person chose, and nothing to refuse when the tree
            // is dirty. The verb's whole subject is the thing the console does
            // not have. If the console ever grows an estate pane it renders
            // from the same read this calls - ReadEstateAsync - without needing
            // the tree at all.
            "AirspacePullAsync",

            // THE OTHER TWO HALVES OF THE SAME SUBJECT. Diff compares a working
            // copy against the estate and apply submits what differs; both take
            // a directory as their argument and neither has one to take in a
            // console. Same reason as pull, listed separately so that removing
            // one exemption does not silently carry the others.
            "AirspaceDiffAsync",
            "AirspaceApplyAsync",

            // Editing an envelope is the write half of that read, and it cannot come
            // first: you do not edit what you cannot see. When the pane exists this
            // is $EDITOR seeded with the canonical text and applied back - the same
            // shape as opening a flight, one document larger. The name also covers
            // StrategyCommands.ApplyAsync (slice twelve), the same document-edit
            // shape one role over, with the same missing-pane reason.
            "ApplyAsync",

            // The console DOES take a flight over, through ITakeSession, and it
            // reaches the same claim: Program.cs hands TakeSession
            // TakeCommands.ClaimAsync, so there is one path to the hold. What differs
            // is the composition - the verb claims and prints a seed, the console
            // claims and hands a person the terminal - and those are two ways of
            // spending a hold rather than two ways of taking one.
            "TakeAsync",

            // Handing back needs an `infer` port that spawns an agent to propose an
            // account of what somebody did, and building that means invoking an
            // executor from the console. The key exists and answers "this console is
            // not configured to hand flights back", which is asserted deliberately in
            // gg:ConsoleTakeWiringTests rather than left ambiguous.
            "ReturnAsync",
        ];
        var expected = verbs.Where(v => !exempt.Contains(v)).ToList();
        var missing = expected.Where(v => !console.Contains(v)).ToList();

        await Assert.That(missing).IsEmpty()
            .Because($"these verbs have no console equivalent: {string.Join(", ", missing)}");
        await Assert.That(expected).IsNotEmpty();
        await Assert.That(expected).Contains("ListCredentialsAsync")
            .Because("the credential list is a read, so the rule reaches it and the exemptions do not.");
    }

    /// <summary>Every class the CLI dispatches a verb to.</summary>
    /// <remarks>
    /// Listed rather than discovered, because "every public class returning
    /// VerbResult" would silently include a helper somebody wrote in a test.
    /// A new verb class not added here is a surface the equivalence rule does
    /// not reach, so the count is asserted too.
    /// </remarks>
    private static IReadOnlyList<Type> VerbClasses { get; } =
        [typeof(FlightCommands), typeof(CredentialCommands),
         typeof(EnvelopeCommands), typeof(TakeCommands), typeof(StrategyCommands)];

    [Test]
    public async Task Every_class_that_holds_verbs_is_in_that_list()
    {
        // THE COUNT THE COMMENT ABOVE ALWAYS CLAIMED WAS ASSERTED. It was not, and
        // two classes had walked past the equivalence rule as a result: EnvelopeCommands
        // since it was written, and TakeCommands when slice seven added it. A verb
        // surface the rule does not reach is a whole capability the console can
        // silently not grow.
        //
        // Discovered by shape and compared to the list, rather than replacing the
        // list with discovery: "every public class returning VerbResult" would
        // silently include a helper somebody wrote in a test, which is the reason
        // the list exists at all.
        var holders = typeof(FlightCommands).Assembly.GetExportedTypes()
            .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Any(m => !m.IsSpecialName && m.ReturnType == typeof(Task<VerbResult>)))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        var missing = holders.Where(t => !VerbClasses.Contains(t)).Select(t => t.Name).ToList();

        await Assert.That(missing).IsEmpty()
            .Because("a class holding verbs that this list does not name is a surface the console "
                   + "equivalence rule never sees. Found: " + string.Join(", ", missing));

        await Assert.That(VerbClasses.Count).IsEqualTo(holders.Count)
            .Because("and a name in the list that holds no verbs is a rule pointed at nothing.");
    }

    [Test]
    public async Task Applying_a_verb_result_is_the_only_way_flight_data_enters_the_model()
    {
        // Behavioural half of the structural claim: what a pane shows came
        // from a VerbResult, and can therefore be reproduced by --json.
        var summary = new FlightSummary
        {
            FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
            FlightNumber = FlightRef.Format(42),
            Name = "nightly audit",
            Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "fix the login bug" },
            CreatedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero),
            RunnerProtocolVersion = 1,
            FactVocabularyVersion = "0.1.0",
            ConstitutionVersion = "1.0.0",
            EnvelopeVersion = "none",
        Attempts = 1,
        Facts = [],
        };

        var state = ConsoleProjection.Apply(new AppState(), new VerbResult.Flight(summary));

        await Assert.That(state.Flight).IsEqualTo(summary);
        await Assert.That(PaneText.Flight(state)).Contains("nightly audit");
        await Assert.That(PaneText.Flight(state)).Contains(FlightRef.Format(42));
    }

    [Test]
    public async Task The_flight_pane_shows_the_credential_identity_rather_than_promising_it_later()
    {
        // The pane has said "(resolution arrives at step 5)" since it was
        // written. It is step 5. A placeholder that outlives its step is the
        // same lie as a stub verb, one screen further along.
        var state = ConsoleProjection.Apply(
            new AppState(),
            new VerbResult.Credentials(new CredentialList
            {
                Credentials =
                [
                    new CredentialSummary
                    {
                        CredentialId = "019fe815-6136-7518-bb57-b06d6d3f411a",
                        Repo = "acme/widgets",
                        Reference = new CredentialReference
                        {
                            Kind = CredentialKinds.Local,
                            Locator = "local:acme/widgets",
                            Identity = "acme-bot",
                            Scopes = [CredentialScopes.Read],
                        },
                        AddedAt = DateTimeOffset.UnixEpoch,
                    },
                ],
            }));

        state = ConsoleProjection.Apply(state, new VerbResult.Flight(Flight("a", "needs a token")));

        await Assert.That(PaneText.Flight(state)).Contains("acme-bot");
        await Assert.That(PaneText.Flight(state)).DoesNotContain("step 5");
    }

    [Test]
    public async Task A_flight_pane_with_no_credentials_registered_says_none_rather_than_nothing()
    {
        var state = ConsoleProjection.Apply(new AppState(), new VerbResult.Flight(Flight("a", "no credentials")));

        await Assert.That(PaneText.Flight(state)).Contains("credential")
            .Because("a section that vanishes reads as a flight with nothing to say about credentials.");
    }

    // ---- the queue is not a flight list ----

    private static FlightSummary Flight(string id, string name) => new()
    {
        FlightId = id,
        FlightNumber = FlightRef.Format(1),
        Name = name,
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "why" },
        CreatedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.1.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "none",
        Attempts = 1,
        Facts = [],
    };

    private static FlightLog LogWith(string id, int expiries) => new()
    {
        FlightId = id,
        FlightNumber = FlightRef.Format(1),
        Entries =
        [
            new FlightLogEntry { At = DateTimeOffset.UnixEpoch, Kind = "lease-granted", Detail = "{}" },
            .. Enumerable.Range(0, expiries).Select(i => new FlightLogEntry
            {
                At = DateTimeOffset.UnixEpoch.AddMinutes(i),
                Kind = "lease-expired",
                Detail = "{}",
            }),
        ],
    };

    [Test]
    public async Task A_healthy_flight_is_not_in_the_queue()
    {
        // The distinction the whole pane turns on. A flight that is simply
        // running is not a row: get this backwards and the console becomes the
        // dashboard the usage model rejects.
        var queue = ConsoleProjection.Queue(
            new FlightList { Flights = [Flight("a", "running fine")] },
            new Dictionary<string, FlightLog> { ["a"] = LogWith("a", expiries: 0) },
            new RunnerList { Runners = [] });

        await Assert.That(queue).IsEmpty();
    }

    [Test]
    public async Task A_flight_whose_lease_expired_twice_needs_me()
    {
        var queue = ConsoleProjection.Queue(
            new FlightList { Flights = [Flight("a", "stuck")] },
            new Dictionary<string, FlightLog> { ["a"] = LogWith("a", expiries: 2) },
            new RunnerList { Runners = [] });

        await Assert.That(queue.Single().Reason).IsEqualTo(QueueReason.LeaseExpiredTwice);
    }

    [Test]
    public async Task One_expiry_is_an_incident_rather_than_a_pattern()
    {
        // The threshold matters: every flight whose runner restarted once
        // would otherwise be in the queue, and a queue that is always full is
        // a list.
        var queue = ConsoleProjection.Queue(
            new FlightList { Flights = [Flight("a", "blipped")] },
            new Dictionary<string, FlightLog> { ["a"] = LogWith("a", expiries: 1) },
            new RunnerList { Runners = [] });

        await Assert.That(queue).IsEmpty();
    }

    [Test]
    public async Task A_flight_held_by_an_offline_runner_needs_me()
    {
        var queue = ConsoleProjection.Queue(
            new FlightList { Flights = [Flight("a", "stranded")] },
            new Dictionary<string, FlightLog> { ["a"] = LogWith("a", expiries: 0) },
            new RunnerList
            {
                Runners =
                [
                    new RunnerSummary
                    {
                        RunnerId = "r", Label = "laptop", State = RunnerStates.Offline,
                        CurrentFlightId = "a", CurrentFlightNumber = FlightRef.Format(1),
                        LastHeartbeatAt = DateTimeOffset.UnixEpoch,
                    },
                ],
            });

        await Assert.That(queue.Single().Reason).IsEqualTo(QueueReason.RunnerOffline);
    }

    [Test]
    public async Task A_busy_runner_is_not_a_reason_to_be_in_the_queue()
    {
        var queue = ConsoleProjection.Queue(
            new FlightList { Flights = [Flight("a", "working")] },
            new Dictionary<string, FlightLog> { ["a"] = LogWith("a", expiries: 0) },
            new RunnerList
            {
                Runners =
                [
                    new RunnerSummary
                    {
                        RunnerId = "r", Label = "laptop", State = RunnerStates.Busy,
                        CurrentFlightId = "a", CurrentFlightNumber = FlightRef.Format(1),
                        LastHeartbeatAt = DateTimeOffset.UnixEpoch,
                    },
                ],
            });

        await Assert.That(queue).IsEmpty();
    }

    [Test]
    public async Task The_sort_strategy_is_named_and_says_it_is_a_placeholder()
    {
        // The risk is shipping recency, calling it a queue, and letting it
        // calcify. A name that admits what it is makes replacing it an obvious
        // task rather than an archaeology.
        await Assert.That(QueueSort.Default.Name).Contains("placeholder");
        await Assert.That(string.IsNullOrWhiteSpace(QueueSort.Default.Name)).IsFalse();
    }

    [Test]
    public async Task The_order_does_not_depend_on_the_order_rows_arrived_in()
    {
        // An unstable sort under equal keys makes the cursor appear to move on
        // its own, which is indistinguishable from a bug in the arrival rules.
        var rows = Enumerable.Range(0, 6)
            .Select(i => new QueueRow
            {
                FlightId = $"f{i}",
                FlightNumber = FlightRef.Format(i),
                Name = $"n{i}",
                Reason = QueueReason.RunnerOffline,
                Since = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero),
            })
            .ToList();

        var one = QueueSort.Default.Order(rows).Select(r => r.FlightId).ToList();
        var other = QueueSort.Default.Order([.. rows.AsEnumerable().Reverse()]).Select(r => r.FlightId).ToList();

        await Assert.That(one).IsEquivalentTo(other);
    }
}

/// <summary>
/// What the console does when it cannot load anything.
/// </summary>
public class ConsoleStartTests
{
    private sealed class NoSession : ISessionStore
    {
        public StoredSession? Read() => null;
        public void Write(StoredSession value) { }
        public void Clear() { }
    }

    /// <summary>
    /// A store with nothing in it and nowhere to put anything.
    /// </summary>
    /// <remarks>
    /// The console never reaches these paths - it is not signed in, so every
    /// verb refuses first - and a store that threw would make that assumption
    /// visible if it stopped being true.
    /// </remarks>
    private sealed class RefusesEverything : ICredentialStore
    {
        public string Root => "(no store)";
        public string Protection => "nothing is stored";
        public string PathFor(string locator) => throw new InvalidOperationException("no store here");
        public void Write(string locator, string secret) => throw new InvalidOperationException("no store here");
        public string? Read(string locator) => null;
        public bool Remove(string locator) => false;
    }

    /// <summary>Throws if the console ever asks a person for a secret.</summary>
    private sealed class NeverAsked : ISecretPrompt
    {
        public string ReadSecret(string prompt) =>
            throw new InvalidOperationException("the console does not prompt for secrets.");

        public string ReadLine(string prompt) =>
            throw new InvalidOperationException("the console does not prompt for secrets.");
    }

    [Test]
    public async Task A_console_that_could_not_load_says_why_in_the_model()
    {
        // In the MODEL, not on a screen and not in a log, so the reason
        // survives the UI being destroyed. A console that forgets why it is
        // empty is a console that looks like it is working.
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1/") };
        var client = new ControlPlaneClient(http);
        var sessions = new NoSession();
        var data = new ConsoleData(
            new FlightCommands(client, sessions),
            new CredentialCommands(client, sessions, new RefusesEverything(), new NeverAsked()),
            new TakeCommands(client, sessions));

        var state = await ConsoleStart.LoadAsync(data);

        await Assert.That(state.Diagnosis).IsNotNull();
        await Assert.That(state.Queue).IsEmpty();
        await Assert.That(PaneText.QueueRows(state).Single()).IsEqualTo("(could not load)")
            .Because("an empty queue and a queue that failed to load are different facts.");
    }

    [Test]
    public async Task An_empty_queue_says_so_rather_than_showing_nothing()
    {
        await Assert.That(PaneText.QueueRows(new AppState()).Single()).IsEqualTo("nothing needs you");
    }
}
