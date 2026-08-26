using Gg.Cli;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg flights</c> shows what is in the air, and says how each one stands.
/// </summary>
/// <remarks>
/// <para>
/// <b>The verb's one-line description was aspirational for fourteen slices.</b>
/// It listed every flight a tenant had ever opened, which was the only thing it
/// could list while nothing recorded an ending — so the queue only ever grew,
/// and a governance act that had already applied sat in it looking exactly like
/// one somebody was still working on.
/// </para>
/// <para>
/// <b><c>unknown</c> stays in the default view.</b> Hiding it would produce a
/// tidier queue and would be the empty-queue-reads-as-health failure this slice
/// is most afraid of: a flight nobody can account for is precisely what somebody
/// should see.
/// </para>
/// </remarks>
public class FlightsQueueTests
{
    private static FlightSummary AFlight(string number, string state) => new()
    {
        FlightId = Guid.NewGuid().ToString(),
        FlightNumber = number,
        Name = "fix the rounding",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "fix it" },
        CreatedAt = new DateTimeOffset(2026, 8, 26, 11, 0, 0, TimeSpan.Zero),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.1.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v1",
        Attempts = 0,
        Facts = [],
        State = state,
    };

    [Test]
    public async Task The_verb_takes_all_and_it_is_position_independent()
    {
        // --json is position-independent because a person will type it in both
        // places, and being told off for one of them is not helpful. The same
        // argument applies to this, so the same treatment does.
        await Assert.That(CliArgs.Parse(["flights"])).IsEquivalentTo(
            new CliAction.Flights(Json: false, All: false));
        await Assert.That(CliArgs.Parse(["flights", "--all"])).IsEquivalentTo(
            new CliAction.Flights(Json: false, All: true));
        await Assert.That(CliArgs.Parse(["--all", "flights"])).IsEquivalentTo(
            new CliAction.Flights(Json: false, All: true));
        await Assert.That(CliArgs.Parse(["flights", "--all", "--json"])).IsEquivalentTo(
            new CliAction.Flights(Json: true, All: true));
    }

    [Test]
    public async Task The_help_says_what_the_default_is()
    {
        // A default that narrowed silently would be worse than one that never
        // changed: somebody who knew this listed everything has to be able to
        // find out that it no longer does.
        var usage = CliArgs.Parse(["definitely-not-a-verb"]) as CliAction.Unknown;

        await Assert.That(usage!.Message).Contains("gg flights [--all]")
            .Because("the flag exists so the old answer is still reachable, and a "
                   + "reachable answer nobody is told about is not reachable.");
    }

    [Test]
    public async Task Every_row_says_how_the_flight_stands()
    {
        var rendered = VerbOutput.ToText(
            new VerbResult.Flights(new FlightList
            {
                Flights = [AFlight("GG-1", FlightStates.Open), AFlight("GG-2", FlightStates.Landed)],
            }));

        await Assert.That(rendered).Contains(FlightStates.Open);
        await Assert.That(rendered).Contains(FlightStates.Landed);
        await Assert.That(rendered).Contains("GG-1");
    }

    [Test]
    public async Task Unknown_renders_rather_than_being_tidied_away()
    {
        var rendered = VerbOutput.ToText(
            new VerbResult.Flights(new FlightList
            {
                Flights = [AFlight("GG-9", FlightStates.Unknown)],
            }));

        await Assert.That(rendered).Contains(FlightStates.Unknown)
            .Because("a flight nobody can account for is exactly what somebody should see, "
                   + "and a queue that hid it would read as health.");
    }

    [Test]
    public async Task A_state_nothing_can_render_halts_rather_than_being_shown_as_one_that_can()
    {
        // ARTICLE XI, and RunnerStates' throw one noun over. The plausible
        // default here is `open`, and showing a flight that ended in a way this
        // build does not understand as one somebody is still working on is
        // precisely the confusion the vocabulary exists to remove.
        var halting = Assert.Throws<InvalidOperationException>(() =>
            VerbOutput.ToText(
                new VerbResult.Flights(new FlightList
                {
                    Flights = [AFlight("GG-5", "sort-of-finished")],
                })));

        await Assert.That(halting!.Message).Contains("sort-of-finished");
        await Assert.That(halting.Message).Contains("open")
            .Because("the message names the guess it is refusing to make, which is the one "
                   + "somebody would otherwise reach for.");
    }

    [Test]
    public async Task An_empty_queue_says_so_rather_than_printing_nothing()
    {
        // Nothing found and nothing printed look identical in a terminal, and
        // this slice makes an empty queue the ORDINARY outcome rather than a
        // rare one - so the difference matters more than it used to.
        var rendered = VerbOutput.ToText(
            new VerbResult.Flights(new FlightList { Flights = [] }));

        await Assert.That(rendered).Contains("No flights.");
    }
}
