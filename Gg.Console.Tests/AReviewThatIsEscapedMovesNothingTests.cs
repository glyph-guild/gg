using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// Escaping the review sends nothing and reads no passphrase, from either stage.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.6-04, and the reason two stages exist at all.</b> A review somebody cannot back
/// out of is a confirmation, and the owner chose the list-then-field shape precisely so
/// that <i>"I did not mean that audience"</i> is cheap. That is only true if the way out
/// works from both halves.
/// </para>
/// <para>
/// <b>From the SECOND stage especially.</b> Backing out of a list is obvious; backing out
/// with a half-typed passphrase on screen is the moment somebody has realised something,
/// and the thing that must not happen is the keystroke being read as a confirmation.
/// </para>
/// <para>
/// <b>Modals own the keyboard with exactly one escape hatch</b> — the console's own rule,
/// so that the terminal can never be locked up — and this is that hatch for this mode.
/// </para>
/// </remarks>
public class AReviewThatIsEscapedMovesNothingTests
{
    private static readonly CredentialAudienceRow Reachable = new(
        RunnerId: "r1", Label: "vmlinux001", Locator: "local:acme/widgets",
        Declared: true, Reported: false, Reachable: true, Through: null);

    private static AppState Reviewing() =>
        new()
        {
            ActiveTab = TabId.Credentials,
            CredentialsVisible = true,
            Mode = UiMode.CredentialAudience,
            Audience = [Reachable],
            AudienceFor = "local:acme/widgets",
        };

    [Test]
    public async Task Escape_closes_the_review()
    {
        var context = KeymapContext.For(Reviewing());

        await Assert.That(Keymap.Resolve(KeyStroke.Esc, context))
            .IsEqualTo(Command.CloseModal)
            .Because("modals own the keyboard with exactly one escape hatch, so the terminal can "
                   + "never be locked up.");
    }

    [Test]
    public async Task And_leaves_no_audience_behind_to_be_acted_on()
    {
        var after = Reducer.Reduce(Reviewing(), Command.CloseModal);

        await Assert.That(after.Mode).IsEqualTo(UiMode.Normal);

        await Assert.That(after.Audience).IsEmpty()
            .Because("an audience left in the state is one a later keystroke could send. The act "
                   + "was abandoned, so the subject goes with it.");
    }

    [Test]
    public async Task Escaping_the_passphrase_stage_also_sends_nothing()
    {
        // THE ONE THAT MATTERS MOST. Backing out with a half-typed passphrase on screen
        // is the moment somebody has realised something, and the keystroke must not be
        // read as a confirmation.
        var typing = Reducer.Reduce(Reviewing(), Command.ConfirmAudience);

        await Assert.That(AudienceReview.AsksForThePassphrase(typing)).IsTrue()
            .Because("the fixture has to be in the second stage for this test to mean anything.");

        var after = Reducer.Reduce(typing, Command.CloseModal);

        await Assert.That(after.Mode).IsEqualTo(UiMode.Normal);
        await Assert.That(after.Audience).IsEmpty();

        await Assert.That(AudienceReview.AsksForThePassphrase(after)).IsFalse()
            .Because("and the stage goes back with it, so reopening the review starts at the list "
                   + "rather than at a field for an audience nobody has read.");
    }

    [Test]
    public async Task Nothing_about_escaping_reaches_the_shell()
    {
        // THE PUSH IS A SHELL ACT, so the way a session ENDS decides whether one happens.
        // CloseModal is handled inside the session; if it reached the shell, closing a
        // review would run the arm that sends.
        await Assert.That(ShellCommands.Handled).DoesNotContain(Command.CloseModal)
            .Because("closing a modal is the session's own business, and a close that ended the "
                   + "session would hand the shell a command it reads as 'do the thing'.");
    }

    [Test]
    public async Task Reopening_after_an_escape_starts_at_the_list_again()
    {
        var abandoned = Reducer.Reduce(
            Reducer.Reduce(Reviewing(), Command.ConfirmAudience), Command.CloseModal);

        // Nothing stale: the next review is derived fresh, so a second attempt reads the
        // fleet again rather than acting on what it said a minute ago.
        await Assert.That(abandoned.AudienceFor).IsNull()
            .Because("the credential the review was about goes with the review. A stale subject is "
                   + "how a second attempt sends the first one's credential.");
    }
}
