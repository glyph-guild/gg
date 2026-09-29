using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// A host reads which environment instances it has off the disk they live on.
/// </summary>
/// <remarks>
/// <para>
/// <b>good-grief#617, and the owner's answer to it (2026-09-29).</b>
/// <c>scheduling.gg_environment_instance</c> had every reader shipped and no
/// writer at all: the claim's pick predicate, the anti-join that takes an
/// instance and the lease that names it to a runner are real, and nothing ever
/// recorded that an instance existed. So a work kind declaring <c>hosts:</c>
/// produced a flight stepped over for ever. Three answers were possible — a
/// pool attestation, a line in the airspace document, or the host saying what it
/// has — and the host won on the mint's own reasoning: it is the only party
/// whose word about this can be trusted.
/// </para>
/// <para>
/// <b>Which makes the shape of an instance the runbook's, not ADR-0034
/// Decision 2's.</b> An instance is a UNIX user with its own rootless daemon,
/// made once when the host is built. What is warm from a pin is what runs
/// INSIDE that daemon.
/// </para>
/// </remarks>
public class AHostSeesTheInstancesItHasTests
{
    private static string ASlot(string root, string instance, string? environment, bool socket)
    {
        var home = Path.Combine(root, instance);
        Directory.CreateDirectory(home);

        if (environment is not null)
        {
            File.WriteAllText(
                Path.Combine(home, EnvironmentSlotScan.EnvironmentFile), environment + "\n");
        }

        if (socket)
        {
            Directory.CreateDirectory(Path.Combine(home, "run"));
            File.WriteAllText(Path.Combine(home, EnvironmentSlotScan.SocketPath), "");
        }

        return home;
    }

    private static string ARoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "gg-env-scan-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        return root;
    }

    [Test]
    public async Task A_slot_that_says_what_it_serves_and_can_be_reached_is_seen()
    {
        var root = ARoot();
        ASlot(root, "gg-env-1", "ui", socket: true);

        var seen = new EnvironmentSlotScan(root).Read();

        await Assert.That(seen).IsNotNull();
        await Assert.That(seen!.Count).IsEqualTo(1);
        await Assert.That(seen[0].Instance).IsEqualTo("gg-env-1");
        await Assert.That(seen[0].Environment).IsEqualTo("ui")
            .Because("the socket is named after the instance and the grant is made against "
                   + "the environment, so a report carrying one of the two answers nothing.");

        Directory.Delete(root, recursive: true);
    }

    [Test]
    public async Task A_slot_nobody_said_the_purpose_of_is_not_reported()
    {
        // THE RUNBOOK MAKES gg-env-1, gg-env-2, and those names say nothing
        // about `ui`. Guessing from the name would be this code deciding what an
        // operator meant; an unclaimed slot is the honest answer.
        var root = ARoot();
        ASlot(root, "gg-env-1", environment: null, socket: true);

        await Assert.That(new EnvironmentSlotScan(root).Read()).IsEmpty();

        Directory.Delete(root, recursive: true);
    }

    [Test]
    public async Task A_slot_whose_daemon_never_came_up_is_not_reported()
    {
        // A slot with no socket is one a flight cannot stand a stack in, and
        // reporting it hands somebody a name that resolves to nothing. This is
        // the case that is knowable here and cheap: made, and never finished.
        var root = ARoot();
        ASlot(root, "gg-env-1", "ui", socket: false);

        await Assert.That(new EnvironmentSlotScan(root).Read()).IsEmpty();

        Directory.Delete(root, recursive: true);
    }

    [Test]
    public async Task A_host_that_hosts_no_environments_says_nothing_at_all()
    {
        // NULL, AND IT IS NOT THE SAME AS EMPTY. Every developer's Mac and every
        // member container has no root, and none of them should buy a request to
        // report that.
        await Assert.That(new EnvironmentSlotScan(
            Path.Combine(Path.GetTempPath(), "gg-env-absent-" + Guid.NewGuid().ToString("n")))
            .Read()).IsNull();
    }

    [Test]
    public async Task A_host_with_the_root_and_no_slots_says_it_has_none()
    {
        // THE OTHER HALF OF THAT DISTINCTION, and the one that makes a teardown
        // possible at all. A host that HAS the root and nothing in it is making
        // a statement - it retires whatever it used to have. Collapsed into
        // null, a host that lost every slot would be indistinguishable from one
        // that was never in this business, and the slots would stay grantable
        // for ever.
        var root = ARoot();

        var seen = new EnvironmentSlotScan(root).Read();

        await Assert.That(seen).IsNotNull()
            .Because("null is 'not my business' and empty is 'I have none', and only the "
                   + "second one can ever take a dead instance out of the pool.");
        await Assert.That(seen).IsEmpty();

        Directory.Delete(root, recursive: true);
    }

    [Test]
    public async Task Several_slots_come_back_in_a_stable_order()
    {
        var root = ARoot();
        ASlot(root, "gg-env-2", "ui", socket: true);
        ASlot(root, "gg-env-1", "ui", socket: true);
        ASlot(root, "gg-env-3", "api", socket: true);

        var seen = new EnvironmentSlotScan(root).Read()!;

        await Assert.That(seen.Select(s => s.Instance).ToList())
            .IsEquivalentTo((string[])["gg-env-1", "gg-env-2", "gg-env-3"]);
        await Assert.That(seen.Single(s => s.Instance == "gg-env-3").Environment)
            .IsEqualTo("api")
            .Because("one host may serve two environments, and a report that assumed one "
                   + "would make that an unstated limit.");

        Directory.Delete(root, recursive: true);
    }

    [Test]
    public async Task The_declaration_is_trimmed_rather_than_taken_whole()
    {
        // An operator writes this with `tee`, and a trailing newline is what
        // `echo` leaves. "ui\n" matching no charted environment would be a slot
        // refused for a whitespace character.
        var root = ARoot();
        var home = ASlot(root, "gg-env-1", environment: null, socket: true);
        File.WriteAllText(
            Path.Combine(home, EnvironmentSlotScan.EnvironmentFile), "  ui  \n\nsomething else\n");

        var seen = new EnvironmentSlotScan(root).Read()!;

        await Assert.That(seen.Single().Environment).IsEqualTo("ui");

        Directory.Delete(root, recursive: true);
    }
}
