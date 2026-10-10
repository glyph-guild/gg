namespace Gg.Console.Tests;

/// <summary>
/// After five minutes of nobody touching it, the console shows the mark and
/// nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Orthogonal to the mode, like freezing and not like a modal.</b> A
/// screensaver that was a <see cref="UiMode"/> would have to remember what it
/// covered and put it back, and this console has exactly one slot for that -
/// spent by the watch. A flag beside <c>Frozen</c> covers whatever is there and
/// uncovers the same thing, including a modal, including another modal over
/// that one.
/// </para>
/// <para>
/// <b>Every key wakes it, and the keymap says so.</b> The rule here is that
/// bindings live in one place and advertised keys are exactly the live ones -
/// so "any key" is a real binding list rather than a special case in a view.
/// All of it is off the hint line, because a hint line listing ninety-five
/// keys is not a hint.
/// </para>
/// <para>
/// <b>Counted in ticks, not clock time.</b> The console already has a
/// once-a-second timer and the state has to survive the terminal being handed
/// away, so idleness is a number in the model rather than a timestamp against
/// a clock somebody has to inject.
/// </para>
/// </remarks>
public class AScreensaverCoversAnIdleConsoleTests
{
    [Test]
    public async Task Five_minutes_of_nothing_brings_up_the_mark()
    {
        await Assert.That(Screensaver.After).IsEqualTo(300)
            .Because("five minutes, counted by the once-a-second tick that already exists.");

        var state = new AppState();

        for (var second = 0; second < Screensaver.After; second++)
        {
            state = Reducer.Idled(state);
        }

        await Assert.That(state.Screening).IsTrue();
    }

    [Test]
    public async Task And_a_second_before_that_it_does_not()
    {
        var state = new AppState();

        for (var second = 0; second < Screensaver.After - 1; second++)
        {
            state = Reducer.Idled(state);
        }

        await Assert.That(state.Screening).IsFalse()
            .Because("the boundary is the whole assertion; an off-by-one here is four "
                   + "minutes fifty-nine or five minutes one and nobody could tell.");
    }

    [Test]
    public async Task Anything_a_person_does_puts_the_clock_back()
    {
        var nearly = new AppState { IdleTicks = Screensaver.After - 1 };

        await Assert.That(Reducer.Touched(nearly).IdleTicks).IsEqualTo(0);

        // AND ONE MORE TICK AFTER THAT DOES NOT TRIGGER IT, which is the half
        // that would be missed by resetting the count and leaving the flag.
        await Assert.That(Reducer.Idled(Reducer.Touched(nearly)).Screening).IsFalse();
    }

    // ---- the shortcut ----

    [Test]
    public async Task It_can_be_asked_for_outright()
    {
        // ctrl+g, obscure on purpose and free: c, d, f, o, r and v are taken
        // and g is the only letter that means anything here. TWICE NOW (slice
        // sixty-nine): the first arms the mux's switch, as it does beside an
        // agent, and the second is the mark it always was.
        var armed = Reducer.Reduce(
            new AppState(),
            Keymap.Resolve(KeyStroke.Control('g'), KeymapContext.For(new AppState()))!.Value);

        await Assert.That(
            Keymap.Resolve(KeyStroke.Control('g'), KeymapContext.For(armed)))
            .IsEqualTo(Command.ShowScreensaver);

        await Assert.That(Reducer.Reduce(new AppState(), Command.ShowScreensaver).Screening)
            .IsTrue();
    }

    // ---- waking ----

    [Test]
    public async Task Space_escape_or_enter_wakes_it()
    {
        // NOT EVERY KEY, AND THE CONSOLE'S OWN GUARDS DECIDED THAT. Binding all
        // ninety-five printable keys was written first and backed out: it
        // satisfies the rule that advertised keys are the live ones, and then
        // puts a hundred and twenty rows on the help page, because the
        // catalogue lists what is bound.
        var context = KeymapContext.For(new AppState { Screening = true });

        foreach (var key in new[] { KeyStroke.Char(' '), KeyStroke.Esc, KeyStroke.EnterKey })
        {
            await Assert.That(Keymap.Resolve(key, context)).IsEqualTo(Command.WakeScreen)
                .Because($"{key.Name} is what a person presses at a screen showing nothing.");
        }
    }

