using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Anywhere gg takes a runner id, it takes the machine's name too.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because nobody knows a uuid.</b> Every verb that acts on a machine wanted
/// <c>01a0dc18-9482-74c9-82c0-0953ec110c04</c>, and the only way to get one was
/// to list the fleet and copy it - so the ordinary way to run <c>gg agent login</c> was
/// two commands, the first of which existed only to feed the second.
/// </para>
/// <para>
/// <b>A uuid is still a uuid, and costs no read.</b> Scripts pass ids and
/// should not start paying for a fleet listing; a value shaped like an id is
/// used as one without asking anybody.
/// </para>
/// <para>
/// <b>Ambiguity is refused, never guessed.</b> Labels are what a machine calls
/// itself and nothing makes them unique. Half these verbs are destructive -
/// <c>retire</c>, <c>release</c>, <c>unclaim</c> - so a name matching two
/// machines has to stop, and stop while naming both.
/// </para>
/// </remarks>
public class ARunnerCanBeNamedTests
{
    private const string Vmlinux = "01a0dc18-9482-74c9-82c0-0953ec110c04";
    private const string Mac = "9f2b77e1-0000-4c1a-8a55-7bd2c6e41111";

    private static RunnerSummary A(string id, string label) => new()
    {
        RunnerId = id,
        Label = label,
        State = "idle",
    };

    private static readonly IReadOnlyList<RunnerSummary> Fleet =
    [
        A(Vmlinux, "vmlinux001"),
        A(Mac, "kevins-mac"),
    ];

    // ---- an id is an id ----

    [Test]
    public async Task A_uuid_is_taken_as_written()
    {
        await Assert.That(RunnerNamed.LooksLikeAnId(Vmlinux)).IsTrue();

        // AND NOTHING IS READ TO ESTABLISH IT. A script that already holds an
        // id would otherwise start paying for a fleet listing on every verb,
        // and the listing could fail for a value that never needed it.
        await Assert.That(RunnerNamed.LooksLikeAnId("vmlinux001")).IsFalse();
    }

    // ---- a name is resolved ----

    [Test]
    public async Task A_name_finds_its_machine()
    {
        var found = RunnerNamed.Resolve("vmlinux001", Fleet);

        await Assert.That(found.RunnerId).IsEqualTo(Vmlinux);
    }

    [Test]
    public async Task And_case_is_not_something_to_get_right()
    {
        // A MACHINE NAME IS TYPED FROM MEMORY, and a refusal over a capital is
        // a refusal about nothing. Exact otherwise: no prefixes, because half
        // the verbs that take this are destructive and `vm' should never reach
        // for the only machine starting with it.
        await Assert.That(RunnerNamed.Resolve("VMLinux001", Fleet).RunnerId).IsEqualTo(Vmlinux);

        await Assert.That(RunnerNamed.Resolve("vmlinux", Fleet).RunnerId).IsNull()
            .Because("a prefix is not a name, and retire is one of the verbs that takes one.");
    }

    // ---- and refused when it cannot be ----

    [Test]
    public async Task A_name_nothing_answers_to_says_what_there_is()
    {
        var missing = RunnerNamed.Resolve("vmlinux002", Fleet);

        await Assert.That(missing.RunnerId).IsNull();

        // THE FLEET IS THE REMEDY. "No such runner" sends somebody to another
        // command to find out what they should have typed; the names are
        // already in hand here.
        await Assert.That(missing.Said).Contains("vmlinux001");
        await Assert.That(missing.Said).Contains("kevins-mac");
    }

    [Test]
    public async Task Two_machines_of_one_name_stop_it_and_name_both()
    {
        var twins = new[] { A(Vmlinux, "runner"), A(Mac, "runner") };

        var several = RunnerNamed.Resolve("runner", twins);

        await Assert.That(several.RunnerId).IsNull()
            .Because("choosing for somebody here retires the wrong machine half the time.");

        // BOTH IDS, because the way out of this is to name one of them and the
        // id is the only thing that tells them apart.
        await Assert.That(several.Said).Contains(Vmlinux);
        await Assert.That(several.Said).Contains(Mac);
    }

    [Test]
    public async Task And_an_empty_fleet_says_that_rather_than_listing_nothing()
    {
        var nothing = RunnerNamed.Resolve("vmlinux001", []);

        await Assert.That(nothing.RunnerId).IsNull();
        await Assert.That(nothing.Said).Contains("no machines")
            .Because("a list of nothing reads as the name being wrong, and the tenant having "
                   + "no fleet is a different thing to go and fix.");
    }
}
