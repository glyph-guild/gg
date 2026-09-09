using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A modal that asks something is wrapped to a width a screen has.
/// </summary>
/// <remarks>
/// <para>
/// <b>The box sizes itself to its longest LINE.</b> <c>ConsoleScreen</c> takes
/// <c>body.Max(line =&gt; line.Length) + 4</c>, which is right when the body is
/// already broken into lines and catastrophic when it is not: one
/// two-hundred-character sentence asks for a two-hundred-column dialog, and a
/// terminal that has eighty gives back a box running off the side with its text
/// unwrapped.
/// </para>
/// <para>
/// <b>Hand-wrapping is what the existing bodies do and it is not the fix.</b>
/// The sizing code says so itself: hand-wrapping "breaks a sentence where the
/// box ends rather than where it reads — and goes wrong silently the moment
/// somebody rewords it." It went wrong the moment somebody wrote a new one.
/// </para>
/// <para>
/// <b>Wrapped where it can be checked without a terminal</b>, which is this
/// console's rule for anything about layout: the arithmetic is in
/// <c>PaneText</c> and a test reads it, rather than in a view where only a
/// person looking at a screen would ever know.
/// </para>
/// <para>
/// <b>Documents are exempt and say why.</b> Help, the flight detail and the
/// runner modal take the screen and lay themselves out in columns; wrapping
/// those would fold a table in half.
/// </para>
/// </remarks>
public class AQuestionBoxFitsOnTheScreenTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 9, 9, 3, 30, 0, TimeSpan.Zero);

    /// <summary>Every mode, over a model with something in it to describe.</summary>
    private static AppState Filled(UiMode mode) => new()
    {
        Mode = mode,
        ActiveTab = TabId.Flights,
        Machine = "Kevins-MBP",
        FlightSelected = 0,
        RunnerSelected = 0,
        Flights = new FlightList
        {
            Flights =
            [
                new FlightSummary
                {
                    FlightId = "01a08431-a096-72cf-8c8f-55ed2233f2f8",
                    FlightNumber = "GG-81",
                    Name = "a flight with a name long enough to be worth wrapping around",
                    Intent = new FlightIntent
                    {
                        Kind = FlightIntentKinds.Text,
                        Text = "count slowly from one to forty, one number per line",
                    },
                    CreatedAt = T0,
                    RunnerProtocolVersion = 1,
                    FactVocabularyVersion = "0.25.0",
                    ConstitutionVersion = "1.0.0",
                    EnvelopeVersion = "v6",
                    Attempts = 1,
                    Facts = [],
                },
            ],
        },
        Runners = new RunnerList
        {
            Runners =
            [
                new RunnerSummary
                {
                    RunnerId = "01a06572-a784-72ae-b951-f147553cd48e",
                    Label = "vmlinux001",
                    State = RunnerStates.Busy,
                    CurrentFlightNumber = "GG-81",
                    CurrentFlightId = "01a08431-a096-72cf-8c8f-55ed2233f2f8",
                    LastHeartbeatAt = T0,
                    Labels = [],
                },
            ],
        },
    };

    [Test]
    public async Task The_box_is_measured_on_wrapped_text()
    {
        // WHERE THE PROPERTY LIVES. The first version of this wrapped inside
        // PaneText.Modal and asserted over its output - which broke a test
        // asserting a sentence the wrap had split, and revealed that sixteen
        // other files reading modal prose were passing only because their
        // phrases happened not to straddle a line break. Wrapping the CONTENT
        // put a whole file of assertions at the mercy of where words fall.
        //
        // So the wrap is presentation and lives in the view, and what is
        // asserted here is that the view measures what it wrapped rather than
        // what it was given. Sizing on unwrapped text is the defect; sizing on
        // wrapped text cannot produce it.
        var screen = Sources.Read("Gg.Console", Path.Combine("Views", "ConsoleScreen.cs"));

        var measuring = screen[screen.IndexOf(
            "var body = ", StringComparison.Ordinal)..];
        measuring = measuring[..measuring.IndexOf("var wide", StringComparison.Ordinal)];

        await Assert.That(measuring).Contains("PaneText.Wrapped")
            .Because("the width is the longest LINE, so an unwrapped paragraph is a request "
                   + "for a dialog wider than the terminal. Measured: " + measuring.Trim());

        await Assert.That(measuring).Contains("ModalIsADocument")
            .Because("a document lays itself out in columns and folding one would break a "
                   + "table in half.");
    }

    [Test]
    public async Task Every_question_wraps_to_something_a_screen_has()
    {
        // AND THE CONTENT REALLY DOES FIT once wrapped, over every mode that
        // asks something - which is the half a structural check cannot see: a
        // body could be wrapped and still carry one unbreakable line.
        var offenders = new List<string>();

        foreach (var mode in Enum.GetValues<UiMode>())
        {
            if (PaneText.ModalIsADocument(mode))
            {
                continue;
            }

            var wrapped = PaneText.Wrapped(
                PaneText.Modal(Filled(mode)), PaneText.QuestionColumns);

            foreach (var line in wrapped.Split('\n'))
            {
                if (line.Length > PaneText.QuestionColumns)
                {
                    offenders.Add($"{mode}: {line.Length} cols — {line}");
                }
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("a single word longer than the line is left whole on purpose, so this "
                   + "finding means a body carries one - a url or a path that wants its own "
                   + "line. Found:\n" + string.Join("\n", offenders));
    }

    [Test]
    public async Task Wrapping_breaks_between_words_and_keeps_the_paragraphs()
    {
        // NOT MID-WORD, and not by collapsing what somebody wrote. A blank line
        // is a paragraph break the author put there, and a wrapper that ate it
        // would run a question and its explanation together.
        var wrapped = PaneText.Wrapped(
            "Open a new flight on GG-81's intent?\n\nThe editor opens on what it said, so "
          + "you can change it first.",
            columns: 30);

        var lines = wrapped.Split('\n');

        await Assert.That(lines[0]).IsEqualTo("Open a new flight on GG-81's");
        await Assert.That(lines).Contains("")
            .Because("the blank line between the question and its explanation is the author's "
                   + "and must survive. Got:\n" + wrapped);
        await Assert.That(lines.All(l => l.Length <= 30)).IsTrue()
            .Because("Got:\n" + wrapped);
        await Assert.That(lines.Any(l => l.Contains("  ", StringComparison.Ordinal))).IsFalse()
            .Because("a wrap that left double spaces is one that split on the wrong thing.");
    }

    [Test]
    public async Task A_word_longer_than_the_line_is_left_alone()
    {
        // A URL, A FLIGHT ID, A PATH. Breaking one mid-way makes it
        // uncopyable, which is worse than a box one line too wide - and this is
        // the case a naive wrapper loops forever on.
        var wrapped = PaneText.Wrapped(
            "see https://forge.example/acme/widgets/issues/1?with=a&very=long&query=string here",
            columns: 20);

        await Assert.That(wrapped.Split('\n').Any(l => l.Contains("https://", StringComparison.Ordinal)
                && l.Length > 20)).IsTrue()
            .Because("a link is one word and breaking it makes it uncopyable. Got:\n" + wrapped);
    }
}