    [Test]
    public async Task And_waking_puts_back_exactly_what_was_covered()
    {
        // THE MODE IS NOT TOUCHED, which is the reason this is a flag and not a
        // mode. A watch open over a flight modal is two levels of something to
        // put back, and the console has one slot to remember them in.
        var screening = new AppState
        {
            Mode = UiMode.Normal,
            ActiveTab = TabId.Board,
            Screening = true,
            IdleTicks = Screensaver.After,
        };

        var woken = Reducer.Reduce(screening, Command.WakeScreen);

        await Assert.That(woken.Screening).IsFalse();
        await Assert.That(woken.IdleTicks).IsEqualTo(0);
        await Assert.That(woken.Mode).IsEqualTo(UiMode.Normal);
        await Assert.That(woken.ActiveTab).IsEqualTo(TabId.Board)
            .Because("it covered the board and the board is what is there again.");
    }

    [Test]
    public async Task A_question_somebody_left_open_is_not_covered()
    {
        // A MODAL IS NOT A CONSOLE AT REST. It is a question waiting for
        // somebody, and covering it would mean waking to a question already
        // asked and no longer on the screen.
        //
        // It is also what keeps waking to one set of keys: the help page lists
        // what is bound per mode, and a mark that could be up over any of the
        // thirty would have put its three keys on all thirty.
        var reading = new AppState { Mode = UiMode.FlightDetail };

        for (var second = 0; second < Screensaver.After * 2; second++)
        {
            reading = Reducer.Idled(reading);
        }

        await Assert.That(reading.Screening).IsFalse();
    }

    [Test]
    public async Task And_nothing_underneath_answers_while_it_is_up()
    {
        // PRESSING x AT A SCREENSAVER MUST NOT GROUND A FLIGHT. The mark
        // outranks the mode, so the keys underneath it are unreachable until it
        // is gone - which is the half that makes "wake" safe rather than the
        // half that makes it convenient.
        var screening = new AppState { Mode = UiMode.Normal, Screening = true };

        // EVERY KEY WAKES IT SINCE 2026-10-10 ("fix j too") - and waking is all it does:
        // f answers WakeScreen, never the act it means on the plain console.
        await Assert.That(Keymap.Resolve(KeyStroke.Char('f'), KeymapContext.For(screening)))
            .IsEqualTo(Command.WakeScreen)
            .Because("f is a key on the plain console, and the person pressing it was looking "
                   + "at a screen with nothing on it.");
    }

    // ---- what it shows ----

    [Test]
    public async Task It_is_the_loading_mark_and_it_breathes()
    {
        // THE SAME MARK, NOT A SECOND ONE. LoadingArt already holds the letters,
        // the cosine that fades them and the shimmer that changes them; a
        // screensaver drawing its own would be a second thing to keep in
        // agreement with the first.
        await Assert.That(Screensaver.Showing(new AppState { Screening = true })).IsTrue();
        await Assert.That(Screensaver.Showing(new AppState())).IsFalse();

        // AND IT MOVES WHILE IT IS UP, which is the difference between a
        // screensaver and a console that has crashed showing a logo.
        await Assert.That(LoadingArt.Of(0)).IsNotEquivalentTo(LoadingArt.Of(LoadingArt.Breath / 2));
    }

    [Test]
    public async Task And_it_breathes_half_as_fast_as_a_tab_that_is_loading()
    {
        // TWICE AS SLOW, AND FOR A DIFFERENT JOB. A loading tab's mark answers
        // "is this coming?" for a second or two and wants to look busy. This
        // one is what a room looks at for an hour, and at the loading pace it
        // is a thing pulsing at somebody rather than a thing at rest.
        await Assert.That(Screensaver.Breath).IsEqualTo(LoadingArt.Breath * 2);

        // THE SAME CURVE STRETCHED, not a second curve. One breath in, one
        // breath out, over twice the ticks - so the loading mark's low point is
        // this one's peak.
        await Assert.That(LoadingArt.Glow(LoadingArt.Breath, Screensaver.Breath))
            .IsGreaterThan(0.99)
            .Because("half of a slow breath is the top of it.");

        await Assert.That(LoadingArt.Glow(LoadingArt.Breath))
            .IsLessThan(0.36)
            .Because("a whole fast breath is back at the bottom, which is the pace this "
                   + "is halving.");
    }

