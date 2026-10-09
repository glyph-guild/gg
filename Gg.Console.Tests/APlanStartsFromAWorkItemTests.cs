using System.Text.Json;
using Gg.Client;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// <c>l</c> on a work item starts an itinerary planning session with an agent: the draft's intent
/// is already the item, and the agent can read the item itself through its tracker's reader
/// (owner, 2026-10-08).
/// </summary>
public class APlanStartsFromAWorkItemTests
{
    private static readonly PlanSeed Seed = new("ado", "18678", "Job Fields: Effective Date stuck in loading state");

    private static AppState Listed() => new()
    {
        ActiveTab = TabId.Intents,
        BrowseVisible = true,
        ReaderKeys = ["ado", "jira"],
        Browse = new BrowseListing
        {
            ProviderKey = "ado",
            Items = [new BrowseRow { Id = "18678", Title = Seed.Title!, State = "In Test" }],
        },
    };

    [Test]
    public async Task L_on_a_row_or_in_the_item_plans_from_it()
    {
        await Assert.That(Keymap.Resolve(KeyStroke.Char('l'), KeymapContext.For(Listed())))
            .IsEqualTo(Command.PlanFromIntent);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('l'), KeymapContext.For(Listed() with { Mode = UiMode.WorkItemDetail })))
            .IsEqualTo(Command.PlanFromIntent);
    }

    [Test]
    public async Task It_is_the_shells_to_run_and_the_reducers_to_leave()
    {
        await Assert.That(ShellCommands.Handled).Contains(Command.PlanFromIntent);
        var before = Listed();
        await Assert.That(Reducer.Reduce(before, Command.PlanFromIntent)).IsEqualTo(before);
    }

    [Test]
    public async Task The_loop_plans_from_the_row_under_the_cursor()
    {
        await Assert.That(Seeded(Listed())).IsEqualTo(Seed);
    }

    [Test]
    public async Task A_ticket_opened_from_a_flight_plans_from_its_own_tracker()
    {
        var opened = Listed() with
        {
            Mode = UiMode.WorkItemDetail,
            WorkItemProvider = "jira",
            WorkItemId = "PROJ-7",
            WorkItemRow = new BrowseRow { Id = "PROJ-7", Title = "", State = "" },
        };

        await Assert.That(Seeded(opened)).IsEqualTo(new PlanSeed("jira", "PROJ-7", null));
    }

    [Test]
    public async Task The_draft_starts_with_the_item_as_its_intent()
    {
        var root = Directory.CreateTempSubdirectory("gg-seeded-draft-");
        try
        {
            var drafts = new ItineraryDrafts(root.FullName);

            var draft = PtyPlanSession.SeedDraft(drafts, [], Seed);

            var held = (DraftRead.Held)drafts.Read(draft);
            await Assert.That(held.Draft.Intent).IsEqualTo(FlightIntent.Of(null, provider: "ado", id: "18678"));
            await Assert.That(held.Draft.Legs).IsEmpty();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_session_opens_on_the_item_and_can_read_it()
    {
        IReadOnlyList<string> argv = [];
        var root = Directory.CreateTempSubdirectory("gg-seeded-session-");
        try
        {
            new PtyPlanSession(
                    "claude",
                    () => new HostedTerminal { Columns = 100, Rows = 40 },
                    self: new SelfInvocation("/usr/local/bin/gg", ["runner", "tools"]),
                    host: (_, _, args, _, _, _, _) =>
                    {
                        argv = args;
                        return Task.FromResult(0);
                    },
                    drafts: new ItineraryDrafts(root.FullName),
                    seed: Seed,
                    reader: new IntentReader("ado", "/usr/local/bin/gg", ["runner", "read", "--provider", "ado"]))
                .Run();

            await Assert.That(argv[0]).IsEqualTo(AgentOpening.Plan(PtyPlanSession.Draft, Seed))
                .Because("the opening comes before every flag.");

            using var config = JsonDocument.Parse(argv[argv.ToList().IndexOf("--mcp-config") + 1]);
            var servers = config.RootElement.GetProperty("mcpServers");
            await Assert.That(servers.TryGetProperty(PlanningTool.Server, out _)).IsTrue();
            var reader = servers.GetProperty("ado");
            await Assert.That(reader.GetProperty("command").GetString()).IsEqualTo("/usr/local/bin/gg");
            await Assert.That(reader.GetProperty("args").EnumerateArray().Select(a => a.GetString()).ToList())
                .IsEquivalentTo((string?[])["runner", "read", "--provider", "ado"]);

            foreach (var tool in (string[])[ItemTool.Name, ItemTool.HistoryName, ItemTool.FieldsName])
            {
                await Assert.That(argv).Contains($"mcp__ado__{tool}")
                    .Because("reading the item it plans from is what the agent needs first.");
            }
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_opening_names_the_item_and_says_its_intent_is_set()
    {
        var prompt = AgentOpening.Plan("console-2", Seed);

        await Assert.That(prompt).Contains("ado#18678");
        await Assert.That(prompt).Contains(Seed.Title!);
        await Assert.That(prompt).Contains("mcp__ado__get_work_item");
        await Assert.That(prompt).Contains("already set");
        await Assert.That(prompt).DoesNotContain("\n");
        await Assert.That(AgentOpening.Plan("console-2")).DoesNotContain("already set")
            .Because("an unseeded plan opens as it always did.");
    }

    /// <summary>What the loop hands the plan launcher for a PlanFromIntent in this state.</summary>
    private static PlanSeed? Seeded(AppState state)
    {
        PlanSeed? seen = null;
        var ui = new Scripted(
            s => new UiOutcome(Command.PlanFromIntent, s),
            s => new UiOutcome(Command.Quit, s));

        new ConsoleLoop(ui, new NoEditor(), planFromIntent: (s, seed) =>
        {
            seen = seed;
            return s;
        }).Run(state);

        return seen;
    }

    private sealed class Scripted(params Func<AppState, UiOutcome>[] script) : IUiSession
    {
        private readonly Queue<Func<AppState, UiOutcome>> _script = new(script);

        public UiOutcome Run(AppState state) => _script.Dequeue()(state);
    }

    private sealed class NoEditor : IEditorSession
    {
        public string Edit(string initialText) => initialText;
    }
}
