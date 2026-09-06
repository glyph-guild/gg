using System.Diagnostics;
using Gg.Client;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// `y` is `n` with the terminal handed over: the same prompt, a flight opened
/// on this machine, and the person flies it here.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both doc comments said `the selected flight' and the machinery says
/// otherwise.</b> <c>ConsoleHandFlight.StartInfoFor</c> spawns
/// <c>gg fly &lt;intent&gt; --hand</c>, which opens a new flight - so the key
/// could never have flown a flight that already existed. The prompt is the
/// same editor <c>n new flight</c> opens; what differs is where the work
/// happens, and that is a choice a person makes before they press rather than
/// after.
/// </para>
/// <para>
/// <b>The refusal is computed HERE and the order is the feature.</b> The child
/// would check too, and its answer would be printed to a terminal this console
/// redraws over a second later - so a machine that cannot serve the flight has
/// to be told before anything is created and before anybody is asked to type.
/// <c>HandRefusal.For</c> is the same function the CLI verb uses; a second
/// containment check here would be the second evaluator this design forbids,
/// one process further out.
/// </para>
/// </remarks>
public class FlyingByHandFromTheConsoleTests
{
    private static readonly SelfInvocation Self = new("/usr/local/bin/gg", []);

    /// <summary>The same shape FlyByHandCommandTests builds, for the same check.</summary>
    private static Checklist Needing(params string[] labels) => new()
    {
        EnvelopeVersion = "v6",
        RequiredLabels = labels,
        Items =
        [
            .. labels.Select(label => new ChecklistItem
            {
                Requirement = label,
                Verification = "a runner advertises it",
                Satisfier = ChecklistSatisfiers.MatchingRunner,
                Disposition = LabelDispositions.Stated,
            }),
        ],
    };

    [Test]
    public async Task A_machine_that_cannot_serve_it_creates_nothing_and_asks_nothing()
    {
        var asked = false;
        var started = new List<ProcessStartInfo>();

        var after = ConsoleHandFlight.Fly(
            new AppState(),
            plan: () => Needing("environment=aspire-payments"),
            advertised: ["linux"],
            ask: () => { asked = true; return "something"; },
            self: Self,
            start: info => { started.Add(info); return 0; });

        await Assert.That(asked).IsFalse()
            .Because("refusing after somebody has typed a paragraph is the same refusal and "
                   + "an insult.");
        await Assert.That(started).IsEmpty()
            .Because("a flight created and then abandoned because this laptop was wrong is "
                   + "litter with a number on it.");
        await Assert.That(after.LastHandFlight).IsNotNull();
        await Assert.That(after.LastHandFlight!).Contains("environment=aspire-payments")
            .Because("the label first, because it is the actionable half.");
    }

    [Test]
    public async Task Nothing_typed_opens_nothing()
    {
        var started = new List<ProcessStartInfo>();

        var after = ConsoleHandFlight.Fly(
            new AppState(),
            plan: () => Needing(),
            advertised: [],
            ask: () => "   ",
            self: Self,
            start: info => { started.Add(info); return 0; });

        await Assert.That(started).IsEmpty();
        await Assert.That(after.LastHandFlight!).Contains("Nothing was")
            .Because("a flight opened by accident is a record somebody has to explain and a "
                   + "number that is now taken.");
    }

    [Test]
    public async Task What_a_person_typed_is_what_the_child_is_told_to_fly()
    {
        var started = new List<ProcessStartInfo>();

        var after = ConsoleHandFlight.Fly(
            new AppState(),
            plan: () => Needing("linux"),
            advertised: ["linux", "gpu"],
            ask: () => "  make the report say who was idle  ",
            self: Self,
            start: info => { started.Add(info); return 0; });

        await Assert.That(started).Count().IsEqualTo(1);
        await Assert.That(started[0].ArgumentList).Contains("make the report say who was idle")
            .Because("trimmed, and otherwise exactly what was written.");
        await Assert.That(started[0].ArgumentList).Contains("--hand");
        await Assert.That(started[0].RedirectStandardInput).IsFalse()
            .Because("a person is at the keyboard and the child owns the screen until it "
                   + "exits.");

        await Assert.That(after.LastHandFlight!).Contains("flown by hand")
            .Because("and the console says so afterwards, because the child's own output is "
                   + "about to be redrawn over.");
    }

    [Test]
    public async Task A_machine_advertising_more_than_the_flight_asks_for_is_still_eligible()
    {
        // CONTAINMENT, THE SAME DIRECTION THE MATCHER RUNS IT. Reversing it
        // refuses every machine that had anything extra, which is every machine.
        var started = new List<ProcessStartInfo>();

        ConsoleHandFlight.Fly(
            new AppState(),
            plan: () => Needing("linux"),
            advertised: ["linux", "gpu", "environment=aspire-payments"],
            ask: () => "work",
            self: Self,
            start: info => { started.Add(info); return 0; });

        await Assert.That(started).Count().IsEqualTo(1);
    }

    [Test]
    public async Task A_child_that_failed_says_so_rather_than_claiming_a_flight()
    {
        var after = ConsoleHandFlight.Fly(
            new AppState(),
            plan: () => Needing(),
            advertised: [],
            ask: () => "work",
            self: Self,
            start: _ => 1);

        await Assert.That(after.LastHandFlight!).DoesNotContain("flown by hand")
            .Because("the console redraws over whatever the child printed, so a line claiming "
                   + "success over a child that failed is the only record left.");
    }

    [Test]
    public async Task A_plan_that_could_not_be_read_costs_one_read_rather_than_the_console()
    {
        var before = new AppState { Principal = "somebody", SelectedRow = 3 };

        var after = ConsoleHandFlight.Fly(
            before,
            plan: () => throw new HttpRequestException("the control plane is unreachable"),
            advertised: [],
            ask: () => throw new InvalidOperationException("nobody should be asked."),
            self: Self,
            start: _ => throw new InvalidOperationException("nothing should be started."));

        await Assert.That(after.Principal).IsEqualTo("somebody");
        await Assert.That(after.SelectedRow).IsEqualTo(3);
        await Assert.That(after.LastHandFlight!).Contains("unreachable");
    }
}
