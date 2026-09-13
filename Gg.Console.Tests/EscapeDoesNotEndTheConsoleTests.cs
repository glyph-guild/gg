using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// Escape on the console itself does nothing, and doing nothing takes work.
/// </summary>
/// <remarks>
/// <para>
/// <b>MEASURED IN A PTY: PRESSING ESCAPE QUIT GG.</b> The console booted, ran,
/// took one escape and exited with status zero — no message, no confirmation,
/// nothing on the screen to say what had happened. A person reaching for the
/// key that backs out of things, with nothing to back out of, lost their
/// console.
/// </para>
/// <para>
/// <b>Nothing in this repository asked for that.</b> <c>Keymap.EscapeHatch</c>
/// answers null in Normal mode — there is no modal to leave — and
/// <c>Resolve</c> answers nothing, so the screen's handler returned without
/// marking the key handled. Terminal.Gui then applied its own meaning: escape
/// on a runnable stops it, which ends the session, which ends gg.
/// </para>
/// <para>
/// <b>So a key the keymap declines can still have to be TAKEN.</b> That is a
/// statement about what a key means, which is the keymap's to make — and
/// asking it is what makes this checkable without a terminal, where the whole
/// defect lived.
/// </para>
/// </remarks>
public class EscapeDoesNotEndTheConsoleTests
{
    [Test]
    public async Task Escape_means_nothing_on_the_console_and_is_taken_anyway()
    {
        var console = new KeymapContext(UiMode.Normal);

        await Assert.That(Keymap.Resolve(KeyStroke.Esc, console)).IsNull()
            .Because("there is no modal to leave, which is what EscapeHatch already says.");

        await Assert.That(Keymap.Swallowed(KeyStroke.Esc, console)).IsTrue()
            .Because("handed on, it reaches Terminal.Gui, which stops the runnable - and a "
                   + "console that vanishes on one keystroke is the worst kind of quit: "
                   + "silent, immediate, and nowhere advertised. `q` is how gg is left.");
    }

    [Test]
    public async Task Nothing_else_is_swallowed_because_swallowing_is_the_exception()
    {
        var console = new KeymapContext(UiMode.Normal);

        // A SCREEN THAT TOOK EVERY KEY IT DID NOT UNDERSTAND would be a screen
        // no widget under it could ever hear from - and the tables' own arrows,
        // which never reach the keymap at all, are how three of these panes are
        // walked.
        foreach (var key in (KeyStroke[])[
            KeyStroke.Char('§'), KeyStroke.Char('~'), KeyStroke.EnterKey, KeyStroke.TabKey])
        {
            await Assert.That(Keymap.Swallowed(key, console)).IsFalse()
                .Because($"{key} is not a key with a destructive meaning of its own.");
        }
    }

    [Test]
    public async Task Inside_a_modal_escape_is_answered_rather_than_swallowed()
    {
        foreach (var mode in Modals.Drawn)
        {
            var context = new KeymapContext(mode);

            await Assert.That(Keymap.Resolve(KeyStroke.Esc, context)).IsNotNull()
                .Because($"{mode} has exactly one way out and escape is it.");

            await Assert.That(Keymap.Swallowed(KeyStroke.Esc, context)).IsFalse()
                .Because("a key the keymap answers is dispatched, not eaten.");
        }
    }

    [Test]
    public async Task The_screen_asks_the_keymap_rather_than_deciding_for_itself()
    {
        var screen = ConsoleSource.Text("Gg.Console", Path.Combine("Views", "ConsoleScreen.cs"));

        await Assert.That(screen).Contains("Keymap.Swallowed")
            .Because("what a key MEANS has one authority, and 'take it and do nothing' is a "
                   + "meaning - one the screen was making up by omission.");
    }
}
