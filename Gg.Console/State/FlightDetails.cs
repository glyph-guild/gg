using System.Text;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>One fact about a flight, and what to call it.</summary>
/// <remarks>
/// <b>A record rather than a formatted line, for the reason <see cref="FlightRow"/>
/// is one.</b> These were <c>$"  {label,-10} {value}"</c>, which made every
/// label as wide as the widest one anybody imagined and left the value welded
/// to it - so a person could not put a cursor on the flight id, which is the
/// value most often wanted out of this modal.
/// </remarks>
public sealed record FlightField(string Label, string Value);

/// <summary>
/// What the flight modal shows, in the four shapes it shows them in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure, and split by what the content IS rather than by where it lands.</b>
/// An identity is a heading, an intent is a document somebody wrote, the
/// scalars are fields and the history is a table - and each of those is a
/// different widget. Deciding that in the view would put four layouts where no
/// test can reach them, which is the argument <see cref="PaneText"/> already
/// makes for itself.
/// </para>
/// <para>
/// <b>The log's rows are in <see cref="Rows"/>, with the other tables'.</b> A
/// reader asking where a table's cells come from should find one file, not one
/// per table.
/// </para>
/// </remarks>
public static class FlightDetails
{
    /// <summary>What the frame over the log says.</summary>
    /// <remarks>
    /// It was <i>"what happened, in order:"</i> - a sentence doing a heading's
    /// job, in the imperative, on a box that is plainly a list already. The
    /// order is the table's first column and needs no announcing.
    /// </remarks>
    public const string LogTitle = "Log";

    /// <summary>What the frame over the intent says.</summary>
    public const string IntentTitle = "Intent";

    /// <summary>
    /// The flight this modal is about, named the way a person names it.
    /// </summary>
    /// <remarks>
    /// <b>The number and the name, because the modal is about one flight and
    /// its heading said so of every flight.</b> <c>"This flight"</c> is true
    /// whichever one is open, which is what made it worth nothing across the
    /// top of one. The number is what a person types at <c>gg</c> and the name
    /// is what tells two of them apart.
    /// </remarks>
    public static string Title(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return PaneText.Detailed(state) is { } flight
            ? $"{ControlText.Strip(flight.FlightNumber)}  {ControlText.Strip(flight.Name)}"
            : PaneText.ModalTitle(UiMode.FlightDetail);
    }

    /// <summary>
    /// Why the flight exists, as markdown, whole.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Markdown because that is what a person wrote.</b> <c>gg fly</c> takes
    /// whatever somebody typed, and what somebody types to open a flight is a
    /// paragraph with steps in it often enough that a renderer assuming one line
    /// is wrong about the common case. Handed over as written: this console does
    /// not get to decide that a numbered list was not meant as one.
    /// </para>
    /// <para>
    /// <b>The other two kinds are addresses, so they are code rather than
    /// prose.</b> A uri inside a markdown paragraph is a uri whose underscores
    /// and asterisks are read as emphasis, and the one thing an address must
    /// survive is being copied exactly.
    /// </para>
    /// </remarks>
    public static string Intent(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (PaneText.Detailed(state) is not { } flight)
        {
            // NOT BLANK. An empty pane where the intent goes reads as a flight
            // opened for no reason, which is a thing FlightIntent.Validate
            // refuses to create - so the honest sentence is about the read.
            return "No flight is selected.";
        }

        return flight.Intent.Kind switch
        {
            FlightIntentKinds.Uri => $"`{ControlText.Strip(flight.Intent.Uri)}`",
            FlightIntentKinds.Ticket =>
                $"`{ControlText.Strip(flight.Intent.Provider)}#{ControlText.Strip(flight.Intent.Id)}`",
            _ => ControlText.Strip(flight.Intent.Text, allowLineBreaks: true),
        };
    }

