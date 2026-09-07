using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The CLI and the pane show the same facts about a story.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two layouts by decision, one field set by guard.</b> The console's panes
/// are compact for a reason and the CLI's are not, so a shared renderer was
/// considered and declined. What replaces it is this: the layouts may differ and
/// the set of facts may not.
/// </para>
/// <para>
/// <b>Nine documents are rendered twice in this repository and exactly one is
/// shared.</b> <c>PaneText.Envelope</c> calls the CLI's own renderer with the
/// comment <i>"A second layout of one document is two views that drift"</i> —
/// and the eight beside it drifted: the flight pane showed no state, no
/// attempts, no waiting reason, and dropped the log's detail entirely. This
/// guard is the only thing standing between the story and the ninth.
/// </para>
/// <para>
/// <b>Made to fail before it is believed.</b> A parity guard that has never
/// reddened is a guard nobody has checked the direction of, so the last test
/// removes a field from one side and asserts it notices.
/// </para>
/// </remarks>
public class StoryFieldParityTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A story carrying every optional member, because an absent one proves nothing.
    /// </summary>
    /// <remarks>
    /// A fixture with a null <c>HeldBy</c> would let both surfaces omit the holder
    /// and agree — parity over an empty set is parity nobody can rely on.
    /// </remarks>
    private static FlightStory Full()
    {
        StoryEntry[] entries =
        [
            new StoryEntry
            {
                At = At,
                Kind = StoryKinds.LeaseGranted,
                Stage = FlightStages.Of(StoryKinds.LeaseGranted),
                Params = ["a-runner"],
                Attempt = 2,
                Actor = new Actor { Kind = ActorKinds.Runner, Name = "a-runner" },
            },
            new StoryEntry
            {
                At = At.AddMinutes(20),
                Kind = StoryKinds.ObligationHalted,
                Stage = FlightStages.Of(StoryKinds.ObligationHalted),
                Params = [],
                Said = "the move bound could not be measured",
            },
        ];

        return new FlightStory
        {
            FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
            FlightNumber = FlightRef.Format(42),
            WorkKind = "audit",
            Stage = FlightStoryStages.Reached(entries),
            State = FlightStates.Open,
            Waiting = Reason.For(ReasonKinds.NoRunnerAdvertises, ["linux-x64"]),
            HeldBy = new Actor { Kind = ActorKinds.Person, Name = "kevin" },
            HeldUntil = At.AddMinutes(30),
            Outstanding = [entries[1]],
            Entries = entries,
        };
    }

    /// <summary>
    /// What both surfaces must show, and the value that proves each is shown.
    /// </summary>
    /// <remarks>
    /// Values rather than labels, because a label is a layout choice and a value
    /// is the fact. "state" appearing in both proves two headings agree; <c>open</c>
    /// appearing in both proves a reader learns the same thing.
    /// </remarks>
    private static IReadOnlyDictionary<string, string> Facts(FlightStory story) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["the flight number"] = story.FlightNumber,
            ["the stage it reached"] = story.Stage,
            ["what became of it"] = story.State,
            ["what it waits on"] = Reason.Sentence(story.Waiting!.Kind, story.Waiting.Params),
            ["who holds it now"] = story.HeldBy!.Name,
            ["each entry, as a sentence"] =
                FlightStory.Sentence(story.Entries[0].Kind, story.Entries[0].Params),
            ["what somebody wrote"] = story.Entries[1].Said!,
        };

    /// <summary>
    /// The flight the modal is opened on, which is chosen from the list.
    /// </summary>
    /// <remarks>
    /// <c>PaneText.Detailed</c> picks the row under the cursor out of the flight
    /// list, so the modal needs one there to be about. Only the two fields that
    /// decide which flight it is are meaningful; the rest exist because the
    /// contract requires them.
    /// </remarks>
    private static Gg.Contracts.FlightSummary Listed(FlightStory story) => new()
    {
        FlightId = story.FlightId,
        FlightNumber = story.FlightNumber,
        Name = story.WorkKind ?? "work",
        Intent = new Gg.Contracts.FlightIntent
        {
            Kind = Gg.Contracts.FlightIntentKinds.Text,
            Text = "work",
        },
        CreatedAt = new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.25.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v6",
        Attempts = 1,
        State = story.State,
        Facts = [],
    };

    private static string Cli(FlightStory story) =>
        VerbOutput.ToText(new VerbResult.Story(story));

    private static string Pane(FlightStory story) =>
        PaneText.Flight(new AppState { Story = story });

    /// <summary>
    /// The third rendering, and the one a person opens to read a history.
    /// </summary>
    /// <remarks>
    /// <b>It was not in slice thirty-two's scope because it did not exist when
    /// the scope was written.</b> S32.4-02 fixed the pane - "it prints
    /// entry.Kind today and drops the detail entirely, so a person reads WHAT
    /// happened and never WHAT IT SAYS" - and the modal was doing exactly that,
    /// full screen, which is where somebody goes when the pane is not enough.
    /// </remarks>
    private static string Modal(FlightStory story) =>
        PaneText.Modal(new AppState
        {
            Mode = UiMode.FlightDetail,
            Story = story,
            Flights = new Gg.Contracts.FlightList
            {
                Flights = [Listed(story)],
            },
        });

    // ---- S32.4-03 ----

    [Test]
    public async Task Every_surface_shows_every_fact()
    {
        var story = Full();

        var surfaces = new (string Named, string Rendered)[]
        {
            ("the CLI", Cli(story)),
            ("the pane", Pane(story)),
            ("the modal", Modal(story)),
        };

        var missing = (from surface in surfaces
                       from fact in Facts(story)
                       where !surface.Rendered.Contains(fact.Value, StringComparison.Ordinal)
                       select $"{surface.Named} does not show {fact.Key}")
            .ToList();

        await Assert.That(missing).IsEmpty()
            .Because("three surfaces keep their own layouts by decision, so this is the only "
                   + "thing holding them together. Found: " + string.Join("; ", missing));
    }

    [Test]
    public async Task The_guard_notices_a_field_only_one_side_shows()
    {
        // MADE TO FAIL, in the only direction a fixture can make it. A story
        // whose holder appears in neither rendering must not read as parity: it
        // is a fact both surfaces dropped, and this asserts the comparison would
        // have caught it rather than shrugging at the absence.
        var story = Full();

        await Assert.That(Cli(story).Contains("kevin", StringComparison.Ordinal)).IsTrue();
        await Assert.That(Pane(story).Contains("kevin", StringComparison.Ordinal)).IsTrue();

        // The same comparison over a value NEITHER shows: it must be reported,
        // twice, rather than passing because both agree about nothing.
        var absent = new List<string>();

        foreach (var surface in (string[])[Cli(story), Pane(story)])
        {
            if (!surface.Contains("a-runner-nobody-has", StringComparison.Ordinal))
            {
                absent.Add("missing");
            }
        }

        await Assert.That(absent).Count().IsEqualTo(2)
            .Because("agreement about a fact neither surface carries is not parity, and a "
                   + "guard that reported nothing here would pass on two blank renderings.");
    }
}
