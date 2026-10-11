using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Opening the broadcast review, or where a credential is, keeps the console on screen:
/// the modal opens at once and the machines fold in when the read lands.
/// </summary>
/// <remarks>
/// <para>
/// <b>Owner, 2026-10-11:</b> <i>"the modal in gg 'who this credential is wanted by' needs
/// to use real controls to allow selection of the runner. it should also not flash back to
/// the console prior to this."</i>
/// </para>
/// <para>
/// <b>The flash was the session ending to read.</b> Both commands were the shell's, so
/// Terminal.Gui was torn down, the fleet and the profiles were read with the screen gone,
/// and a new session was built over the answer. That is right for handing the terminal to a
/// child and a blink for a read - <c>BackgroundReads</c>' own reason, which the runner
/// modal's flights and the credential chooser already follow.
/// </para>
/// <para>
/// <b>The send itself stays the shell's.</b> It introduces, reaches over WebRTC and waits
/// out a heartbeat per machine, which a session may never do.
/// </para>
/// </remarks>
public class TheAudienceOpensInsideTheConsoleTests
{
    private const string Sonar = "local:analysis/sonarcloud";

    private static AppState OnACredential() => new()
    {
        ActiveTab = TabId.Credentials,
        CredentialsVisible = true,
        Credentials = new CredentialList
        {
            Credentials =
            [
                new CredentialSummary
                {
                    CredentialId = Sonar,
                    For = Sonar,
                    AddedAt = DateTimeOffset.UnixEpoch,
                    Reference = new CredentialReference
                    {
                        Kind = CredentialKinds.Local,
                        Locator = Sonar,
                        Identity = "kevin",
                        Scopes = [CredentialScopes.Read],
                    },
                },
            ],
        },
        CredentialsSelected = 0,
    };

    private static readonly CredentialAudienceRow Machine = new(
        RunnerId: "r1", Label: "vmlinux002", Locator: Sonar,
        Declared: false, Reported: false, Reachable: true, Through: null);

    [Test]
    public async Task Both_are_reads_beside_the_console_not_the_shell_s()
    {
        foreach (var command in (Command[])[Command.SendWhereNeeded, Command.ShowWhereCredentialIs])
        {
            await Assert.That(ShellCommands.Reads).Contains(command)
                .Because("a read folds in without the session ending, so nothing flashes.");
            await Assert.That(ShellCommands.Handled).DoesNotContain(command)
                .Because("a shell command ends the session, which is the flash.");
        }

        await Assert.That(ShellCommands.Handled).Contains(Command.SendTheAudience)
            .Because("the push blocks, and only the shell may wait.");
    }

    [Test]
    public async Task The_review_opens_at_once_and_says_it_is_reading()
    {
        var state = Reducer.Reduce(OnACredential() with { Mode = UiMode.CredentialActions }, Command.SendWhereNeeded);

        await Assert.That(state.Mode).IsEqualTo(UiMode.CredentialAudience);
        await Assert.That(state.AudienceFor).IsEqualTo(Sonar);
        await Assert.That(state.Audience).IsEmpty();
        await Assert.That(state.ReadInFlight).IsTrue();
        await Assert.That(PaneText.Audience(state)).Contains("Reading")
            .And.DoesNotContain("nobody to send it to")
            .Because("an audience not read yet is not an audience of nobody.");
    }

    [Test]
    public async Task Where_it_is_opens_at_once_too()
    {
        var state = Reducer.Reduce(OnACredential(), Command.ShowWhereCredentialIs);

        await Assert.That(state.Mode).IsEqualTo(UiMode.CredentialWhereabouts);
        await Assert.That(state.AudienceFor).IsEqualTo(Sonar);
        await Assert.That(PaneText.Whereabouts(state)).Contains("Reading");
    }

    [Test]
    public async Task With_nothing_selected_no_modal_opens_and_a_sentence_says_why()
    {
        var state = Reducer.Reduce(
            new AppState { ActiveTab = TabId.Credentials, Mode = UiMode.CredentialActions },
            Command.SendWhereNeeded);

        await Assert.That(state.Mode).IsEqualTo(UiMode.Normal);
        await Assert.That(state.LastCredential).Contains("no credential is selected");
    }

    [Test]
    public async Task The_machines_fold_into_the_open_review()
    {
        var opened = Reducer.Reduce(OnACredential(), Command.SendWhereNeeded);

        var landed = ConsoleAudience.Fold(
            Sonar,
            new VerbResult.CredentialAudienceFound(new VerbResult.AudienceList(Sonar, [Machine])),
            failure: null)(opened);

        await Assert.That(landed.Mode).IsEqualTo(UiMode.CredentialAudience);
        await Assert.That(landed.Audience.Select(r => r.Label)).IsEquivalentTo(new[] { "vmlinux002" });
    }

    [Test]
    public async Task A_read_that_lands_after_the_review_closed_reopens_nothing()
    {
        var closed = Reducer.Reduce(Reducer.Reduce(OnACredential(), Command.SendWhereNeeded), Command.CloseModal);

        var landed = ConsoleAudience.Fold(
            Sonar,
            new VerbResult.CredentialAudienceFound(new VerbResult.AudienceList(Sonar, [Machine])),
            failure: null)(closed);

        await Assert.That(landed.Mode).IsNotEqualTo(UiMode.CredentialAudience);
        await Assert.That(landed.Audience).IsEmpty();
    }

    [Test]
    public async Task A_failed_read_closes_the_review_with_why()
    {
        var opened = Reducer.Reduce(OnACredential(), Command.SendWhereNeeded);

        var landed = ConsoleAudience.Fold(Sonar, result: null, failure: "the control plane did not answer")(opened);

        await Assert.That(landed.Mode).IsEqualTo(UiMode.Normal);
        await Assert.That(landed.LastCredential).Contains("the control plane did not answer");
    }
}