    /// <summary>
    /// The intent as a person would have typed it, for seeding a new flight.
    /// </summary>
    /// <remarks>
    /// <b>Raw, and not what the pane renders.</b> <see cref="Intent"/> wraps a
    /// uri in backticks because that is how it reads on a screen; seeding an
    /// editor with that hands the open path something nobody would have typed.
    /// <para>
    /// <b>Empty when there is nothing on the screen</b>, rather than the
    /// sentence that says so — a flight opened about the words "No flight is
    /// selected." is exactly the record somebody has to explain.
    /// </para>
    /// </remarks>
    public static string IntentToFlyAgain(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (PaneText.Detailed(state) is not { } flight)
        {
            return "";
        }

        return flight.Intent.Kind switch
        {
            FlightIntentKinds.Uri => ControlText.Strip(flight.Intent.Uri),
            FlightIntentKinds.Ticket =>
                $"{ControlText.Strip(flight.Intent.Provider)}#{ControlText.Strip(flight.Intent.Id)}",
            _ => ControlText.Strip(flight.Intent.Text, allowLineBreaks: true),
        };
    }

    /// <summary>
    /// How many lines the intent is, before anything wraps it.
    /// </summary>
    /// <remarks>
    /// The markdown's own lines, which is what can be known without a terminal.
    /// Wrapping only ever adds, and the view caps the answer against the room
    /// it has - so this is a floor, and the floor is the half that matters: a
    /// one-line intent in a box sized for a page is nine empty rows taken off
    /// the log, and one-line intents are the common case.
    /// </remarks>
    public static int IntentLines(AppState state) =>
        Intent(state).ReplaceLineEndings("\n").Split('\n').Length;

    /// <summary>
    /// How tall the intent's frame is, given its lines and the room there is.
    /// </summary>
    /// <param name="lines">What <see cref="IntentLines"/> answered.</param>
    /// <param name="room">
    /// The rows the whole body has. Zero before anything has been laid out,
    /// which is why the cap is skipped rather than applied to nothing.
    /// </param>
    /// <remarks>
    /// <b>Arithmetic, so it can be checked without a terminal.</b> Three is the
    /// floor: two borders and a line. The cap is a share of the body rather
    /// than a number of rows, because the thing being protected is the log
    /// underneath, and how much of it is left is a proportion.
    /// </remarks>
    public static int IntentRows(int lines, int room)
    {
        var wanted = Math.Max(3, lines + 2);

        return room <= 0 ? wanted : Math.Min(wanted, Math.Max(3, room * 45 / 100));
    }

    /// <summary>
    /// The flight's scalars, each with the word a person reads beside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Out of the model, and the model is filled two ways.</b> The flight
    /// comes from the list the boot read. The story comes from the boot for a
    /// flight still in the air, and from <c>ConsoleFlightLog</c> on the keypress
    /// for one that landed - so the fields the story carries are absent rather
    /// than wrong when it was never fetched, and <c>state</c> falls back to the
    /// summary's own reading.
    /// </para>
    /// <para>
    /// <b>Everything that is one value is here, including the ones that are
    /// sentences.</b> Why a flight cannot start, and who is owed something, are
    /// the two answers a person most often opens this for; they are long, and a
    /// long value in a field a cursor can enter is a value that can be read to
    /// the end and copied. A list of them below the fields would have been a
    /// third layout for no gain.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<FlightField> Fields(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (PaneText.Detailed(state) is not { } flight)
        {
            return [];
        }

        var story = PaneText.StoryOf(state, flight.FlightId);
        var fields = new List<FlightField>
        {
            new("id", ControlText.Strip(flight.FlightId)),
        };

        // THE STORY'S SCALARS FIRST, because they answer the question somebody
        // opened this to ask. `state` alone says what became of a flight and
        // never how far it got, so a flight that never started and one that ran
        // and was stopped both read as `open` - which is the pair a person is
        // most often trying to tell apart.
        if (story is not null)
        {
            fields.Add(new FlightField("stage", PaneText.Staged(story.Stage)));
            fields.Add(new FlightField("state", PaneText.Stated(story.State)));

            if (story.HeldBy is { } holder)
            {
                var until = story.HeldUntil is { } expiry ? $" until {expiry:u}" : "";
                fields.Add(new FlightField("held by", ControlText.Strip(holder.Name) + until));
            }
        }
        else
        {
            var ending = PaneText.LoopEndingOf(flight) is { Length: > 0 } outcome
                ? $" · {outcome}"
                : "";
            fields.Add(new FlightField("state", ControlText.Strip(flight.State) + ending));
        }

