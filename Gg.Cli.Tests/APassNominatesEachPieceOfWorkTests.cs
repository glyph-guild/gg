using System.Text.Json;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// S53.4-03. A pass is offered a subject per nomination and told how many it
/// may open, and a flight that may open one is told what it was told before.
/// </summary>
/// <remarks>
/// <para>
/// <b>WITHOUT THIS THE CONTRACT MEMBER IS ONE NOBODY CAN FILL.</b>
/// <c>FlightNomination.Subject</c> shipped in 0.245.0 and the flight's tool
/// offers no way to say it - its arguments are the kind, the reason, the note
/// and the two selections, and its description says <i>"Call it once"</i>. So
/// an agent cannot propose a second leg whatever the contract permits, and the
/// walk this slice is for cannot happen.
/// </para>
/// <para>
/// <b>Not a second tool, for the sweep mode's reason.</b> A sweep is already
/// the same tool told it is about many items; a pass is the same again. Three
/// declarations of one tool is two of them drifting.
/// </para>
/// <para>
/// <b>And a flight that opens one thing reads exactly what it read before.</b>
/// Nearly every flight that nominates is a classifier deciding one kind for the
/// one item it is about, and a description rewritten for plans would change
/// what every one of those agents is told - measured behaviour, changed for a
/// case they are not in.
/// </para>
/// </remarks>
public class APassNominatesEachPieceOfWorkTests
{
    private const string List = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}";

    private static async Task<JsonElement> NominateToolAsync()
    {
        var output = new StringWriter();
        await PlatformToolServer.RunAsync(new StringReader(List), output, sweep: false);

        using var listed = JsonDocument.Parse(output.ToString().Trim());

        return listed.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(t => t.GetProperty("name").GetString() == NominationTool.Name)
            .Clone();
    }

    [Test]
    public async Task The_flights_tool_offers_a_subject_and_a_version()
    {
        var tool = await NominateToolAsync();
        var properties = tool.GetProperty("inputSchema").GetProperty("properties");

        await Assert.That(properties.TryGetProperty(NominationTool.Subject, out _)).IsTrue()
            .Because("a pass saying 'here are three pieces of work' must say which piece each "
                   + "nomination is about, or the three collapse onto one row.");

        await Assert.That(properties.TryGetProperty(NominationTool.Version, out _)).IsTrue()
            .Because("beside the subject, because the board supersedes per (nominator, "
                   + "subject) and the version is what tells a revision from a repeat.");
    }

    [Test]
    public async Task Neither_is_required()
    {
        // EVERY CLASSIFIER THAT EXISTS OMITS BOTH, and absent means what it has
        // always meant: this nomination is about the flight it came from. A
        // required subject would refuse every agent running today.
        var tool = await NominateToolAsync();

        var required = tool.GetProperty("inputSchema").GetProperty("required")
            .EnumerateArray().Select(r => r.GetString()).ToList();

        await Assert.That(required).IsEquivalentTo((string?[])
            [NominationTool.WorkKindArgument, NominationTool.ReasonArgument]);
    }

    [Test]
    public async Task A_pass_is_told_to_call_it_once_per_piece_of_work()
    {
        var tool = await NominateToolAsync();
        var described = tool.GetProperty("description").GetString()!;

        await Assert.That(described).Contains("once for each")
            .Because("the description is the only thing an agent reads. One that says 'call it "
                   + "once' is one no pass will ever call twice, whatever the schema permits.");
    }

    [Test]
    public async Task The_menu_says_how_many_flights_one_pass_may_open()
    {
        // TOLD RATHER THAN DISCOVERED. The cap is refused where the leg is
        // proposed rather than at admission (rule 10's shape), and an agent
        // that is not told the number learns it by having work thrown away.
        var menu = EnvelopeText.RenderMenu(Capped(3));

        await Assert.That(menu).IsNotNull();
        await Assert.That(menu!).Contains("3")
            .Because("the number is the whole of what this line is for.");
    }

    [Test]
    public async Task A_destination_with_no_cap_says_what_it_said_before()
    {
        // The half that would be skipped: nearly every flight that nominates is
        // a classifier about one item, and a line added for all of them would
        // change what every measured agent reads.
        var menu = EnvelopeText.RenderMenu(Capped(null));

        await Assert.That(menu).IsNotNull();
        await Assert.That(menu!.Contains("may open", StringComparison.OrdinalIgnoreCase))
            .IsFalse()
            .Because("absent is unbounded, so there is no number to give - and inventing a "
                   + "sentence about it would tell an agent about a bound nobody wrote.");
    }

    private static Envelope Capped(int? cap) => new()
    {
        Context = new ContextBinding { Scope = EnvelopeScopes.None, Constitution = "1.0.0" },
        Accepts = [],
        Produces = [],
        Obligations =
        [
            new Obligation
            {
                Id = "in-scope",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "work",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["in-scope"],
                Moves = [LoopMoves.Read],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "open-the-flight",
                Kind = DestinationKinds.Flight,
                Requires = ["in-scope"],
                Opens = ["research"],
                CapPerPass = cap,
            },
        ],
    };
}
