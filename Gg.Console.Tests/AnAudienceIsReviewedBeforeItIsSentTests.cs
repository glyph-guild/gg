using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// The audience is a list a person reads before confirming, and the passphrase field
/// does not appear until they have.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.6-02, and the owner's call between two shapes.</b> One screen with the list and
/// the field together was the cheaper option; two stages were chosen because <i>"I did not
/// mean that audience"</i> should be a cheap mistake, and a field already focused invites
/// typing before reading.
/// </para>
/// <para>
/// <b>It costs one <c>UiMode</c> and no <c>KeymapContext</c> flag</b>, which is the part
/// worth knowing for the next one. <c>enter</c> and <c>esc</c> mean <i>go on</i> and
/// <i>stop</i> in both stages, so the stage is read by the reducer and the view rather
/// than by the keymap — which keeps it out of <c>HelpNamesEveryKeyTests</c>' hand-kept
/// member count and out of the <c>Everywhere()</c> product that multiplies with every
/// flag.
/// </para>
/// <para>
/// <b>The modal says the specific thing, the hint line says the general one.</b> A hint
/// that read "enter to send" during the review would be wrong for a stage, and the
/// airspace tab already set the precedent: the acts name themselves inside the modal
/// where there is room for a sentence.
/// </para>
/// </remarks>
public class AnAudienceIsReviewedBeforeItIsSentTests
{
    private static readonly CredentialAudienceRow First = new(
        RunnerId: "r1", Label: "vmlinux001", Locator: "local:acme/widgets",
        Declared: true, Reported: false, Reachable: true, Through: null);

    private static readonly CredentialAudienceRow Second = new(
        RunnerId: "r2", Label: "vmlinux002", Locator: "local:acme/widgets",
        Declared: true, Reported: true, Reachable: true, Through: null);

    private static AppState Reviewing() =>
        new()
        {
            ActiveTab = TabId.Credentials,
            CredentialsVisible = true,
            Mode = UiMode.CredentialAudience,
            Audience = [First, Second],
            AudienceFor = "local:acme/widgets",
        };

    [Test]
    public async Task Every_machine_is_a_row_in_the_review()
    {
        var rows = Rows.Audience(Reviewing());

        await Assert.That(rows.Count).IsEqualTo(2)
            .Because("a row per machine, which is what makes it reviewable rather than a count.");

        await Assert.That(rows.Select(r => r.Machine).Order())
            .IsEquivalentTo(new[] { "vmlinux001", "vmlinux002" });
    }

    [Test]
    public async Task And_each_row_says_why_it_is_there()
    {
        var rows = Rows.Audience(Reviewing());

        var reported = rows.Single(r => r.Machine == "vmlinux002");

        await Assert.That(reported.Why).Contains("reported")
            .Because("declared and reported mean different things to whoever is reading - one is "
                   + "somebody's intent, the other is a machine saying it is currently broken. "
                   + "Said: " + reported.Why);
    }

    [Test]
    public async Task The_passphrase_field_is_not_shown_while_the_list_is_being_read()
    {
        await Assert.That(AudienceReview.AsksForThePassphrase(Reviewing())).IsFalse()
            .Because("a field already focused invites typing before reading, and the whole reason "
                   + "for two stages is that the list is looked at first.");
    }

    [Test]
    public async Task Confirming_the_list_is_what_reveals_it()
    {
        var after = Reducer.Reduce(Reviewing(), Command.ConfirmAudience);

        await Assert.That(after.Mode).IsEqualTo(UiMode.CredentialAudience)
            .Because("still the same modal - the stage moved, not the screen, so the list stays "
                   + "visible behind the field a person is typing into.");

        await Assert.That(AudienceReview.AsksForThePassphrase(after)).IsTrue()
            .Because("they have read it and said go on, which is the moment the secret is wanted.");
    }

    [Test]
    public async Task And_the_list_is_still_there_when_the_field_appears()
    {
        var after = Reducer.Reduce(Reviewing(), Command.ConfirmAudience);

        await Assert.That(Rows.Audience(after).Count).IsEqualTo(2)
            .Because("somebody typing a passphrase should still be able to see what they are "
                   + "typing it for - a field on a blank screen is a confirmation with no subject.");
    }

    [Test]
    public async Task The_modal_names_how_many_will_receive_it_before_the_field()
    {
        var said = PaneText.Audience(Reviewing());

        await Assert.That(said).Contains("2")
            .Because("the count is the summary of the list above it, and the thing a person checks "
                   + "against what they expected. Said: " + said);

        await Assert.That(said).Contains("local:acme/widgets")
            .Because("and which credential, because a broadcast is two facts at once. Said: " + said);
    }

    [Test]
    public async Task The_hint_line_does_not_promise_a_send_during_the_review()
    {
        var reviewing = Keymap.Hints(KeymapContext.For(Reviewing()));

        await Assert.That(reviewing).DoesNotContain("send", StringComparison.OrdinalIgnoreCase)
            .Because("enter means GO ON during the review and SEND after it, so a hint naming one "
                   + "of them is wrong for a stage. The modal says the specific thing. Line: "
                   + reviewing);
    }
}