    [Test]
    public async Task And_the_shimmer_slows_with_it()
    {
        // BOTH HALVES OR NEITHER. The letters settling as the mark brightens is
        // tied to the same wave, so a glow that slowed while the ink kept its
        // old pace would come apart - dim and still, or bright and churning.
        await Assert.That(LoadingArt.Of(LoadingArt.Breath, Screensaver.Breath))
            .IsNotEquivalentTo(LoadingArt.Of(LoadingArt.Breath));
    }

    [Test]
    public async Task In_the_mux_it_covers_the_column_too()
    {
        // THE OWNER, 2026-10-10: "when it is embedded in the mux, the screensaver should
        // be fullscreen (and also takeover the sidebar)". The mark covered every view in
        // the console's content, and the mux's column lives in the window's PADDING - so
        // it stayed lit beside a screen that had gone dark.
        var awake = new AppState();
        var screening = awake with { Screening = true };

        await Assert.That(MuxColumn.Shown(awake)).IsTrue();
        await Assert.That(MuxColumn.Room(awake)).IsEqualTo(MuxColumn.Width);

        await Assert.That(MuxColumn.Shown(screening)).IsFalse()
            .Because("a screensaver with the sidebar still lit beside it is a console with a "
                   + "picture on half of it.");
        await Assert.That(MuxColumn.Room(screening)).IsEqualTo(0)
            .Because("the column's room is given back, so the mark is centred on the whole "
                   + "terminal rather than on what the column left.");
    }

    [Test]
    public async Task Every_key_wakes_it_even_one_a_focused_table_wants()
    {
        // SEEN IN A PTY, 2026-10-10: `j` did not wake the mark, while esc and space did.
        // The keymap already answers every key with WakeScreen while the mark is up - but
        // a key goes to the FOCUSED view first, and the queue table under the mark takes
        // `j` as a cursor move before the screen's handler ever sees it.
        //
        // So the screen takes keys where it takes the mouse: from the application, before
        // any view is chosen. That is a property of where a handler is attached, which no
        // state-level test can see, so it is held over the source the way the mouse's is.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("_app.Keyboard.KeyDown += OnKeyBeforeRouting;")
            .Because("only a handler on the application sees a key before the focused view.");
        await Assert.That(screen).Contains("_app.Keyboard.KeyDown -= OnKeyBeforeRouting;")
            .Because("a handler on the application outlives the screen unless it is released, "
                   + "and the console builds a new screen on every return from a child.");
    }

    [Test]
    public async Task Any_key_wakes_it_and_does_nothing_else()
    {
        // THE OWNER, 2026-10-10: "fix j too". The mark woke on esc, space and enter and
        // swallowed every other key - so a person pressing j, or any letter, saw nothing
        // happen and could not tell whether gg had hung. Every key wakes it now, and still
        // does nothing else: a key that both woke the console and acted would act on a
        // screen nobody was looking at.
        var screening = new KeymapContext(UiMode.Normal) { Screening = true };

        // NO CAPITALS, which this console never receives: a letter arrives without its shift.
        foreach (var key in KeymapTests.Universe.Where(k => k != Keymap.Interrupt
                     && k.Input is not (>= 'A' and <= 'Z')))
        {
            await Assert.That(Keymap.Resolve(key, screening)).IsEqualTo(Command.WakeScreen)
                .Because($"'{key.Name}' is a key somebody might press to come back.");
        }

        // AND THE HINT LINE STILL NAMES THE THREE IT ALWAYS DID, rather than ninety.
        var hinted = Keymap.Bindings(screening).Where(b => !b.OffTheHintLine).Select(b => b.Key).ToList();
        await Assert.That(hinted).IsEquivalentTo(
            (KeyStroke[])[KeyStroke.Esc, KeyStroke.Char(' '), KeyStroke.EnterKey]);
    }
}