        fields.Add(new FlightField("opened", $"{flight.CreatedAt:u}"));
        fields.Add(new FlightField("envelope", ControlText.Strip(flight.EnvelopeVersion)));
        fields.Add(new FlightField("attempts", flight.Attempts.ToString(
            System.Globalization.CultureInfo.InvariantCulture)));
        fields.Add(new FlightField("facts", PaneText.FactsOf(flight)));

        // WHY IT CANNOT START, in the contract's own words. The same sentence
        // from the same function the pane and `gg show` render, so one reason is
        // never worded three ways.
        if (story?.Waiting is { } waiting)
        {
            // "why", NOT "waiting". The contract's sentence already opens with
            // the word - `waiting: no runner advertises linux-x64' - so a label
            // reading `waiting' put it on the line twice, which reads as a
            // renderer that has lost track of what it is saying.
            fields.Add(new FlightField(
                "why", ControlText.Strip(Reason.Sentence(waiting.Kind, waiting.Params))));
        }

        // ONE FIELD PER THING OWED, sharing a label. Somebody is owed to two
        // people is two answers, and a label repeated down a column reads as the
        // list it is.
        // "awaiting", because the value is the THING that waits and not the
        // person it waits on. Under the heading it replaces - "waiting on
        // somebody" - the distinction was carried by the indent; on a line of
        // its own, `waiting on stopped on a rule' reads as a sentence that lost
        // its subject.
        foreach (var owed in story?.Outstanding ?? [])
        {
            fields.Add(new FlightField(
                "awaiting",
                ControlText.Strip(FlightStory.Sentence(owed.Kind, owed.Params))));
        }

        return fields;
    }

    /// <summary>
    /// Why the log is empty, or empty when it is not.
    /// </summary>
    /// <remarks>
    /// <b>Two absences, and telling them apart is the whole point.</b> A story
    /// this console never fetched and a flight nothing has been recorded against
    /// are different facts; a person shown the second when the first is true
    /// stops looking. A table cannot say either - a header over no rows claims a
    /// read succeeded and found nothing - so the sentence is drawn where the
    /// rows would have been.
    /// </remarks>
    public static string LogAbsence(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (PaneText.Detailed(state) is not { } flight)
        {
            return "";
        }

        if (PaneText.StoryOf(state, flight.FlightId) is not { } story)
        {
            return "No story was fetched for this flight.";
        }

        return story.Entries.Count == 0
            ? "Nothing has been recorded against it yet."
            : "";
    }

    /// <summary>
    /// Everything above, read top to bottom as one string.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is not what the screen draws, and every word of it is on the
    /// screen.</b> <c>StoryFieldParityTests</c> holds this modal against the
    /// CLI's rendering over one string; composed independently of the widgets
    /// that would be a guard over text nobody sees, which is the defect shape
    /// this repository keeps finding. Composed from the same four producers the
    /// view binds, it is the widgets read in order - and
    /// <c>AFlightIsReadInFieldsRatherThanAWallOfTextTests</c> says so.
    /// </para>
    /// </remarks>
    internal static string Linear(AppState state)
    {
        if (PaneText.Detailed(state) is null)
        {
            return "";
        }

        var text = new StringBuilder();

        text.AppendLine($"  {Title(state)}");
        text.AppendLine();
        text.AppendLine($"  {IntentTitle}");
        text.AppendLine(Intent(state));
        text.AppendLine();

        foreach (var field in Fields(state))
        {
            text.AppendLine($"  {field.Label,-10} {field.Value}");
        }

        text.AppendLine();
        text.AppendLine($"  {LogTitle}");

        if (LogAbsence(state) is { Length: > 0 } absence)
        {
            text.AppendLine($"  {absence}");
            return text.ToString().TrimEnd();
        }

        foreach (var row in Rows.Log(state))
        {
            var attempt = row.Attempt is { Length: > 0 } which ? $"#{which} " : "";
            text.AppendLine($"    {row.Time}  {attempt}{row.Event}");

            if (row.Detail is { Length: > 0 } detail)
            {
                text.AppendLine($"        {detail}");
            }
        }

        return text.ToString().TrimEnd();
    }
}
