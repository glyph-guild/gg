using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The queue and the flights get a key each, and it is punctuation because
/// there is no letter left to give them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Six of the eight tabs had a key and these two did not.</b> The reason
/// was written down and it was about letters: <i>"a letter spent on either is a
/// letter taken from something a person cannot otherwise reach"</i>. That is
/// still true, so no letter is spent - but it was never an argument against a
/// key, only against a letter, and getting from the airspace tab to the queue
/// was four presses of tab through everything in between.
/// </para>
/// <para>
/// <b>How short of letters this console actually is.</b> Every letter that says
/// anything about either word is taken: <c>q</c> is quit, <c>u</c> is the
/// runners tab and <c>e</c> the airspace; of `flights', <c>f</c> is freeze and
/// fly, <c>l</c> is live, and <c>i g h t</c> are all bound. Capitals cannot
/// help, because <c>KeyTranslator</c> strips shift and lowercases before the
/// keymap ever sees a keystroke - deliberately, so that one letter is one key.
/// The digits are the floor modal's, and that modal opens from Normal with
/// <c>o</c>, which is the one collision this keymap's own guard names: a key
/// must not mean something else one keypress earlier.
/// </para>
/// <para>
/// <b>So `,' and `.', in bar order.</b> They are free in every mode, they
/// shadow nothing, they sit next to each other under the right hand, and they
/// are engraved with the arrows that mean "along". The two leftmost tabs get
/// the two leftmost of them.
/// </para>
/// <para>
/// <b>One meaning each, where the other six have two.</b> A tab key closes its
/// view on a second press and lands on the queue; neither of these can be
/// closed - the queue is what closing lands ON - so pressing it again leaves
/// you where you already are.
/// </para>
/// </remarks>
public class TheTwoFlightListsGetKeysTooTests
{
    private static KeymapContext Anywhere(TabId showing) =>
        new(UiMode.Normal, showing) { Refresh = "30s" };

    [Test]
    public async Task The_queue_has_a_key_and_the_flights_have_one()
    {
        await Assert.That(Tabs.KeyFor(TabId.Queue)).IsNotNull();
        await Assert.That(Tabs.KeyFor(TabId.Flights)).IsNotNull();

        foreach (var tab in Tabs.All)
        {
            await Assert.That(Tabs.KeyFor(tab)).IsNotNull()
                .Because($"{tab} is a tab, and every tab now says how to get to it.");
        }
    }

    [Test]
    public async Task And_neither_of_them_spends_a_letter()
    {
        // THE REASON THE OLD GUARD GAVE, KEPT. Letters are the scarce thing in
        // this keymap - there are none free that read as anything - so the
        // invariant is not "these two have no key", it is "these two take no
        // letter from anything that needs one".
        foreach (var tab in (TabId[])[TabId.Queue, TabId.Flights])
        {
            var key = Tabs.KeyFor(tab)!.Value.Name;

            await Assert.That(char.IsAsciiLetter(key[0])).IsFalse()
                .Because($"{tab} took '{key}', and a letter here is a letter taken from a "
                       + "view somebody cannot otherwise reach.");
        }
    }

    [Test]
    public async Task Pressing_them_goes_there()
    {
        await Assert.That(Keymap.Resolve(Tabs.KeyFor(TabId.Queue)!.Value, Anywhere(TabId.Envelope)))
            .IsEqualTo(Tabs.CommandFor(TabId.Queue));

        await Assert.That(Keymap.Resolve(Tabs.KeyFor(TabId.Flights)!.Value, Anywhere(TabId.Browse)))
            .IsEqualTo(Tabs.CommandFor(TabId.Flights));
    }

    [Test]
    public async Task From_anywhere_to_either_is_one_press()
    {
        // WHAT THIS IS FOR. Tab cycles all eight, so the airspace tab was four
        // presses from the queue with six tabs' worth of redraw in between.
        foreach (var from in Tabs.All)
        {
            var onQueue = Reducer.Reduce(
                new AppState { ActiveTab = from }, Tabs.CommandFor(TabId.Queue)!.Value);

            await Assert.That(onQueue.ActiveTab).IsEqualTo(TabId.Queue)
                .Because($"one press from {from}.");

            var onFlights = Reducer.Reduce(
                new AppState { ActiveTab = from }, Tabs.CommandFor(TabId.Flights)!.Value);

            await Assert.That(onFlights.ActiveTab).IsEqualTo(TabId.Flights);
        }
    }

    [Test]
    public async Task Pressing_it_again_leaves_you_where_you_are()
    {
        // NOT A TOGGLE, because neither can be closed. The other six close on a
        // second press and land on the queue, which only works because the
        // queue is the thing they land on.
        var onQueue = Reducer.Reduce(
            new AppState { ActiveTab = TabId.Queue }, Tabs.CommandFor(TabId.Queue)!.Value);

        await Assert.That(onQueue.ActiveTab).IsEqualTo(TabId.Queue);

        var onFlights = Reducer.Reduce(
            new AppState { ActiveTab = TabId.Flights }, Tabs.CommandFor(TabId.Flights)!.Value);

        await Assert.That(onFlights.ActiveTab).IsEqualTo(TabId.Flights);
    }

    [Test]
    public async Task Neither_key_means_anything_anywhere_else()
    {
        // THE WHOLE REASON PUNCTUATION WAS AVAILABLE. A key that already meant
        // something in a modal would mean two things one keypress apart, which
        // is what ruled the digits out.
        foreach (var tab in (TabId[])[TabId.Queue, TabId.Flights])
        {
            var key = Tabs.KeyFor(tab)!.Value;

            foreach (var mode in Enum.GetValues<UiMode>())
            {
                if (mode == UiMode.Normal)
                {
                    continue;
                }

                await Assert.That(Keymap.Resolve(key, new KeymapContext(mode)))
                    .IsNull()
                    .Because($"'{key.Name}' is {tab}'s, and in {mode} it would be a second "
                           + "meaning for one keypress.");
            }
        }
    }

    [Test]
    public async Task The_keys_are_on_the_tabs_and_not_on_the_hint_line()
    {
        // The rule the other six already keep: the bar is where a tab's key is
        // advertised, because the hint line is one line.
        var bare = new AppState();

        await Assert.That(Tabs.Title(bare, TabId.Queue))
            .Contains(Tabs.KeyFor(TabId.Queue)!.Value.Name, StringComparison.Ordinal);
        await Assert.That(Tabs.Title(bare, TabId.Flights))
            .Contains(Tabs.KeyFor(TabId.Flights)!.Value.Name, StringComparison.Ordinal);

        var line = Keymap.Hints(KeymapContext.For(bare));

        await Assert.That(line).DoesNotContain("queue", StringComparison.OrdinalIgnoreCase);
        await Assert.That(line).DoesNotContain("flights", StringComparison.OrdinalIgnoreCase);
    }
}
