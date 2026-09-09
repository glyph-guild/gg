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
    public async Task The_line_says_how_an_offer_is_taken_rather_than_only_that_it_may_be()
    {
        // `accept-unattended` used to be the other half of this and is gone: it
        // was a switch for a pool member, and a pool member has no file to set
        // it in. So a machine that accepts offers accepts them ONE WAY, and the
        // line names the way rather than leaving somebody to wonder when.
        var text = VerbOutput.ToText(
            new VerbResult.ConfigShown(Shown(new Configuration { AcceptOffered = true })));

        await Assert.That(text).Contains("gg config accept", StringComparison.Ordinal)
            .Because("a machine that will accept an offer should say what makes it happen.");
    }

    [Test]
    public async Task No_file_is_the_same_answer_as_a_file_that_says_nothing()
    {
        // A machine with no configuration at all accepts nothing. The default
        // has to be off in both readings, or "I have not configured this yet"
        // would mean something different from "I configured it to refuse".
        await Assert.That(Shown(null).AcceptsOffered).IsFalse();
    }
}
