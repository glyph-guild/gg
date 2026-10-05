using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The credentials pane is on the bar, answers <c>r</c>, and every switch that
/// throws on a tab it does not know knows this one.
/// </summary>
/// <remarks>
/// <para>
/// <b>S60.4-01 and S60.5-01 together, because they are one change.</b> The
/// repositories pane does not disappear and a credentials pane appear beside it —
/// the owner's call was that one <i>retires into</i> the other, and <c>r</c> cannot
/// belong to two tabs. So this is a rename of the slot plus a change of what it
/// shows, which is also much the safer shape: the enum position, the keystroke and
/// the bar order all stay put.
/// </para>
/// <para>
/// <b>THE SHAPE THAT HAS CRASHED THIS CONSOLE BEFORE.</b>
/// <c>TheItinerariesTabIsOnTheBarTests</c> says it in those words: a tab costs six
/// exhaustive switches that throw on a word they do not know, and the two that were
/// missed last time each killed the console on a frame — one on the first render,
/// one on arrival. Renaming cannot miss one, because the compiler finds every arm.
/// That is the argument for doing it this way rather than adding and deleting.
/// </para>
/// </remarks>
public class TheCredentialsTabIsOnTheBarTests
{
    private static AppState Bare() => new();

    [Test]
    public async Task The_bar_names_it()
    {
        var named = Tabs.All.Select(Tabs.Name).ToList();

        await Assert.That(named).Contains("credentials", StringComparer.OrdinalIgnoreCase)
            .Because($"the bar's job is to say what there is. Saw [{string.Join(", ", named)}]");
    }

    [Test]
    public async Task It_answers_r()
    {
        // THE KEY CARRIES OVER FROM THE REPOSITORIES PANE, which is the owner's
        // call. The cost is muscle memory landing somewhere slightly different -
        // the milder version of the dead-key shape this console has paid for four
        // times - and the pane still lists repositories, so it is not a wild jump.
        await Assert.That(Tabs.KeyFor(TabId.Credentials)?.Name).IsEqualTo("r");

        await Assert.That(Keymap.Resolve(
                Tabs.KeyFor(TabId.Credentials)!.Value, KeymapContext.For(Bare())))
            .IsEqualTo(Tabs.CommandFor(TabId.Credentials))
            .Because("the key the bar advertises and the command dispatch runs must be the same "
                   + "thing, whether a person typed it or clicked.");
    }

    [Test]
    public async Task Every_switch_that_throws_on_an_unknown_tab_knows_it()
    {
        // CALLING THEM IS THE ASSERTION, which is the sibling test's own shape.
        // Four of these are in Tabs, the fifth is where focus lands and the sixth
        // is the pane's own text; Trouble and TableOf throw just the same.
        var state = Bare() with { ActiveTab = TabId.Credentials };

        await Assert.That(Tabs.Name(TabId.Credentials)).IsNotEmpty();
        await Assert.That(Tabs.KeyFor(TabId.Credentials)).IsNotNull();
        await Assert.That(Tabs.CommandFor(TabId.Credentials)).IsNotNull();
        _ = Tabs.HasRead(state, TabId.Credentials);
        await Assert.That(Tabs.Title(state, TabId.Credentials)).IsNotEmpty();
        await Assert.That(PaneText.ForTab(state, TabId.Credentials)).IsNotEmpty();
        await Assert.That(PaneText.Trouble(state, TabId.Credentials)).IsNotNull();
    }

    [Test]
    public async Task It_is_in_the_offered_set()
    {
        await Assert.That(Tabs.Offered(Bare())).Contains(TabId.Credentials)
            .Because("it is not conditional: every tenant has credentials, and a pane that "
                   + "appeared only for an admin would hide the row that predicts a failure.");
    }

    [Test]
    public async Task Its_key_means_nothing_in_every_other_mode()
    {
        // A TAB KEY MUST NOT SHADOW A MODAL'S. `r` was Repositories' and is now
        // this pane's, so the property is inherited rather than new - and worth
        // re-asserting, because the thing that changed is which tab it opens.
        var key = Tabs.KeyFor(TabId.Credentials)!.Value;

        foreach (var mode in Enum.GetValues<UiMode>().Where(m => m != UiMode.Normal))
        {
            await Assert.That(Keymap.Resolve(
                    key, KeymapContext.For(Bare() with { Mode = mode })))
                .IsNull()
                .Because($"'{key.Name}' is a tab key, and in {mode} it would shadow whatever that "
                       + "mode means by it.");
        }
    }

    [Test]
    public async Task Arriving_on_it_asks_for_a_read_when_it_has_none()
    {
        // THE EDGE, not every frame. Reducer.Arrived sets Refresh.Wanted only
        // while HasRead is false, so a refusing control plane is asked once per
        // arrival rather than once per render.
        var arrived = Reducer.Reduce(Bare(), Tabs.CommandFor(TabId.Credentials)!.Value);

        await Assert.That(arrived.ActiveTab).IsEqualTo(TabId.Credentials);
        await Assert.That(arrived.Refresh.Wanted).IsTrue()
            .Because("a pane that never asks is a pane that says 'reading' for ever.");
    }
}
