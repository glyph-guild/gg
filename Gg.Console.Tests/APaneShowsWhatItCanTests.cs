using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// A pane is handed what it could show, and told about the rest.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured, on a real tenant, with the console's own timing hook.</b>
/// Returning to the queue tab spent <b>1,973ms</b> on one statement -
/// <c>_flight.Text = …</c> - against <b>32ms</b> to build the string it was
/// assigning. The flight pane is a <c>Label</c> filling its frame, and a Label
/// does not scroll: every line past the bottom was already invisible, and
/// Terminal.Gui laid out all of them anyway.
/// </para>
/// <para>
/// <b>So the lines beyond the pane cost two seconds and showed nothing.</b>
/// This is the cut, and the sentence that keeps it honest - a console that
/// silently dropped the end of a log would be telling somebody a flight
/// recorded less than it did.
/// </para>
/// </remarks>
public class APaneShowsWhatItCanTests
{
    private static string Lines(int many) =>
        string.Join('\n', Enumerable.Range(1, many).Select(at => $"line {at}"));

    [Test]
    public async Task Text_that_fits_is_handed_over_whole()
    {
        // THE ORDINARY CASE, AND IT MUST NOT BE TOUCHED. Most flights have a
        // handful of entries, and a cut that rewrote those would be a change to
        // what the pane says for the sake of one that does not fit.
        var short_ = Lines(12);

        await Assert.That(PaneText.WhatAPaneCanShow(short_)).IsEqualTo(short_);
    }

    [Test]
    public async Task And_text_exactly_at_the_bound_is_too()
    {
        var exact = Lines(200);

        await Assert.That(PaneText.WhatAPaneCanShow(exact)).IsEqualTo(exact)
            .Because("the bound is how many it may keep, so keeping exactly that many "
                   + "withholds nothing and has nothing to report.");
    }

    [Test]
    public async Task Beyond_it_the_pane_keeps_what_it_could_show()
    {
        var long_ = Lines(5000);

        var shown = PaneText.WhatAPaneCanShow(long_);

        await Assert.That(shown).StartsWith("line 1\nline 2\n")
            .Because("the top is what a pane shows, so the top is what it keeps - cutting "
                   + "from the front would move the text a person is reading.");
        await Assert.That(shown).Contains("line 200");
        await Assert.That(shown).DoesNotContain("line 500");
    }

    [Test]
    public async Task And_says_how_much_it_withheld()
    {
        // NOT SILENTLY. A log that stops at two hundred lines with no sentence
        // is a flight that appears to have recorded two hundred lines.
        var shown = PaneText.WhatAPaneCanShow(Lines(5000));

        await Assert.That(shown).Contains("4800 more lines");
        await Assert.That(shown).Contains("cannot scroll");
    }

    [Test]
    public async Task A_pane_with_one_enormous_line_is_left_alone()
    {
        // THE CUT IS BY LINE, so text with no newline in it has no line to cut
        // at. Terminal.Gui wraps it, which costs - but truncating mid-sentence
        // would be this console editing somebody's words, and that is worse.
        var one = new string('x', 100_000);

        await Assert.That(PaneText.WhatAPaneCanShow(one)).IsEqualTo(one);
    }
}
