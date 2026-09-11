using Gg.Client;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// What an apply came to is readable in full, in a modal, rather than
/// flattened onto a row that cannot hold it.
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS COST SOMEBODY AN EVENING.</b> A real apply was refused because a
/// document named a work-kind nobody had declared — the control plane's own
/// refusal, which <i>points at the door that declares one</i> — and the
/// console showed "Nothing was applied:" with the reason off the right edge.
/// The person saw a key that appeared to do nothing, twice, and went looking
/// for a bug in the apply.
/// </para>
/// <para>
/// <b>The console already learned this exact lesson one modal over.</b>
/// <c>ConsoleLoop</c>'s fly-by-hand arm says it: <i>"the refusal is three
/// sentences and the activity line is one, so the remedy — the half somebody
/// can act on — ran off the right edge of the screen"</i>, and that one was
/// given a modal. The apply outcome never was, and it is the outcome most
/// likely to be long: one line per document, plus a refusal that names paths.
/// </para>
/// <para>
/// <b>So the flattening goes at the source.</b> <c>ConsoleApply.Applied</c>
/// joined its per-document lines with "; " and ran multi-line refusals through
/// a <c>Flatten</c> helper, because its only destination was a one-row slot.
/// It answers in LINES now, and the one-row slot gets a summary that is
/// checked to fit.
/// </para>
/// </remarks>
public class TheApplyOutcomeIsReadableTests
{
    private const string Undeclared =
        "No name 'score-hal' is declared in this airspace, so no document can be applied "
      + "to it. Declare it first with gg airspace name work-kind score-hal --under root.";

    [Test]
    public async Task A_refusal_keeps_every_clause_including_the_remedy()
    {
        var said = ConsoleApply.Applied(
            () => throw new EnvelopeRefusedException(Undeclared));

        var whole = string.Join('\n', said);

        await Assert.That(whole).Contains("gg airspace name work-kind score-hal",
                StringComparison.Ordinal)
            .Because("the remedy is the half somebody acts on, and it is the half a "
                   + "one-row label loses. This is the sentence that was on screen and "
                   + "unreadable for two rounds of asking. Said: " + whole);
    }

    [Test]
    public async Task A_multi_line_refusal_stays_multiple_lines()
    {
        // THE FLATTENING WAS THE DEFECT, not the width. A heading and a path
        // per line is how the client composes an unreadable-file refusal, and
        // joining them into one row is what made the width matter at all.
        var said = ConsoleApply.Applied(
            () => throw new EnvelopeRefusedException(
                "The working copy holds files that sit where a document goes and do not read "
              + "as one. Nothing was applied, because applying the rest would land part of a "
              + "changeset somebody meant as a whole:\n"
              + "  airspace/narrowings/pci.yaml: This does not read as a narrowing.\n"
              + "  airspace/narrowings/soc2.yaml: This does not read as a narrowing."));

        await Assert.That(said.Count).IsGreaterThan(2)
            .Because("a heading and two paths is three lines, and each path is a thing to "
                   + "go and fix. Lines: " + said.Count);

        await Assert.That(said).Contains(l => l.Contains("soc2.yaml", StringComparison.Ordinal))
            .Because("the LAST path matters as much as the first, and it is the one a "
                   + "flattened line loses first.");
    }

    [Test]
    public async Task One_line_per_document_with_landings_and_gates_told_apart()
    {
        var said = ConsoleApply.Applied(() => new VerbResult.AirspaceApplied(new EstateApplied
        {
            Applied =
            [
                new AppliedDocument
                {
                    Name = "score-hal", Path = "airspace/work-kinds/score-hal.yaml",
                    Version = "score-hal@v1", Changed = true,
                },
                new AppliedDocument
                {
                    Name = "root", Path = "airspace/root.yaml",
                    Version = "root@v6", Changed = false,
                    Widens = "repositories", Flight = "GG-91", Awaiting = "platform-owner",
                },
            ],
            Retiring = [],
            Declared = [],
        }));

        await Assert.That(said.Count).IsGreaterThanOrEqualTo(2)
            .Because("one flight per document is one line per document - a count would "
                   + "collapse the two outcomes a person most needs told apart.");

        var landed = said.First(l => l.Contains("score-hal", StringComparison.Ordinal));
        var waiting = said.First(l => l.Contains("root", StringComparison.Ordinal));

        await Assert.That(landed).Contains("score-hal@v1", StringComparison.Ordinal)
            .Because("a minted version is what a person quotes afterwards.");

        await Assert.That(waiting).Contains("GG-91", StringComparison.Ordinal);
        await Assert.That(waiting).Contains("platform-owner", StringComparison.Ordinal)
            .Because("a gate with nobody named is a gate a person cannot go and ask about.");
    }

