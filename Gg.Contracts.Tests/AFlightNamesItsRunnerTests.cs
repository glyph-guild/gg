using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A flight can name the runner it is for.
/// </summary>
/// <remarks>
/// <para>
/// <b>The other half of reserving, and the half that makes it usable.</b> A
/// reservation says whose work a runner takes; direction says which machine a
/// piece of work is for. Without it, reserving a laptop means its holder's work
/// goes there — and nothing else can be sent there deliberately.
/// </para>
/// <para>
/// <b>The runner still PULLS.</b> "Push" here is a person choosing a machine,
/// not the control plane opening a connection to one: the flight is narrowed,
/// and the runner it names claims it the same way it claims anything. Rule 4 of
/// <c>docs/patterns.md</c> is untouched.
/// </para>
/// <para>
/// <b>An id, not a label.</b> A label says what a machine can do and several may
/// answer to it; direction names one machine. Spelling it as a label would make
/// "this one" unsayable, which is the thing being added.
/// </para>
/// <para>
/// <b>Beside <c>Environment</c> and <c>Repository</c>, which are the two
/// selections a flight already declares.</b> Null inherits, exactly as they do —
/// and here inheriting means "any runner that may take it".
/// </para>
/// </remarks>
public class AFlightNamesItsRunnerTests
{
    private static FlightLaunchRequest ALaunch(string? runner) => new()
    {
        Name = "for one machine",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "work" },
        Runner = runner,
    };

    [Test]
    public async Task A_flight_can_name_the_runner_it_is_for()
    {
        await Assert.That(ALaunch("01a06b0c-0000-7000-8000-000000000000").Runner)
            .IsEqualTo("01a06b0c-0000-7000-8000-000000000000");
    }

    [Test]
    public async Task Naming_no_runner_is_the_ordinary_case()
    {
        // THE ANCHOR, and it is nearly every flight. Null inherits - any runner
        // that may take it - exactly as Environment and Repository do.
        await Assert.That(ALaunch(null).Runner).IsNull();
        await Assert.That(new FlightLaunchRequest
        {
            Name = "for anyone",
            Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "work" },
        }.Runner).IsNull();
    }

    [Test]
    public async Task It_sits_beside_the_selections_a_flight_already_declares()
    {
        // Environment, Repository, Runner: where it runs, what it is about, and
        // which machine. One shape for three narrowings a person can state.
        var members = typeof(FlightLaunchRequest).GetProperties().Select(p => p.Name).ToList();

        await Assert.That(members).Contains("Environment");
        await Assert.That(members).Contains("Repository");
        await Assert.That(members).Contains("Runner");
    }
}
