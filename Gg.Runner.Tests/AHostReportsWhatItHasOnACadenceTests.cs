using Gg.Contracts;
using Gg.Local;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// The host's report of its environment instances is taken no more often than
/// it can have changed, and says nothing when this machine hosts none.
/// </summary>
/// <remarks>
/// <para>
/// <b>The reporter is where the local shape and the wire shape meet</b>, which
/// is the division <c>MachineReporter</c> describes: <c>Gg.Local</c> cannot
/// reference the wire contract, so a scan of the disk and a record that crosses
/// are separate by construction and exactly one file maps between them.
/// </para>
/// <para>
/// <b>Null and empty stay different all the way across.</b> The scan's null is
/// "this machine hosts no environments" and must buy no request at all — it is
/// every developer's Mac and every member container. The scan's empty list is a
/// host that has the root and nothing in it, which is a real statement and the
/// only thing that can ever retire a dead instance. A reporter that treated
/// both as "nothing to say" — which is what <c>MachineReporter</c> correctly
/// does with its four absent figures — would leave a lost slot grantable for
/// ever.
/// </para>
/// </remarks>
public class AHostReportsWhatItHasOnACadenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 7, 0, 0, TimeSpan.Zero);

    private static EnvironmentReporter Reporting(
        Func<IReadOnlyList<SeenInstance>?> scan, TimeSpan? cadence = null) =>
        new(_ => scan(), cadence ?? EnvironmentReporter.Cadence);

    [Test]
    public async Task What_the_host_has_becomes_a_reading()
    {
        var reporter = Reporting(() =>
            [new SeenInstance { Environment = "ui", Instance = "gg-env-1" }]);

        var reading = reporter.Read(Now);

        await Assert.That(reading).IsNotNull();
        await Assert.That(reading!.MeasuredAt).IsEqualTo(Now);
        await Assert.That(reading.Instances.Single().Instance).IsEqualTo("gg-env-1");
        await Assert.That(reading.Instances.Single().Environment).IsEqualTo("ui");
    }

    [Test]
    public async Task A_machine_that_hosts_no_environments_reports_nothing()
    {
        // NULL FROM THE SCAN, AND NO REQUEST. Every developer's Mac is here, and
        // a post every cadence to say "not my business" is a request nobody
        // asked for on every machine in the fleet.
        await Assert.That(Reporting(() => null).Read(Now)).IsNull();
    }

    [Test]
    public async Task A_host_with_no_slots_left_reports_that_it_has_none()
    {
        // THE DISTINCTION THAT MAKES A TEARDOWN POSSIBLE, and the one place it
        // would be easy to lose: an empty list is what retires whatever this
        // host used to have. MachineReporter's "every figure absent, so say
        // nothing" is correct there and would be a defect here.
        var reading = Reporting(() => []).Read(Now);

        await Assert.That(reading).IsNotNull()
            .Because("a host that lost every slot has to be able to say so, or the slots stay "
                   + "grantable and every flight sent to one fails at a socket.");
        await Assert.That(reading!.Instances).IsEmpty();
    }

    [Test]
    public async Task It_is_not_read_again_until_the_cadence_is_up()
    {
        var looks = 0;
        var reporter = Reporting(
            () => { looks++; return []; }, cadence: TimeSpan.FromSeconds(30));

        _ = reporter.Read(Now);
        _ = reporter.Read(Now + TimeSpan.FromSeconds(29));

        await Assert.That(looks).IsEqualTo(1)
            .Because("a walk of every slot's disk per beat is a walk per second, and a set of "
                   + "UNIX users does not change that fast.");

        _ = reporter.Read(Now + TimeSpan.FromSeconds(30));

        await Assert.That(looks).IsEqualTo(2);
    }

    [Test]
    public async Task A_scan_that_throws_stands_nothing_down()
    {
        // A DISK THAT CANNOT BE READ IS NOT A RUNNER THAT SHOULD STOP. The
        // report is bookkeeping; the loop's own guard catches what escapes, and
        // this keeps a permissions problem on one host from being an outage.
        await Assert.That(Reporting(() => throw new IOException("the disk went away"))
            .Read(Now)).IsNull();
    }
}