    [Test]
    public async Task The_activity_line_gets_a_summary_that_actually_fits()
    {
        // THE ROW IS ONE ROW AND THAT IS FINE - what was wrong was putting the
        // whole report in it. A summary is checked against the narrowest
        // screen this console supports, so this cannot regress into the same
        // defect by growing a clause.
        var summary = ConsoleApply.Summary(ConsoleApply.Applied(
            () => throw new EnvelopeRefusedException(Undeclared)));

        await Assert.That(summary).DoesNotContain("\n", StringComparison.Ordinal)
            .Because("the slot is one row; a newline in it is a line nobody sees.");

        await Assert.That(summary.Length).IsLessThanOrEqualTo(PaneText.QuestionColumns)
            .Because($"it has to fit the narrowest screen anybody here has. Was "
                   + $"{summary.Length}: {summary}");
    }

    [Test]
    public async Task The_outcome_opens_over_the_console_by_itself()
    {
        // NOT ON A KEY. Somebody who just pressed `y` is owed the answer
        // without having to discover a second keystroke - the same reason the
        // hand-flight refusal opens itself.
        var after = Reducer.ApplyAnswered(new AppState
        {
            ApplyOutcome = ["score-hal applied as score-hal@v1"],
        });

        await Assert.That(after.Mode).IsEqualTo(UiMode.ReadingOutcome);

        var quiet = Reducer.ApplyAnswered(new AppState());

        await Assert.That(quiet.Mode).IsEqualTo(UiMode.Normal)
            .Because("an apply with nothing to say opens nothing, or every refresh would "
                   + "put a box over the console.");
    }

    [Test]
    public async Task The_modal_renders_it_and_is_sized_as_a_document()
    {
        var state = new AppState
        {
            Mode = UiMode.ReadingOutcome,
            ApplyOutcome = ["Nothing was applied.", "", Undeclared],
        };

        await Assert.That(PaneText.ApplyLines(state, 0))
            .Contains(l => l.Contains("gg airspace name", StringComparison.Ordinal));

        await Assert.That(PaneText.Modal(state))
            .Contains("gg airspace name", StringComparison.Ordinal)
            .Because("a mode that falls through to the empty default is a title over "
                   + "nothing.");

        await Assert.That(PaneText.ModalIsADocument(UiMode.ReadingOutcome)).IsTrue()
            .Because("a report of one line per document plus a refusal that names paths "
                   + "is read down, and a clipped Label is the defect the scrolling list "
                   + "exists to avoid.");
    }

    [Test]
    public async Task It_is_the_third_view_of_the_reading_modal()
    {
        // ONE MODAL, THREE VIEWS. Comparing what happened against what governs
        // and what is still pending is why they sit together, and inside a
        // modal the letters are free.
        var outcome = new AppState
        {
            Mode = UiMode.ReadingOutcome,
            ActiveTab = TabId.Envelope,
            ApplyOutcome = ["something"],
        };

        await Assert.That(Keymap.Resolve(KeyStroke.Char('d'), KeymapContext.For(outcome)))
            .IsEqualTo(Command.ReadChangeset);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('e'), KeymapContext.For(outcome)))
            .IsEqualTo(Command.ReadEnvelope);

        await Assert.That(Keymap.Resolve(KeyStroke.Esc, KeymapContext.For(outcome)))
            .IsEqualTo(Command.CloseModal)
            .Because("one way out, from every view.");

        // AND REACHABLE BACK, so somebody who pressed `d` to check the diff can
        // return to what just happened.
        var changeset = outcome with { Mode = UiMode.ReadingChangeset };

        await Assert.That(Keymap.Resolve(KeyStroke.Char('o'), KeymapContext.For(changeset)))
            .IsEqualTo(Command.ReadOutcome);
    }
}
