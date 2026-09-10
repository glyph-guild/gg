using System.Text.RegularExpressions;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// A person reads one word for the airspace; the code keeps another.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three words were in play and two of them meant the same thing.</b>
/// ADR-0014 settled that <c>envelope</c> names one document. ADR-0016 then
/// introduced <i>estate</i> in prose for the whole set a tenant holds in force,
/// and named the verb family <c>airspace</c> — leaving, as a carried-open
/// question, whether the two should be reconciled: <i>"the likely line is scope
/// rather than document type… but this wants use in anger before it is
/// fixed."</i>
/// </para>
/// <para>
/// <b>The use in anger has happened, and it produced the mismatch the question
/// anticipated.</b> A person ran <c>gg airspace pull</c> and was told it pulled
/// <i>the estate</i> — one verb, a different noun, and nothing on any surface
/// connecting them. So the answer is: <b>airspace is the word a person reads</b>,
/// because it is the one already on the verb they typed and on the tab they are
/// looking at, and <i>estate</i> stays as internal vocabulary where the types
/// already use it.
/// </para>
/// <para>
/// <b>Envelope stays as a label, deliberately.</b> The tab still renders the
/// composed envelope in force, <c>gg envelope show|apply|validate</c> still
/// names one document, and <c>e</c> is still the key. What changed is the
/// collective noun, which was the only one doing two jobs.
/// </para>
/// <para>
/// <b>A scan rather than a list of strings, because a rename is exactly the
/// change that half-lands.</b> Naming today's eighteen would fix today and guard
/// nothing; the next sentence somebody writes is the one that reintroduces it.
/// </para>
/// </remarks>
public class OneWordForItWhereAPersonReadsItTests
{
    /// <summary>
    /// Every quoted string in production source, by file and line.
    /// </summary>
    /// <remarks>
    /// <b>Lines whose whole content is a comment are skipped, and that is the
    /// point of the whole test.</b> <c>estate</c> is still the internal word —
    /// it is in type names, locals and every doc comment explaining why — so a
    /// scan that read comments would demand a rename this decision explicitly
    /// did not ask for.
    /// </remarks>
    private static IReadOnlyList<(string File, int Line, string Text)> QuotedStrings()
    {
        var found = new List<(string, int, string)>();

        foreach (var file in ConsoleSource.In("Gg.Console", "Gg.Cli", "Gg.Client"))
        {
            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimStart();
                if (line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var quoted in Regex.Matches(lines[i], "\"([^\"]*)\""))
                {
                    found.Add((Path.GetFileName(file), i + 1, ((Match)quoted).Groups[1].Value));
                }
            }
        }

        return found;
    }

    [Test]
    public async Task The_scan_actually_reads_the_strings()
    {
        // THE LIVENESS ANCHOR. A scan that found nothing would make the
        // assertion below pass over a product that said estate on every screen.
        var strings = QuotedStrings();

        await Assert.That(strings.Count).IsGreaterThan(500)
            .Because("this product is mostly sentences; a scan finding a handful has stopped "
                   + "reading.");

        await Assert.That(strings.Any(s => s.Text.Contains("airspace", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the word this test is about has to be findable by it.");
    }

    [Test]
    public async Task Nothing_a_person_reads_calls_it_an_estate()
    {
        var said = QuotedStrings()
            .Where(s => s.Text.Contains("estate", StringComparison.OrdinalIgnoreCase))
            .Select(s => $"{s.File}:{s.Line}")
            .ToList();

        await Assert.That(said).IsEmpty()
            .Because("one word where a person reads it, and it is the one on the verb they "
                   + "typed. `estate` stays in the types and the comments, which is where a "
                   + "reader of the code is already holding both. Found: "
                   + string.Join(", ", said));
    }

    [Test]
    public async Task The_tab_is_called_airspace()
    {
        await Assert.That(Tabs.Name(TabId.Envelope)).IsEqualTo("Airspace")
            .Because("it is the tab the airspace verbs act on, and a tab called Envelope "
                   + "beside a verb called airspace made a person learn two words for one "
                   + "thing.");
    }

    [Test]
    public async Task The_tab_keeps_its_key_and_says_so_on_the_bar()
    {
        // `e` STAYS, and it is worth saying why rather than leaving a mismatch
        // to look accidental. `a` is flight actions and taking it would shadow
        // them; of what is free in Normal, none of `o`, `w` or `z` says
        // airspace. So the key is muscle memory and the tab still holds the
        // envelope in force - which is the label this rename deliberately kept.
        await Assert.That(Tabs.KeyFor(TabId.Envelope)).IsEqualTo(KeyStroke.Char('e'));

        await Assert.That(Tabs.Title(new AppState(), TabId.Envelope))
            .StartsWith("Airspace")
            .Because("the bar is built from the name and the key, so a person sees which "
                   + "letter reaches it rather than guessing from the word.");
    }

    [Test]
    public async Task Envelope_is_still_the_word_for_one_document()
    {
        // THE OTHER HALF OF THE DECISION, and a control on the scan above: a
        // rename that had swept `envelope` out of the UI too would have taken
        // the name ADR-0014 settled for the document class, and `gg envelope
        // show` with it.
        // Read from the same scan, because Gg.Console.Tests cannot see Gg.Cli -
        // the console does not reference the composition root, deliberately.
        var advertised = QuotedStrings()
            .Any(s => s.Text.StartsWith("gg envelope show", StringComparison.Ordinal));

        await Assert.That(advertised).IsTrue()
            .Because("one document is an envelope, which ADR-0014 settled; what was doing "
                   + "two jobs was the collective noun.");
    }
}
