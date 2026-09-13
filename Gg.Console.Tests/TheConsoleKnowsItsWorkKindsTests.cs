using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The names a tenant declared are known at boot, not after somebody goes looking.
/// </summary>
/// <remarks>
/// <para>
/// <b>They are filled by one command and one command only.</b>
/// <c>Estate.Names</c> comes from <c>ConsoleEstate.Read</c>, which is wired to
/// the envelope port and reached by pressing <c>v</c>. So a console that has
/// been open all morning knows nothing about the tenant's work kinds unless
/// somebody happened to look at the Envelope tab.
/// </para>
/// <para>
/// <b>That is the difference between a feature and a dead end.</b> A question
/// asked at fly time can only offer what the model holds; offering nothing, or
/// sending a person away to press another key first, is the same as not having
/// the feature - and worse, because it looks like it should work.
/// </para>
/// <para>
/// <b>Boot already reads seven things in parallel</b> - the fleet, the queue,
/// gates, credentials, notices, allowances and a health report - and several are
/// wrapped so that a failure costs only what they answer. One more read of the
/// same shape is what makes the names always there, and a failure costs the
/// names rather than the console.
/// </para>
/// </remarks>
public class TheConsoleKnowsItsWorkKindsTests
{
    private static EnvelopeTopology ATopology() => new()
    {
        Names =
        [
            new TopologyName
            {
                Name = "root",
                Role = Roles.Root,
                DeclaredBy = "kdee",
                DeclaredAt = DateTimeOffset.UnixEpoch,
            },
            new TopologyName
            {
                Name = "hal-score",
                Role = Roles.WorkKind,
                Parent = "root",
                DeclaredBy = "kdee",
                DeclaredAt = DateTimeOffset.UnixEpoch,
            },
        ],
    };

    [Test]
    public async Task A_topology_that_was_read_names_the_work_kinds()
    {
        var state = new AppState
        {
            Estate = new EstateOnThisMachine { Uncommitted = [], Names = ATopology() },
        };

        await Assert.That(WorkKinds.Declared(state)).IsEquivalentTo(new List<string> { "hal-score" })
            .Because("a flight is FOR one thing, and only a work-kind layer supplies that - "
                   + "root is the floor every kind narrows and is not a choice.");
    }

    [Test]
    public async Task Nothing_read_is_no_kinds_rather_than_an_invented_one()
    {
        await Assert.That(WorkKinds.Declared(new AppState())).IsEmpty()
            .Because("absent must stay absent: the control plane reads a missing kind as "
                   + "implement, and a console that supplied that name locally would be "
                   + "declaring something nobody chose.");
    }

    [Test]
    public async Task The_boot_asks_for_the_topology_like_everything_else_it_asks_for()
    {
        // THE HALF THAT WAS MISSING ENTIRELY. The test above passes on a state
        // somebody hand-built; this asserts the console actually fills it,
        // which is the difference between a feature and a field nothing
        // assigns - the shape this repository has a ratchet for.
        var start = Sources.Read("Gg.Console", "ConsoleStart.cs");

        await Assert.That(start).Contains("TopologyAsync")
            .Because("boot is where the other seven reads are, and the names have to be "
                   + "there before the first flight is opened rather than after somebody "
                   + "presses v.");
    }
}
