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

        // THE WORDS, NOT THE KEYS. What a person needs off the hint line is
        // which of the two things each key does; the letters themselves are
        // whatever Normal mode left free.
        foreach (var word in (string[])["editor", "agent"])
        {
            await Assert.That(offered).Contains(word, StringComparison.OrdinalIgnoreCase)
                .Because($"a modal that does not name '{word}' is one where somebody has to "
                       + $"remember which key does what. Offered: {offered}");
        }
    }

    [Test]
    public async Task Answering_decides_nothing_locally()
    {
        // THE SHAPE THE TWO GATE ANSWERS ALREADY HAVE, and for the same reason.
        // Both keys leave the state exactly as it is: the loop opens the flight,
        // with the terminal released, because that is the only place a child can
        // be started. A reducer that closed the modal here would be closing the
        // question before the thing it asked about had happened.
        var asked = Press(new AppState(), KeyStroke.Char('n'));

        foreach (var key in (KeyStroke[])[KeyStroke.Char('w'), KeyStroke.Char('m')])
        {
            await Assert.That(Press(asked, key)).IsEqualTo(asked)
                .Because($"'{key}' answers a question; it does not act on the answer.");
        }
    }

    [Test]
    public async Task Escaping_opens_nothing()
    {
        // S33.2-02. A launch nobody confirmed is a flight number that was never
        // taken - so the way out closes the question and sends no command at
        // all, which is what "opens nothing" has to mean.
        var asked = Press(new AppState(), KeyStroke.Char('n'));
        var escaped = Press(asked, KeyStroke.Esc);

        await Assert.That(escaped.Mode).IsEqualTo(UiMode.Normal);

        await Assert.That(ShellCommands.Handled.Contains(
                Keymap.Resolve(KeyStroke.Esc, KeymapContext.For(asked))!.Value))
            .IsFalse()
            .Because("escaping must not reach the loop at all. A command that ended the "
                   + "session here would arrive in the same arm the answers do.");
    }

    [Test]
    public async Task The_choice_cannot_be_remembered_because_there_is_nothing_to_remember()
    {
        // Open question 3, answered "not at all in this slice" - and the answer
        // is now structural rather than a rule somebody maintains. The choice
        // lives in the COMMAND, which exists for the length of one dispatch, so
        // there is no field for a later flight to inherit and no clearing step
        // to forget.
        var asked = Press(new AppState(), KeyStroke.Char('n'));

        await Assert.That(Press(asked, KeyStroke.Char('m'))).IsEqualTo(asked);

        var fields = typeof(AppState).GetProperties()
            .Where(p => p.Name.Contains("Compose", StringComparison.Ordinal))
            .Select(p => p.Name)
            .ToList();

        await Assert.That(fields).IsEmpty()
            .Because("a field holding which way to compose is one a later flight can inherit. "
                   + "Found: " + string.Join(", ", fields));
    }

    [Test]
    public async Task Answering_the_question_actually_opens_a_flight()
    {
        // THE PROPERTY THE REST OF THIS FILE DOES NOT COVER, and it is the one
        // that matters: a person presses `n`, answers, and a flight exists.
        //
        // Every other test here asserts what the REDUCER does, and the reducer
        // ran perfectly while the key did nothing at all - because a command the
        // session handles never ends the session, and opening a flight can only
        // happen between sessions with the terminal free. So the modal recorded
        // a choice, returned to Normal, and nothing ever acted on it. That is
        // the "bound and inert key" this slice was warned about, arrived at from
        // the other direction: not a key with no arm, but an arm no key reaches.
        var answered = Keymap.Resolve(KeyStroke.Char('w'), KeymapContext.For(
            Press(new AppState(), KeyStroke.Char('n'))));

        await Assert.That(answered).IsNotNull();

        await Assert.That(ShellCommands.Handled.Contains(answered!.Value)).IsTrue()
            .Because($"{answered} has to END the session. A flight is opened by spawning a "
                   + "child, which may only happen between sessions with the terminal free - "
                   + "so an answer the session handles is an answer nothing acts on.");
    }

    [Test]
    public async Task Choosing_the_agent_hands_over_to_the_agent_and_not_the_editor()
    {
        // AND THE OTHER HALF: that the answer picks the composer it names. The
        // two are separate failures - a key that opens nothing, and a key that
        // opens the wrong thing - and the second is the one the spike shipped.
        // "It opened vim again."
        var chosen = Keymap.Resolve(KeyStroke.Char('m'), KeymapContext.For(
            Press(new AppState(), KeyStroke.Char('n'))));

        await Assert.That(chosen).IsNotNull();
        await Assert.That(ShellCommands.Handled.Contains(chosen!.Value)).IsTrue();

        await Assert.That(chosen).IsNotEqualTo(
            Keymap.Resolve(KeyStroke.Char('w'), KeymapContext.For(
                Press(new AppState(), KeyStroke.Char('n')))))
            .Because("the two answers have to be distinguishable by the time they reach the "
                   + "loop, or it cannot tell which composer somebody picked.");
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

        await Assert.That(catalogued).Contains(KeyStroke.Char('w'));
        await Assert.That(catalogued).Contains(KeyStroke.Char('m'));
    }
}
