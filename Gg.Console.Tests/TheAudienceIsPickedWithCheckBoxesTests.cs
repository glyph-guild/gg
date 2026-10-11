using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// The broadcast review picks machines with real checkboxes, not marks drawn in a label.
/// </summary>
/// <remarks>
/// <para>
/// <b>Owner, 2026-10-11:</b> <i>"needs to use real controls to allow selection of the
/// runner."</i> The review was a <c>Label</c> printing <c>[x]</c> and <c>[ ]</c>, moved with
/// <c>j</c> and <c>k</c> because a label has no arrows, and it took no click.
/// </para>
/// <para>
/// <b>The state still decides.</b> A box flipped by a key or a click is reduced into
/// <c>AudienceToggled</c> and drawn back from it, so the passphrase stage, the send and the
/// dump all read the same picks the boxes show.
/// </para>
/// </remarks>
public class TheAudienceIsPickedWithCheckBoxesTests
{
    private const string Locator = "local:analysis/sonarcloud";

    private static readonly CredentialAudienceRow Declared = new(
        RunnerId: "r1", Label: "vmlinux001", Locator: Locator,
        Declared: true, Reported: false, Reachable: true, Through: null);

    private static readonly CredentialAudienceRow Offered = new(
        RunnerId: "r2", Label: "vmlinux002", Locator: Locator,
        Declared: false, Reported: false, Reachable: true, Through: null);

    private static AppState Reviewing(params CredentialAudienceRow[] rows) => new()
    {
        ActiveTab = TabId.Credentials,
        CredentialsVisible = true,
        Mode = UiMode.CredentialAudience,
        Audience = rows,
        AudienceFor = Locator,
    };

    private static string Screen() =>
        ConsoleSource.Text("Gg.Console", Path.Combine("Views", "ConsoleScreen.cs"));

    [Test]
    public async Task Each_machine_is_a_checkbox()
    {
        var screen = Screen();

        await Assert.That(screen).Contains("new CheckBox")
            .Because("a box a person can tab to, press and click is the control the owner asked for.");
        await Assert.That(screen).DoesNotContain("_audienceList = new Label")
            .Because("marks drawn in a label are what this replaces.");
    }

    [Test]
    public async Task A_click_on_a_box_is_reduced_rather_than_kept_by_the_box()
    {
        await Assert.That(Screen()).Contains("ValueChanging += OnAudienceBoxChanging")
            .Because("the box would otherwise hold a pick the state, the passphrase stage and the "
                   + "send never hear about.");
    }

    [Test]
    public async Task Each_box_hears_the_review_s_keys()
    {
        await Assert.That(Screen()).Contains("box.KeyDown += OnAudienceBoxKeyDown")
            .Because("a focused box receives a key first, so enter and esc must reach the keymap "
                   + "from it - the modal-body lesson.");
    }

    [Test]
    public async Task A_row_reads_as_its_machine_and_why()
    {
        var rows = Rows.Audience(Reviewing(Declared, Offered));

        await Assert.That(rows[0].Text).Contains("vmlinux001").And.Contains("declared");
        await Assert.That(rows[1].Text).Contains("vmlinux002").And.Contains("not declared");
        await Assert.That(rows[0].Text).DoesNotContain("[x]")
            .Because("the box draws its own mark.");
    }

    [Test]
    public async Task The_review_names_arrows_and_clicks_rather_than_j_and_k()
    {
        var text = PaneText.Audience(Reviewing(Declared, Offered));

        await Assert.That(text).Contains("space").And.Contains("click");
        await Assert.That(text).DoesNotContain("j and k");
        await Assert.That(text).DoesNotContain("[x] vmlinux001")
            .Because("the machines are boxes now, so the sentence around them does not repeat them.");
    }

    [Test]
    public async Task J_and_k_are_no_longer_bound_in_the_review()
    {
        var bound = Keymap.Bindings(new KeymapContext(UiMode.CredentialAudience)).Select(b => b.Key);

        await Assert.That(bound).DoesNotContain(KeyStroke.Char('j'));
        await Assert.That(bound).DoesNotContain(KeyStroke.Char('k'));
    }
}
