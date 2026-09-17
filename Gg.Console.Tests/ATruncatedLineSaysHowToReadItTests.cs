using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// A message too long for the activity line says which key opens the whole of
/// it, and that key opens a modal holding every word.
/// </summary>
/// <remarks>
/// <para>
/// <b>The line drops what does not fit and says nothing about it.</b>
/// <c>_activity</c> is one <c>Label</c> one row deep, so a sentence wider than
/// the terminal is drawn up to the edge and the rest is simply gone — and
/// nothing on the screen distinguishes a message that ended from a message that
/// was cut. Three modals in this console exist because one particular sentence
/// ran off that edge (<c>HandFlight</c>'s refusal, the runner's path, the
/// apply's outcome); this is the same repair made once, for whatever is on the
/// line.
/// </para>
/// <para>
/// <b>Offered only while there is something to read.</b> An advertised key that
/// does nothing is the dead key Article XI names, so the binding is conditional
/// on the measurement — and the measurement is a fact about the terminal, which
/// is why it is in the model rather than read off a widget at dispatch time.
/// The hint line and the dispatch then read one answer, the way every other
/// conditional key here does.
/// </para>
/// <para>
/// <b>At the right-hand end, where the console's own keys are.</b> It is about
/// the console rather than about the tab, and it goes in front of the three
/// that are always true so <c>q quit</c> keeps the far right it was given.
/// </para>
/// </remarks>
public class ATruncatedLineSaysHowToReadItTests
{
    /// <summary>Longer than any terminal this is measured against.</summary>
    private const string TooLong =
        "the runner refused the credential because its own configuration does not say "
      + "accept-configured, which is a decision made on that machine rather than a "
      + "permission here";

    private static AppState Said(string? what, int columns) =>
        new() { LastAction = what, SaidColumns = columns };

    [Test]
    public async Task A_line_that_fits_offers_nothing()
    {
        var standing = Keymap.HintsStanding(KeymapContext.For(Said("sent", 80)));

        await Assert.That(standing).DoesNotContain("ctrl+r")
            .Because("there is nothing behind the end of a sentence that ended, and a key "
                   + "offered where it does nothing teaches a person the console is broken.");
    }

    [Test]
    public async Task A_clipped_line_says_which_key_reads_it()
    {
        var standing = Keymap.HintsStanding(KeymapContext.For(Said(TooLong, 80)));

        await Assert.That(standing).Contains("ctrl+r read")
            .Because("a message a person cannot see the end of has to say how to see it. "
                   + "Standing: " + standing);
    }

    [Test]
    public async Task And_quit_keeps_the_far_right()
    {
        var standing = Keymap.HintsStanding(KeymapContext.For(Said(TooLong, 80)));

        await Assert.That(standing.IndexOf("ctrl+r", StringComparison.Ordinal))
            .IsLessThan(standing.IndexOf("q quit", StringComparison.Ordinal))
            .Because("the far right was asked for and given to quit; a key that comes and "
                   + "goes must not push it around. Standing: " + standing);
    }

    [Test]
    public async Task An_unmeasured_line_offers_nothing()
    {
        // BEFORE THE FIRST LAYOUT, and after a model built by hand. Zero is
        // "nobody has measured", which cannot be read as "everything is
        // clipped": that would put a key on the line of every console that has
        // not painted yet.
        var standing = Keymap.HintsStanding(KeymapContext.For(Said(TooLong, 0)));

        await Assert.That(standing).DoesNotContain("ctrl+r");
    }

    [Test]
    public async Task The_key_opens_the_whole_message()
    {
        var clipped = KeymapContext.For(Said(TooLong, 80));

        await Assert.That(Keymap.Resolve(KeyStroke.Control('r'), clipped))
            .IsEqualTo(Command.ReadSaid);

        var after = Reducer.Reduce(Said(TooLong, 80), Command.ReadSaid);

        await Assert.That(after.Mode).IsEqualTo(UiMode.ReadingSaid);
    }

    [Test]
    public async Task The_modal_holds_every_word()
    {
        var lines = PaneText.SaidLines(Said(TooLong, 80), columns: 40);

        await Assert.That(string.Join(' ', lines)).IsEqualTo(TooLong)
            .Because("the whole point is the tail the line dropped, so the modal carries the "
                   + "message entire and only the wrapping is new.");

        await Assert.That(lines.All(line => line.Length <= 40)).IsTrue()
            .Because("it is wrapped to the modal's width, which is what makes it readable "
                   + "where the one-row line was not.");
    }

    [Test]
    public async Task The_width_is_measured_rather_than_assumed()
    {
        var measured = Reducer.SaidMeasured(new AppState(), 64);

        await Assert.That(measured.SaidColumns).IsEqualTo(64);

        await Assert.That(Reducer.SaidMeasured(measured, 64)).IsSameReferenceAs(measured)
            .Because("a resize that changed nothing must not make a new model: the screen "
                   + "renders on every one, and re-rendering on every layout pass is the "
                   + "flicker AutoRefresh already argued about.");
    }
}
