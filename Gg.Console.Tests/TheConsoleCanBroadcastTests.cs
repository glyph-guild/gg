using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The credentials tab can send the credential under the cursor where it is needed.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.6-01, and the owner asked for it the day the broadcast merged:</b> <i>"can i
/// broadcast in the TUI?"</i> It could not. Step 5 had recorded that as a deliberate
/// absence on the grounds that the audience needs somewhere to be PUT — a list, which
/// the credential modals do not do — and that is a thing to build rather than a reason
/// not to.
/// </para>
/// <para>
/// <b>The shape of the gap is the one the owner caught before.</b> The credentials tab
/// already shows a holder column; a person reading that a credential is sealed to them
/// and not to the fleet is exactly the person who wants to send it where it is needed,
/// and a column that states a problem with no key beside it is evidence assembled and
/// discarded.
/// </para>
/// <para>
/// <b>`b` IS NOT A TAB-LEVEL KEY, which the first sketch assumed.</b> It is the browse
/// tab's toggle and tab keys resolve from everywhere by design, so binding it on one tab
/// would make <c>b</c> mean two things depending on where somebody is standing. The four
/// credential acts already live behind <c>a</c>, <i>"because inside a modal the letters
/// are free"</i> — so this is <c>a</c> then <c>b</c> and is reached the same way as every
/// other act on that tab.
/// </para>
/// <para>
/// <b>It acts on the credential under the cursor rather than asking which.</b> The CLI
/// has to be told a locator because it cannot see a screen; the console can, and asking
/// a person to retype something they are looking at is the thing a pane is for.
/// </para>
/// </remarks>
public class TheConsoleCanBroadcastTests
{
    private static AppState OnTheCredentialsTab() =>
        new() { ActiveTab = TabId.Credentials, CredentialsVisible = true };

    [Test]
    public async Task The_broadcast_is_a_key_inside_the_credential_acts()
    {
        var inside = KeymapContext.For(
            OnTheCredentialsTab() with { Mode = UiMode.CredentialActions });

        await Assert.That(Keymap.Resolve(KeyStroke.Char('b'), inside))
            .IsEqualTo(Command.SendWhereNeeded)
            .Because("inside a modal the letters are free, and `b` for broadcast is the one a "
                   + "person would reach for.");
    }

    [Test]
    public async Task And_b_is_still_the_browse_tab_everywhere_including_this_tab()
    {
        // THE HALF THAT KEEPS THE KEYBOARD HONEST. A tab key that stopped working on one
        // tab is a key that means two things, and the hint line prints the tab keys on
        // the tabs themselves - so this would be a lie on screen as well as under it.
        await Assert.That(Keymap.Resolve(
                KeyStroke.Char('b'), KeymapContext.For(OnTheCredentialsTab())))
            .IsEqualTo(Command.ToggleIntents)
            .Because("in Normal mode `b` is the browse tab's toggle from every tab, which is why "
                   + "the broadcast lives inside the acts modal rather than beside it.");
    }

    [Test]
    public async Task It_takes_the_terminal_like_every_other_credential_act()
    {
        // THE PUSH BLOCKS, which is the one thing a UI session may never do: SendAsync
        // introduces, reaches over WebRTC and waits out a heartbeat interval. The review
        // and the passphrase stay on screen; only this leaves.
        await Assert.That(ShellCommands.Handled).Contains(Command.SendWhereNeeded)
            .Because("a session that waited on a push would be a frozen console, and the shell is "
                   + "where every other credential act already does its waiting.");
    }

    [Test]
    public async Task The_act_is_about_the_credential_under_the_cursor()
    {
        // NOT A PROMPT ASKING WHICH ONE. The CLI must be told a locator because it cannot
        // see a screen; a console that asked would be asking somebody to retype what they
        // are looking at.
        var rows = Rows.Credentials(OnTheCredentialsTab() with
        {
            Credentials = new CredentialList
            {
                Credentials =
                [
                    ACredential("local:acme/first"),
                    ACredential("local:acme/second"),
                ],
            },
            CredentialsSelected = 1,
        });

        await Assert.That(AudienceReview.Chosen(OnTheCredentialsTab() with
        {
            Credentials = new CredentialList
            {
                Credentials =
                [
                    ACredential("local:acme/first"),
                    ACredential("local:acme/second"),
                ],
            },
            CredentialsSelected = 1,
        })).IsEqualTo("local:acme/second")
            .Because("the cursor is on the second row, so that is the credential being sent. "
                   + $"Rows: {rows.Count}");
    }

    [Test]
    public async Task With_no_credential_under_the_cursor_there_is_nothing_to_send()
    {
        // AN EMPTY TAB IS NOT AN ERROR. Pressing the key on a tenant with no credentials
        // registered should say so rather than opening a review of nobody, and certainly
        // rather than throwing out of a keystroke.
        await Assert.That(AudienceReview.Chosen(OnTheCredentialsTab())).IsNull()
            .Because("nothing is selected because nothing is there, and the caller turns that into "
                   + "a sentence.");
    }

    private static CredentialSummary ACredential(string locator) =>
        new()
        {
            CredentialId = "01a0632b-e971-7000-8000-00000000000" + locator[^1],
            For = locator,
            Reference = new CredentialReference
            {
                Kind = CredentialKinds.Local,
                Locator = locator,
                Identity = "kdeenanauth",
                Scopes = [CredentialScopes.Read],
            },
            ReferencedBySubject = "kevin@example.test",
            AddedAt = DateTimeOffset.UnixEpoch,
        };
}
