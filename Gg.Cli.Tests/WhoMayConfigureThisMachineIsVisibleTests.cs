using Gg.Client;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// Whether something else may configure this machine, said out loud.
/// </summary>
/// <remarks>
/// <para>
/// <b>A permission nobody can see is a permission somebody forgot they
/// granted.</b> <c>accept-offered</c> decides whether a control plane may change
/// what this machine does, and it arrived invisible: it has no environment
/// variable by design, so it is not on the page that lists settings, and nothing
/// else mentioned it. The one setting most worth reading before trusting a
/// machine was the one setting no surface named.
/// </para>
/// <para>
/// <b>Said even when it is off</b>, which is the harder half. "No" is the
/// answer an operator needs to confirm rather than assume, and a line that
/// appears only when something is switched on cannot be used to check that
/// nothing is.
/// </para>
/// </remarks>
public class WhoMayConfigureThisMachineIsVisibleTests
{
    private static ConfigurationView Shown(Configuration? file) =>
        ((VerbResult.ConfigShown)ConfigCommands.Show(
            ConsoleEnvironment.Read(file, environment: _ => null),
            path: "/somewhere/config.json",
            file: file)).Value;

    [Test]
    public async Task A_machine_that_accepts_nothing_says_so()
    {
        var view = Shown(new Configuration());

        await Assert.That(view.AcceptsOffered).IsFalse();

        var text = VerbOutput.ToText(new VerbResult.ConfigShown(view));

        await Assert.That(text).Contains("offered", StringComparison.OrdinalIgnoreCase)
            .Because("an operator confirming that nothing else may configure this machine "
                   + "needs a line saying so, not an absence to interpret.");
    }

    [Test]
    public async Task A_machine_that_accepts_offers_says_that_too()
    {
        var view = Shown(new Configuration { AcceptOffered = true });

        await Assert.That(view.AcceptsOffered).IsTrue();

        var text = VerbOutput.ToText(new VerbResult.ConfigShown(view));

        await Assert.That(text).Contains("control plane", StringComparison.OrdinalIgnoreCase)
            .Because("the sentence should name who may configure this machine, not just "
                   + "report a flag by its key.");
    }

    [Test]
    public async Task Unattended_is_only_meaningful_once_offers_are_accepted_at_all()
    {
        // Turning on the second without the first does nothing, and a surface
        // that showed it as live would say a machine applies offers when it
        // refuses every one.
        var view = Shown(new Configuration { AcceptUnattended = true });

        await Assert.That(view.AcceptsOffered).IsFalse();
        await Assert.That(view.AppliesUnattended).IsFalse()
            .Because("nothing is accepted at all, so nothing is applied unattended either.");
    }

    [Test]
    public async Task Both_together_are_the_only_way_a_machine_applies_one_on_its_own()
    {
        var view = Shown(new Configuration { AcceptOffered = true, AcceptUnattended = true });

        await Assert.That(view.AppliesUnattended).IsTrue();
    }

    [Test]
    public async Task No_file_is_the_same_answer_as_a_file_that_says_nothing()
    {
        // A machine with no configuration at all accepts nothing. The default
        // has to be off in both readings, or "I have not configured this yet"
        // would mean something different from "I configured it to refuse".
        await Assert.That(Shown(null).AcceptsOffered).IsFalse();
        await Assert.That(Shown(null).AppliesUnattended).IsFalse();
    }
}
