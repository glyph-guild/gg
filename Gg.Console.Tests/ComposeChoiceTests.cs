namespace Gg.Console.Tests;

/// <summary>
/// The choice a person makes before a flight is composed: their editor, or an
/// agent.
/// </summary>
/// <remarks>
/// <para>
/// <b>A modal because there is no honest default.</b> An editor is what gg has
/// always done and what somebody who knows what they want wants; an agent is
/// worth the terminal code because composing an intent from nothing is the hard
/// part. Which of those a person needs depends on the flight rather than on
/// them, so guessing would be wrong about half the time and invisible when it
/// was.
/// </para>
/// <para>
/// <b>It holds no I/O at all</b>, which is why it lives inside the UI session
/// while everything it leads to happens outside one. Pressing <c>n</c> opens it,
/// a key picks, and the loop reads the answer with the terminal already
/// released.
/// </para>
/// </remarks>
public class ComposeChoiceTests
{
    private static AppState Press(AppState state, KeyStroke key) =>
        Keymap.Resolve(key, KeymapContext.For(state)) is { } command
            ? Reducer.Reduce(state, command)
            : state;

    [Test]
    public async Task Pressing_n_asks_rather_than_opening_an_editor()
    {
        // THE KEY THAT USED TO GO STRAIGHT TO $EDITOR. It now asks, because
        // there are two ways to compose and neither is the obvious one.
        var asked = Press(new AppState(), KeyStroke.Char('n'));

        await Assert.That(asked.Mode).IsEqualTo(UiMode.ComposeChoice);
    }

    [Test]
    public async Task Both_ways_of_composing_are_offered_and_each_says_which_it_is()
    {
        var asked = Press(new AppState(), KeyStroke.Char('n'));

        var offered = Keymap.Hints(KeymapContext.For(asked));

        foreach (var word in (string[])["editor", "agent"])
        {
            await Assert.That(offered).Contains(word, StringComparison.OrdinalIgnoreCase)
                .Because($"a modal that does not name '{word}' is one where somebody has to "
                       + $"remember which key does what. Offered: {offered}");
        }
    }

    [Test]
    public async Task Choosing_leaves_the_modal_and_says_which_was_chosen()
    {
        // THE ANSWER IS STATE, not a branch taken inside the modal. The loop
        // reads it with the terminal released, which is the only place either
        // child can be started - so the modal's whole job is to record a
        // decision and get out of the way.
        var asked = Press(new AppState(), KeyStroke.Char('n'));

        var withEditor = Press(asked, KeyStroke.Char('e'));
        await Assert.That(withEditor.Mode).IsEqualTo(UiMode.Normal);
        await Assert.That(withEditor.ComposeWith).IsEqualTo(ComposeWith.Editor);

        var withAgent = Press(asked, KeyStroke.Char('a'));
        await Assert.That(withAgent.Mode).IsEqualTo(UiMode.Normal);
        await Assert.That(withAgent.ComposeWith).IsEqualTo(ComposeWith.Agent);
    }

    [Test]
    public async Task Escaping_composes_nothing()
    {
        // S33.2-02. A launch nobody confirmed is a flight number that was never
        // taken, so the way out has to leave no decision behind - not a default
        // one, and not the last one.
        var asked = Press(new AppState(), KeyStroke.Char('n'));
        var chosen = Press(asked, KeyStroke.Char('a'));

        var escaped = Press(Press(chosen, KeyStroke.Char('n')), KeyStroke.Esc);

        await Assert.That(escaped.Mode).IsEqualTo(UiMode.Normal);
        await Assert.That(escaped.ComposeWith).IsEqualTo(ComposeWith.Nothing)
            .Because("escaping is not choosing, and a previous choice left standing would "
                   + "compose the next flight with something nobody picked this time.");
    }

    [Test]
    public async Task The_choice_is_not_remembered_between_flights()
    {
        // Open question 3, answered "not at all in this slice": it is one
        // keystroke, and a remembered default that changes what a key does is
        // the invisible state this console has removed twice already.
        var composed = Press(Press(new AppState(), KeyStroke.Char('n')), KeyStroke.Char('a'));

        await Assert.That(Reducer.Reduce(composed, Command.FlightOpened).ComposeWith)
            .IsEqualTo(ComposeWith.Nothing)
            .Because("the answer is consumed by the flight it was given for. A person who "
                   + "wants an agent twice presses one more key, and one who does not is "
                   + "never surprised.");
    }

    [Test]
    public async Task Nothing_it_binds_collides_with_a_live_key_in_the_mode_it_opens_from()
    {
        // S33.2-03, ASSERTED AGAINST THE KEYMAP RATHER THAN BY READING IT.
        // Collisions ACROSS modes are ordinary here and deliberate - `a` is
        // approve in a gate decision and actions in Normal - because a modal
        // owns the keyboard while it is up. What must not collide is this
        // modal's keys against the mode it is opened FROM, since those are the
        // two sets a person is holding in their head at the same moment.
        var normal = Keymap.Bindings(KeymapContext.For(new AppState()))
            .Select(binding => binding.Key)
            .ToHashSet();

        var asked = Press(new AppState(), KeyStroke.Char('n'));

        foreach (var binding in Keymap.Bindings(KeymapContext.For(asked)))
        {
            // The escape hatch is SUPPOSED to be the same key everywhere, which
            // is what makes it findable without being learned.
            if (binding.Key == KeyStroke.Esc)
            {
                continue;
            }

            await Assert.That(normal.Contains(binding.Key)).IsFalse()
                .Because($"{binding.Key} does something else one keypress earlier, and a "
                       + "person who pressed n and then reached for it would be surprised.");
        }
    }

    [Test]
    public async Task The_help_page_names_both_keys()
    {
        // S33.2-05. A key that is only in the source is a key nobody finds.
        var catalogued = Keymap.Catalogue()
            .Where(entry => entry.Mode == UiMode.ComposeChoice)
            .Select(entry => entry.Binding.Key)
            .ToList();

        await Assert.That(catalogued).Contains(KeyStroke.Char('e'));
        await Assert.That(catalogued).Contains(KeyStroke.Char('a'));
    }
}
