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
        // and g is the only letter that means anything here.
        await Assert.That(
            Keymap.Resolve(KeyStroke.Control('g'), KeymapContext.For(new AppState())))
            .IsEqualTo(Command.ShowScreensaver);

        await Assert.That(Reducer.Reduce(new AppState(), Command.ShowScreensaver).Screening)
            .IsTrue();
    }

    // ---- waking ----

    [Test]
    public async Task Any_key_at_all_wakes_it()
    {
        var context = KeymapContext.For(new AppState { Screening = true });

        foreach (var key in new[]
                 {
                     KeyStroke.Char('j'), KeyStroke.Char('x'), KeyStroke.Char(' '),
                     KeyStroke.Char('~'), KeyStroke.Esc, KeyStroke.EnterKey,
                     KeyStroke.Control('f'),
                 })
        {
            await Assert.That(Keymap.Resolve(key, context)).IsEqualTo(Command.WakeScreen)
                .Because($"{key.Name} is a person at the keyboard, and that is the whole "
                       + "of what a screensaver is listening for.");
        }
    }

    [Test]
    public async Task And_waking_puts_back_exactly_what_was_covered()
    {
        // THE MODE IS NOT TOUCHED, which is the reason this is a flag and not a
        // mode. A watch open over a flight modal is two levels of something to
        // put back, and the console has one slot to remember them in.
        var watching = new AppState
        {
            Mode = UiMode.Watching,
            ModeBeneath = UiMode.FlightDetail,
            Screening = true,
            IdleTicks = Screensaver.After,
        };

        var woken = Reducer.Reduce(watching, Command.WakeScreen);

        await Assert.That(woken.Screening).IsFalse();
        await Assert.That(woken.IdleTicks).IsEqualTo(0);
        await Assert.That(woken.Mode).IsEqualTo(UiMode.Watching);
        await Assert.That(woken.ModeBeneath).IsEqualTo(UiMode.FlightDetail);
    }

    [Test]
    public async Task And_the_key_that_woke_it_does_nothing_else()
    {
        // PRESSING x TO WAKE MUST NOT GROUND A FLIGHT. Every key resolving to
        // WakeScreen is what guarantees it, and this is that guarantee written
        // down where somebody changing the arm will see it.
        var screening = new AppState { Mode = UiMode.FlightDetail, Screening = true };

        await Assert.That(Keymap.Resolve(KeyStroke.Char('x'), KeymapContext.For(screening)))
            .IsEqualTo(Command.WakeScreen)
            .Because("x grounds a flight in this very mode, and the person pressing it "
                   + "was looking at a screensaver.");
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
}
