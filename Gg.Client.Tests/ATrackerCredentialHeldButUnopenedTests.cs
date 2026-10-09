using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A tracker credential this machine holds and cannot open, and the two
/// sentences that sent a person to the wrong place before it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on a developer's machine.</b> <c>gg credential add --tracker ado</c>
/// sealed the token to its person and filed it at <c>local:tracker/ado</c>; the
/// tracker declared on that machine read <c>local:hrtms/jdx</c>, so nothing used
/// it, and nothing said so. Pointed at the new locator, <c>gg doctor</c> then died
/// with an unhandled exception out of this check - the store's own sentence,
/// which says exactly what to run, reached the person only as a stack trace.
/// </para>
/// <para>
/// <b>And the locator it read was the doctor's own advice.</b> A lacking tracker
/// credential was fixed with <c>gg credential add --repo &lt;slug&gt;</c>, which
/// files a tracker's token at a repository's locator - exactly the shape the
/// "no tracker is declared" branch of this check already stopped recommending.
/// </para>
/// </remarks>
public class ATrackerCredentialHeldButUnopenedTests
{
    private const string Host = "https://tracker.example/acme";

    private static MachineRole Declaring(params DeclaredTracker[] trackers) =>
        MachineRole.None with { Trackers = trackers };

    [Test]
    public async Task A_credential_sealed_to_somebody_else_is_a_finding_not_a_crash()
    {
        var check = Doctor.TrackerCheck(
            Declaring(new DeclaredTracker
            {
                Key = "board", Host = Host, Locator = "local:tracker/board",
            }),
            _ => throw new CredentialUnavailableException(
                "The credential at 'local:tracker/board' is on this machine and will not "
              + "open here."));

        await Assert.That(check.Passed).IsFalse();
        await Assert.That(check.Detail).Contains("local:tracker/board");
        await Assert.That(check.Fix).IsNotNull();
        await Assert.That(check.Fix!).Contains("gg credential trust-this-machine local:tracker/board")
            .Because("the store already knows the one command that fixes this, and a check "
                   + "that drops it sends a person to re-add a secret they are holding.");
        await Assert.That(check.Blocking).IsFalse();
    }

    [Test]
    public async Task A_lacking_tracker_credential_is_added_as_a_tracker()
    {
        var check = Doctor.TrackerCheck(
            Declaring(new DeclaredTracker
            {
                Key = "board", Host = Host, Locator = "local:tracker/board",
            }),
            _ => true);

        await Assert.That(check.Fix!).Contains("gg credential add --tracker board");
        await Assert.That(check.Fix!).DoesNotContain("--repo")
            .Because("a tracker's token filed at a repository's locator is the misfiling "
                   + "this check exists to find.");
    }

    [Test]
    public async Task A_tracker_reading_a_repository_shaped_locator_is_told_where_add_writes()
    {
        var check = Doctor.TrackerCheck(
            Declaring(new DeclaredTracker
            {
                Key = "board", Host = Host, Locator = "local:acme/board",
            }),
            _ => true);

        // `--tracker board` files at local:tracker/board, so following the add alone
        // stores a credential this declaration still does not read.
        await Assert.That(check.Fix!).Contains("local:tracker/board");
        await Assert.That(check.Fix!).Contains("intent-hosts");
    }

    [Test]
    public async Task Adding_for_a_tracker_this_machine_reads_elsewhere_says_so()
    {
        var notice = CredentialCommands.TrackerNotice(
            "board",
            [new DeclaredTracker { Key = "board", Host = Host, Locator = "local:acme/board" }]);

        await Assert.That(notice).IsNotNull();
        await Assert.That(notice!).Contains("local:acme/board");
        await Assert.That(notice!).Contains("local:tracker/board");
        await Assert.That(notice!).Contains("intent-hosts")
            .Because("a credential stored where nothing reads is a secret typed for nothing, "
                   + "and the add reported success.");
    }

    [Test]
    public async Task And_names_the_step_that_lets_this_machine_open_it()
    {
        var notice = CredentialCommands.TrackerNotice(
            "board",
            [new DeclaredTracker { Key = "board", Host = Host, Locator = "local:tracker/board" }]);

        await Assert.That(notice).IsNotNull();
        await Assert.That(notice!).Contains("gg credential trust-this-machine local:tracker/board")
            .Because("the add seals to its person, and this machine's reader opens it as the "
                   + "machine - without this step the reader cannot use what was just added.");
        await Assert.That(notice!).DoesNotContain("intent-hosts");
    }

    [Test]
    public async Task And_says_nothing_about_a_tracker_this_machine_does_not_declare()
    {
        var notice = CredentialCommands.TrackerNotice(
            "board",
            [new DeclaredTracker { Key = "other", Host = Host, Locator = "local:tracker/other" }]);

        await Assert.That(notice).IsNull();
    }
}
