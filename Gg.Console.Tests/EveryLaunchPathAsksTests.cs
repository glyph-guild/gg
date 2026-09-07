namespace Gg.Console.Tests;

/// <summary>
/// The three ways a flight is opened from this console, and which of them can
/// offer a choice of composer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two of the three compose text, and the third does not.</b> `n` writes an
/// intent from nothing and `y` writes one for a flight somebody is about to fly
/// themselves — both hand a child an empty buffer and take back what comes out.
/// `f` flies a work item: what crosses is a provider and an id, and the item IS
/// the intent, so there is no text for either composer to produce.
/// </para>
/// <para>
/// <b>That third case is why S33.4-04 exists.</b> A path that cannot offer both
/// has to say which it is using rather than choosing silently — otherwise the
/// same key means "you were asked" on two paths and "you were not" on a third,
/// with nothing on screen to say so.
/// </para>
/// </remarks>
public class EveryLaunchPathAsksTests
{
    private static AppState Press(AppState state, KeyStroke key) =>
        Keymap.Resolve(key, KeymapContext.For(state)) is { } command
            ? Reducer.Reduce(state, command)
            : state;

    [Test]
    [Arguments('n', "opening a flight from nothing")]
    [Arguments('y', "flying one by hand")]
    public async Task Every_path_that_composes_text_asks_which_way(char key, string what)
    {
        // ONE QUESTION, WHICHEVER DOOR. Both of these hand a child an empty
        // buffer and take back what it produced, so the choice between an editor
        // and an agent means exactly the same thing on each - and a modal
        // offered on one but not the other would be a person having to remember
        // which doors ask.
        var asked = Press(new AppState(), KeyStroke.Char(key));

        await Assert.That(asked.Mode).IsEqualTo(UiMode.ComposeChoice)
            .Because($"{what} composes an intent, so it asks how.");
    }

    [Test]
    [Arguments('n', ComposingFor.NewFlight)]
    [Arguments('y', ComposingFor.HandFlight)]
    public async Task The_modal_remembers_which_question_it_is_asking(
        char key, ComposingFor asking)
    {
        // WHICH QUESTION IS STATE; WHICH ANSWER IS NOT. The question has to
        // survive until somebody answers it, because it is on the screen. The
        // answer travels as a command and is gone the moment it is handled -
        // which is what stops a choice made for one flight deciding the next.
        var asked = Press(new AppState(), KeyStroke.Char(key));

        await Assert.That(asked.ComposingFor).IsEqualTo(asking)
            .Because("the two answers are the same two keys on both paths, so the loop can "
                   + "only tell what to do from the question that was asked.");
    }

    [Test]
    public async Task Nothing_is_being_asked_when_no_question_is_open()
    {
        // The pair to the row above: a field that meant something while no modal
        // was up would be a decision waiting to be acted on by whatever came
        // next.
        await Assert.That(new AppState().ComposingFor).IsEqualTo(ComposingFor.Nothing);

        var escaped = Press(Press(new AppState(), KeyStroke.Char('n')), KeyStroke.Esc);

        await Assert.That(escaped.ComposingFor).IsEqualTo(ComposingFor.Nothing)
            .Because("escaping closes the question, and a question left open behind a closed "
                   + "modal is one the next keypress could answer by accident.");
    }

    [Test]
    public async Task Flying_a_work_item_says_it_did_not_ask()
    {
        // S33.4-04, AND THE HONEST ANSWER FOR THIS PATH. `f` sends a provider
        // and an id: the work item is the intent, and there is no text for
        // either composer to write. Offering the choice here would mean
        // replacing the ticket with prose, which throws away the one thing
        // flying from the browser is for - the link back to the item.
        //
        // So it does not ask, and it says so. A path that quietly used one
        // composer while its neighbours asked would teach somebody that the
        // question is optional.
        var flown = Press(new AppState(), KeyStroke.Char('f'));

        await Assert.That(flown.Mode).IsNotEqualTo(UiMode.ComposeChoice)
            .Because("there is nothing to compose: the work item already is the intent.");

        var said = PaneText.ComposedBy(ComposingFor.WorkItem);

        await Assert.That(said).IsNotEmpty();
        await Assert.That(said).Contains("work item", StringComparison.OrdinalIgnoreCase)
            .Because("a person who was asked twice and not a third time is owed the reason, "
                   + $"and this is the only place it can be given. Said: '{said}'");
    }

    [Test]
    public async Task The_help_page_names_the_key_on_every_path_that_asks()
    {
        // A key that is only in the source is a key nobody finds, and that is
        // twice as true for a modal reached from two different doors.
        var catalogued = Keymap.Catalogue()
            .Where(entry => entry.Mode == UiMode.ComposeChoice)
            .Select(entry => entry.Binding.Key)
            .ToList();

        foreach (var key in (KeyStroke[])[KeyStroke.Char('w'), KeyStroke.Char('m')])
        {
            await Assert.That(catalogued).Contains(key);
        }
    }
}
