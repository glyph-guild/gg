using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// `gg gates` lists what is waiting on a person.
/// </summary>
/// <remarks>
/// <para>
/// <b>Five columns, and the fifth was built last step for something else.</b> The
/// flight, the obligation, the commit, who may decide — and <b>why the obligation
/// attached</b>. That last one is step 2's <c>ObligationAttributed</c> attribution,
/// which turns out to be the gate list's most important column: <i>"a decision is
/// waiting"</i> is a chore, and <i>"a decision is waiting because this flight
/// touched migrations/0002_backfill.sql"</i> is a reason to look.
/// </para>
/// <para>
/// <b>The commit is the whole point of pushing before asking.</b> A gate whose work
/// exists only in a working tree on somebody's laptop is a gate nobody can act on,
/// so the reference is a column rather than a detail.
/// </para>
/// </remarks>
public class GatesVerbTests
{
    private static GateList TwoGates() => new()
    {
        Gates =
        [
            new PendingGate
            {
                FlightNumber = "GG-42",
                ObligationId = "reversibility-plan",
                Approver = "platform-oncall",
                Branch = "gg/GG-42",
                Commit = new string('a', 40),
                Condition = "change.manifest touches migrations/**",
                Because = "change.manifest names 1 path(s) under 'migrations/**': "
                        + "migrations/0002_backfill.sql.",
                AwaitingSince = new DateTimeOffset(2026, 8, 13, 9, 0, 0, TimeSpan.Zero),
            },
            new PendingGate
            {
                FlightNumber = "GG-43",
                ObligationId = "reversibility-plan",
                Approver = "platform-oncall",
                Branch = "gg/GG-43",
                Commit = new string('b', 40),
                Condition = null,
                Because = "this obligation declares no condition, so it always applies to every "
                        + "flight this envelope governs",
                AwaitingSince = new DateTimeOffset(2026, 8, 13, 10, 0, 0, TimeSpan.Zero),
            },
        ],
    };

    // ---- the five columns ----

    [Test]
    public async Task Every_gate_names_the_flight_the_obligation_the_commit_and_the_approver()
    {
        var text = VerbOutput.ToText(new VerbResult.Gates(TwoGates()));

        await Assert.That(text).Contains("GG-42");
        await Assert.That(text).Contains("reversibility-plan");
        await Assert.That(text).Contains("platform-oncall");
        await Assert.That(text).Contains(new string('a', 7))
            .Because("the commit is what makes the work reviewable, so it is on the row.");
    }

    [Test]
    public async Task Every_gate_says_why_the_obligation_attached()
    {
        // THE COLUMN THAT WAS FREE. Built for `gg why` last step, and it is what
        // turns a list of chores into a list of reasons.
        var text = VerbOutput.ToText(new VerbResult.Gates(TwoGates()));

        await Assert.That(text).Contains("migrations/0002_backfill.sql");
        await Assert.That(text).Contains("change.manifest touches migrations/**");
    }

    [Test]
    public async Task A_gate_from_an_unconditional_obligation_says_so_rather_than_leaving_it_blank()
    {
        // The same rule as `gg why`: "declares no condition" and "the condition is
        // unknown" must not render alike, and a blank would be both.
        var text = VerbOutput.ToText(new VerbResult.Gates(TwoGates()));

        await Assert.That(text).Contains("always (this obligation declares no condition)");
    }

    [Test]
    public async Task Nothing_waiting_says_so_rather_than_printing_a_heading()
    {
        // An empty table with a header reads as a broken query. "Nothing is
        // waiting on you" is an answer.
        var text = VerbOutput.ToText(new VerbResult.Gates(new GateList { Gates = [] }));

        await Assert.That(text).Contains("Nothing is waiting");
    }

    [Test]
    public async Task The_gates_are_listed_in_a_declared_order()
    {
        // Oldest first, because the one that has been waiting longest is the one
        // somebody should look at. An undeclared order makes the list different on
        // every call for no reason anybody can see.
        var text = VerbOutput.ToText(new VerbResult.Gates(TwoGates()));

        await Assert.That(text.IndexOf("GG-42", StringComparison.Ordinal))
            .IsLessThan(text.IndexOf("GG-43", StringComparison.Ordinal));
    }

    // ---- --json from the first version ----

    [Test]
    public async Task The_verb_has_json_from_its_first_version()
    {
        var json = VerbOutput.ToJson(new VerbResult.Gates(TwoGates()));

        await Assert.That(json).Contains("reversibility-plan");
        await Assert.That(json).Contains("platform-oncall");
        await Assert.That(json).Contains(new string('a', 40))
            .Because("the full commit crosses in the machine-readable surface, because a script "
                   + "is what would fetch it.");
        await Assert.That(json).Contains("because");
    }

    [Test]
    public async Task The_rendered_and_json_surfaces_carry_the_same_gates()
    {
        var gates = TwoGates();
        var json = VerbOutput.ToJson(new VerbResult.Gates(gates));
        var text = VerbOutput.ToText(new VerbResult.Gates(gates));

        foreach (var gate in gates.Gates)
        {
            await Assert.That(json).Contains(gate.FlightNumber);
            await Assert.That(text).Contains(gate.FlightNumber);
        }
    }

    // ---- the client decides nothing ----

    [Test]
    public async Task The_verb_offers_no_way_to_answer_a_gate()
    {
        // THE ASSERTION THE SCOPE SPLIT EXISTS FOR. This step lists what is
        // waiting and nothing else: there is no decide, approve or reject in the
        // client, so nothing an agent can call can unstick a flight. Step 4 adds
        // exactly one, deliberately.
        var forbidden = new[] { "DecideAsync", "ApproveAsync", "RejectAsync", "AmendAsync" };

        var offenders = Sources()
            .SelectMany(f => forbidden
                .Where(name => File.ReadAllText(f).Contains(name, StringComparison.Ordinal))
                .Select(name => $"{Path.GetFileName(f)}: {name}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("a decision path is step 4's, and its absence here is what makes the "
                   + "zero-exits assertion true from both ends. Found: "
                   + string.Join(", ", offenders));

        // The scan can see one, so the emptiness means something.
        await Assert.That("public Task DecideAsync()".Contains("DecideAsync", StringComparison.Ordinal))
            .IsTrue();
    }

    private static IEnumerable<string> Sources()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = dir ?? throw new InvalidOperationException("Gg.sln not found");

        return new[] { "Gg.Client", "Gg.Cli", "Gg.Console" }
            .SelectMany(project => Directory.EnumerateFiles(
                Path.Combine(root.FullName, project), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal));
    }
}
