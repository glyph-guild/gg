using Gg.Contracts;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// Whether a machine sweeps is a setting its operator owns: on unless they
/// said otherwise, and nothing the control plane can switch.
/// </summary>
/// <remarks>
/// <para>
/// <b>The owner's words:</b> <i>"we should be able to turn off the runner
/// automated sweep as well. it's on by default."</i>
/// </para>
/// <para>
/// <b>A variable and a file key, like every runner setting beside it</b> - so a
/// systemd unit can carry it the way vmlinux001 carries its labels, and a
/// person can write it with <c>gg config set</c>.
/// </para>
/// <para>
/// <b>The default is written down, not implied.</b> <c>ResidentSweeps</c>
/// already reads unset as on, and that alone would leave <c>gg config</c>
/// showing a blank for a setting that is actively deciding something. A default
/// in the table makes the page say <i>on, by default</i> - which is "every
/// absence is said rather than blank" applied to the machine's own settings.
/// </para>
/// <para>
/// <b>Not offerable.</b> A sweep spends the machine's allowance. A control
/// plane that could offer <c>runner-sweeps=on</c> could turn sweeping back on
/// for a machine whose operator turned it off - which is the control plane
/// deciding how somebody else's allowance is spent.
/// </para>
/// </remarks>
public class WhetherAMachineSweepsIsItsOperatorsCallTests
{
    [Test]
    public async Task Nobody_saying_anything_means_on_and_the_page_says_so()
    {
        var resolved = Settings.Resolve(
            Gg.Runner.Sweeps.ResidentSweeps.Variable, file: null, environment: _ => null);

        await Assert.That(resolved.Value).IsEqualTo(Gg.Runner.Sweeps.ResidentSweeps.On);
        await Assert.That(resolved.Source).IsEqualTo(SettingSources.Default)
            .Because("on by default, and SAID to be - a blank on the page for a setting that is "
                   + "deciding whether this machine runs an agent is the absence nobody reads.");
    }

    [Test]
    public async Task The_file_can_turn_it_off()
    {
        await Assert.That(Settings.CanBeInTheFile(Gg.Runner.Sweeps.ResidentSweeps.Variable)).IsTrue()
            .Because("a person turns it off with `gg config set runner-sweeps off`, which needs "
                   + "somewhere in the file to put it.");

        var resolved = Settings.Resolve(
            Gg.Runner.Sweeps.ResidentSweeps.Variable,
            new Configuration { RunnerSweeps = "off" },
            environment: _ => null);

        await Assert.That(resolved.Value).IsEqualTo("off");
        await Assert.That(resolved.Source).IsEqualTo(SettingSources.File);
    }

    [Test]
    public async Task A_unit_can_turn_it_off_over_the_file()
    {
        var resolved = Settings.Resolve(
            Gg.Runner.Sweeps.ResidentSweeps.Variable,
            new Configuration { RunnerSweeps = "on" },
            environment: v => v == Gg.Runner.Sweeps.ResidentSweeps.Variable ? "off" : null);

        await Assert.That(resolved.Value).IsEqualTo("off")
            .Because("the environment wins over the file for every runner setting, and a systemd "
                   + "unit is how vmlinux001 is configured.");
    }

    [Test]
    public async Task The_control_plane_cannot_offer_it()
    {
        await Assert.That(OfferableKeys.All).DoesNotContain(Gg.Runner.Sweeps.ResidentSweeps.Key)
            .Because("a sweep spends the machine's allowance, and an offerable key would let the "
                   + "control plane turn sweeping back on for a machine whose operator turned it "
                   + "off.");
    }

    [Test]
    public async Task The_resolved_default_is_a_machine_that_asks()
    {
        // THE WHOLE CHAIN, setting to decision: what an untouched machine
        // resolves is what makes ResidentSweeps ask.
        var setting = Settings.Value(
            Gg.Runner.Sweeps.ResidentSweeps.Variable, file: null, environment: _ => null);

        var claim = Gg.Runner.Sweeps.ResidentSweeps.ClaimFor(
            setting, [new ServedTracker("ado", "https://tracker.example/acme", "local:acme/triage")]);

        await Assert.That(claim).IsNotNull();
    }
}
