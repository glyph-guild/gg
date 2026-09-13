using Gg.Client;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// Whether a person may put a credential on this machine, decided here and said
/// out loud.
/// </summary>
/// <remarks>
/// <para>
/// <b>The door <c>accept-unattended</c>'s gravestone describes, with a key that
/// fits.</b> That setting was retired because <i>a pool member has no file to
/// set it in: DockerPoolAdapter creates members with no binds, so nothing from
/// the host filesystem reaches one. It was a switch for a machine that could not
/// reach it.</i> The answer is not a switch an operator reaches - it is one the
/// MEMBER writes at its own first start, on the authority of the single-use
/// nonce the tenant minted to create it. A laptop opts in by opening its file; a
/// member is opted in by whoever made it.
/// </para>
/// <para>
/// <b>Off unless it is here and true, and not itself offerable</b> - which is
/// <c>accept-offered</c>'s whole safety argument and transfers unchanged: a
/// control plane that could set this would be granting itself the ability to put
/// a secret on the machine. It has no environment variable for the same reason
/// that one does not: a variable is a second way to turn it on, and one a
/// container image or a unit file could carry without anybody reading it.
/// </para>
/// <para>
/// <b>And it is visible even when it is off</b>, which
/// <c>WhoMayConfigureThisMachineIsVisibleTests</c> established for its sibling:
/// <i>a permission nobody can see is a permission somebody forgot they
/// granted</i>, and a line that appears only when something is switched on
/// cannot be used to check that nothing is.
/// </para>
/// </remarks>
public class AMachineDecidesWhetherItMayBeGivenACredentialTests
{
    private static ConfigurationView Shown(Configuration? file) =>
        ((VerbResult.ConfigShown)ConfigCommands.Show(
            ConsoleEnvironment.Read(file, environment: _ => null),
            path: "/somewhere/config.json",
            file: file)).Value;

    [Test]
    public async Task A_machine_is_closed_until_its_file_says_otherwise()
    {
        await Assert.That(new Configuration().AcceptConfigured).IsNull()
            .Because("absent is off. A credential arriving on a machine nobody opened is "
                   + "the standing grant this whole path exists to not be.");
    }

    [Test]
    public async Task It_survives_the_file_it_is_written_to()
    {
        // ROUND TRIP THROUGH THE REAL RENDERER AND PARSER, because a member
        // writes this at first start and reads it back on every restart after.
        // A key the writer spells one way and the reader another is a member
        // that silently closes again the first time it is bounced.
        var parsed = ConfigurationFile.Parse(
            ConfigurationFile.Render(new Configuration { AcceptConfigured = true }));

        await Assert.That(parsed.Configuration!.AcceptConfigured).IsTrue();
    }

    [Test]
    public async Task Nothing_but_the_file_can_turn_it_on()
    {
        // NO VARIABLE, for accept-offered's reason: a variable is a second way
        // to turn it on, and one a container image or a systemd unit could
        // carry without anybody reading it. Opening the file - or being a
        // member whose tenant minted its nonce - is the whole of the friction.
        await Assert.That(Configuration.Members.Any(
                m => string.Equals(m.Key, "accept-configured", StringComparison.Ordinal)
                  && m.Variable is { Length: > 0 }))
            .IsFalse()
            .Because("a setting with a variable is one an image can carry silently.");

        // AND IT CANNOT BE OFFERED, which is the half that matters most: a
        // control plane able to set this would be granting itself the ability
        // to put a secret on the machine.
        await Assert.That(OfferableKeys.All).DoesNotContain("accept-configured");
    }

    [Test]
    public async Task A_machine_that_may_be_given_one_says_so_and_so_does_one_that_may_not()
    {
        // SAID EVEN WHEN IT IS OFF, which is the harder half and the one its
        // sibling's tests had to be written twice to get right.
        var closed = VerbOutput.ToText(new VerbResult.ConfigShown(Shown(new Configuration())));
        var open = VerbOutput.ToText(new VerbResult.ConfigShown(
            Shown(new Configuration { AcceptConfigured = true })));

        await Assert.That(Shown(new Configuration()).AcceptsConfigured).IsFalse();
        await Assert.That(Shown(new Configuration { AcceptConfigured = true }).AcceptsConfigured)
            .IsTrue();

        await Assert.That(closed).Contains("credential", StringComparison.OrdinalIgnoreCase)
            .Because("an operator confirming that nobody can put a secret on this machine "
                   + "needs a line saying so, not an absence to interpret.");
        await Assert.That(open).Contains("credential", StringComparison.OrdinalIgnoreCase);
        await Assert.That(open).IsNotEqualTo(closed);
    }
}
