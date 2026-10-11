using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// The broadcast review lists every machine a credential could be sent to, and a person
/// picks which ones receive it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Owner, 2026-10-11:</b> <i>"when i tried to send credentials to a machine via the TUI
/// (credentials tab, actions, press b) it says no machines need it. we should be able to
/// send credentials adhoc."</i>
/// </para>
/// <para>
/// <b>Wanted machines start picked, offered ones do not.</b> So a review nobody touches
/// sends exactly what it sent before, and a machine nothing declared receives a credential
/// only because somebody put the cursor on it and said so.
/// </para>
/// <para>
/// <b>What is picked is stored as what was CHANGED</b>, so the default needs no seeding
/// at the moment the list arrives, and a refreshed list cannot disagree with a seeded copy.
/// </para>
/// </remarks>
public class AnAudienceIsPickedTests
{
    private const string Locator = "local:analysis/sonarcloud";

    private static readonly CredentialAudienceRow Declared = new(
        RunnerId: "r1", Label: "vmlinux001", Locator: Locator,
        Declared: true, Reported: false, Reachable: true, Through: null);

    private static readonly CredentialAudienceRow Offered = new(
        RunnerId: "r2", Label: "vmlinux002", Locator: Locator,
        Declared: false, Reported: false, Reachable: true, Through: null);

    private static readonly CredentialAudienceRow Offline = new(
        RunnerId: "r3", Label: "vmlinux003", Locator: Locator,
        Declared: true, Reported: false, Reachable: false, Through: null);

    private static AppState Reviewing(params CredentialAudienceRow[] rows) =>
        new()
        {
            ActiveTab = TabId.Credentials,
            CredentialsVisible = true,
            Mode = UiMode.CredentialAudience,
            Audience = rows,
            AudienceFor = Locator,
        };

    [Test]
    public async Task A_wanted_machine_starts_picked_and_an_offered_one_does_not()
    {
        var picked = AudienceReview.Picked(Reviewing(Declared, Offered));

        await Assert.That(picked.Select(r => r.Label)).IsEquivalentTo(new[] { "vmlinux001" });
    }

    [Test]
    public async Task Space_picks_the_machine_under_the_cursor()
    {
        var state = Reducer.Reduce(Reviewing(Declared, Offered), Command.SelectNext);
        state = Reducer.Reduce(state, Command.ToggleAudienceMachine);

        await Assert.That(AudienceReview.Picked(state).Select(r => r.Label))
            .IsEquivalentTo(new[] { "vmlinux001", "vmlinux002" });
    }

    [Test]
    public async Task And_again_leaves_it_out()
    {
        var state = Reducer.Reduce(Reviewing(Declared, Offered), Command.ToggleAudienceMachine);

        await Assert.That(AudienceReview.Picked(state)).IsEmpty()
            .Because("a declared machine can be left out of one send too.");
    }

    [Test]
    public async Task An_unreachable_machine_cannot_be_picked()
    {
        var state = Reducer.Reduce(Reviewing(Offline), Command.ToggleAudienceMachine);

        await Assert.That(AudienceReview.Picked(state)).IsEmpty()
            .Because("nothing is held for an offline machine, so picking it would promise a "
                   + "delivery gg cannot make.");
    }

    [Test]
    public async Task The_cursor_stays_on_the_list()
    {
        var state = Reviewing(Declared, Offered);
        for (var i = 0; i < 5; i++)
        {
            state = Reducer.Reduce(state, Command.SelectNext);
        }

        await Assert.That(state.AudienceSelected).IsEqualTo(1);

        for (var i = 0; i < 5; i++)
        {
            state = Reducer.Reduce(state, Command.SelectPrevious);
        }

        await Assert.That(state.AudienceSelected).IsEqualTo(0);
    }

    [Test]
    public async Task Nothing_picked_asks_for_no_passphrase()
    {
        var state = Reducer.Reduce(Reviewing(Offered), Command.ConfirmAudience);

        await Assert.That(AudienceReview.AsksForThePassphrase(state)).IsFalse()
            .Because("a passphrase read for a push to nobody reads as something having moved.");
    }

    [Test]
    public async Task Picking_an_offered_machine_is_enough_to_go_on()
    {
        var state = Reducer.Reduce(Reviewing(Offered), Command.ToggleAudienceMachine);
        state = Reducer.Reduce(state, Command.ConfirmAudience);

        await Assert.That(AudienceReview.AsksForThePassphrase(state)).IsTrue();
        await Assert.That(AudienceReview.Recipients(state)).IsEqualTo(1);
    }

    [Test]
    public async Task The_list_marks_what_is_picked_and_where_the_cursor_is()
    {
        var rows = Rows.Audience(Reviewing(Declared, Offered));
        var text = PaneText.Audience(Reviewing(Declared, Offered));

        await Assert.That(rows[0].Picked).IsTrue();
        await Assert.That(rows[1].Picked).IsFalse();
        await Assert.That(rows[1].Text).Contains("not declared", StringComparison.Ordinal);
        await Assert.That(text).Contains("space", StringComparison.Ordinal)
            .Because("a key that picks must be named where the picking happens.");
    }

    [Test]
    public async Task An_empty_tenant_still_says_nobody_is_online()
    {
        var text = PaneText.Audience(Reviewing());

        await Assert.That(text).Contains("no machine", StringComparison.OrdinalIgnoreCase);
    }
}
