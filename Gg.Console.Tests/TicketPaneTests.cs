using Gg.Client;
using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The flight pane renders a work item intent, and every advertised kind.
/// </summary>
/// <remarks>
/// <para>
/// <b>The second of the two surfaces that render an intent</b>, and the reason
/// it gets its own file rather than a line in the client's: it is a different
/// assembly with its own copy of the same switch, and fixing the one you
/// happened to run is exactly how two renderings of one thing drift.
/// </para>
/// <para>
/// <b>The fall-through is the defect.</b> Both switches named <c>uri</c> and
/// let everything else fall to <c>Text</c> — so a ticket, whose text is null by
/// construction, printed <c>intent</c> followed by nothing at all. A pane that
/// silently lacks a value reads as a flight that has nothing to say, which is
/// the sentence this file's neighbours were written around.
/// </para>
/// </remarks>
public class TicketPaneTests
{
    private static AppState With(FlightIntent intent) =>
        ConsoleProjection.Apply(new AppState(), new VerbResult.Flight(new FlightSummary
        {
            FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
            FlightNumber = "GG-4471",
            Name = "from-a-work-item",
            Intent = intent,
            CreatedAt = new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero),
            RunnerProtocolVersion = 1,
            FactVocabularyVersion = "0.1.0",
            ConstitutionVersion = "1.0.0",
            EnvelopeVersion = "none",
            Attempts = 1,
            Facts = [],
        }));

    [Test]
    public async Task The_pane_renders_a_work_item_rather_than_an_empty_line()
    {
        var pane = PaneText.Flight(With(new FlightIntent
        {
            Kind = FlightIntentKinds.Ticket,
            Provider = "a-tracker",
            Id = "4471",
        }));

        await Assert.That(pane).Contains("a-tracker#4471")
            .Because("the two halves render together, in the shape gg fly --ticket takes, so "
                   + "what a person reads back is what they could type again.");
    }

    [Test]
    public async Task Every_advertised_kind_reaches_the_pane()
    {
        // THE TOTALITY ASSERTION, which is what stops the next kind shipping
        // blank on this surface while somebody fixes the other one.
        foreach (var kind in FlightIntentKinds.All)
        {
            var (intent, needle) = kind switch
            {
                FlightIntentKinds.Uri => (
                    new FlightIntent { Kind = kind, Uri = "https://example.invalid/needle-uri" },
                    "needle-uri"),
                FlightIntentKinds.Ticket => (
                    new FlightIntent { Kind = kind, Provider = "tracker", Id = "needle-ticket" },
                    "needle-ticket"),
                _ => (new FlightIntent { Kind = kind, Text = "needle-text" }, "needle-text"),
            };

            await Assert.That(PaneText.Flight(With(intent))).Contains(needle)
                .Because($"'{kind}' is advertised as a kind, so its payload must reach the pane.");
        }
    }

    [Test]
    public async Task The_intent_line_is_never_left_blank()
    {
        // The poison twin for the assertion above: a pane that printed the
        // label and nothing after it would satisfy a Contains on the label, and
        // that is precisely the state a ticket was in before this slice.
        var pane = PaneText.Flight(With(new FlightIntent
        {
            Kind = FlightIntentKinds.Ticket,
            Provider = "a-tracker",
            Id = "4471",
        }));

        var line = pane.Split('\n').Single(l => l.Contains("intent", StringComparison.Ordinal));

        await Assert.That(line.Split("intent")[^1].Trim()).IsNotEmpty()
            .Because("'intent' followed by whitespace is the shape the fall-through produced, "
                   + "and it reads as a flight nobody gave a reason for.");
    }
}
