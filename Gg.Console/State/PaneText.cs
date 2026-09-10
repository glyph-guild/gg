using System.Text;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// What each pane says, as a pure function of the model.
/// </summary>
/// <remarks>
/// <para>
/// Separate from the views so the rendering can be tested without a terminal,
/// and so a pane cannot reach past the model for something to show. Everything
/// here reads <see cref="AppState"/> and nothing else - no client, no clock,
/// no file.
/// </para>
/// <para>
/// The flight-shaped fields it reads are the CONTRACT types the verbs return,
/// which is what makes the console and <c>--json</c> two renderings of one
/// result rather than two implementations that agree today.
/// </para>
/// <para>
/// Everything is stripped on the way out. Text reaches the store already clean
/// - stripping happens at ingress - so in a healthy system this removes
/// nothing. It is here because this is the last code between a control plane
/// and a terminal.
/// </para>
/// </remarks>
public static class PaneText
{
    /// <summary>
    /// The flight the cursor is on, or null when there is none.
    /// </summary>
    /// <remarks>
    /// <b>The list AS SHOWN, newest first</b>, which is the order the pane
    /// renders and therefore the only order a cursor on a screen can mean. And
    /// null rather than the first flight when the list is empty: the reducer
    /// reads this to decide whether there is anything to open, so answering
    /// with something would open a modal about a flight nobody pointed at.
    /// </remarks>
    public static FlightSummary? Detailed(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Flights is not { Flights.Count: > 0 } listed)
        {
            return null;
        }

        var shown = listed.Flights.OrderByDescending(f => f.CreatedAt).ToList();

        return shown[Math.Clamp(state.FlightSelected, 0, shown.Count - 1)];
    }

    /// <summary>
    /// Everything known about one flight, read top to bottom.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Composed, and no longer what the screen draws.</b> This was a
    /// StringBuilder of two-space indents standing in for columns, and it was
    /// the whole modal: identity, scalars, the reason it cannot start and the
    /// entire history in one <c>Label</c>. The screen now binds
    /// <see cref="FlightDetails"/> to a frame title, a markdown view, read-only
    /// fields and a table.
    /// </para>
    /// <para>
    /// <b>It stays, because <c>StoryFieldParityTests</c> reads it</b> - and it
    /// is composed FROM those same producers, so what it holds is what the
    /// widgets hold. A rendering assembled independently would be a parity
    /// guard over text nobody sees.
    /// </para>
    /// </remarks>
    private static string FlightDetail(AppState state) => FlightDetails.Linear(state);

    /// <summary>
    /// The story this console holds for one flight, or null.
    /// </summary>
    /// <remarks>
    /// <b>Checked against the flight it is about.</b> One story is held at a
    /// time and the cursor moves without fetching, so a modal that trusted
    /// whatever was last read would caption one flight's history with another
    /// flight's name - which is the defect the flight pane was fixed for one
    /// slice earlier, arriving through a different door.
    /// </remarks>
    internal static Gg.Contracts.FlightStory? StoryOf(AppState state, string flightId) =>
        state.Story is { } story
        && string.Equals(story.FlightId, flightId, StringComparison.Ordinal)
            ? story
            : null;

    /// <summary>
    /// What one tab's pane says, whichever tab it is.
    /// </summary>
    /// <remarks>
    /// <b>Because a tab a person can reach is a tab that has to say
    /// something.</b> Every view is on the bar from the start, so tabbing lands
    /// on views the shell has not fetched - and a pane that draws blank there
    /// is indistinguishable from a broken one. Each renderer already answers
    /// for its own absence; this is the one place that says all of them do,
    /// which is what <c>TabsTakeTheWholeScreenTests</c> asserts over every tab.
    /// </remarks>
    public static string ForTab(AppState state, TabId tab)
    {
        ArgumentNullException.ThrowIfNull(state);

        return tab switch
        {
            // The queue is a list rather than a block of text, so its rows are
            // joined here: what this answers is "does this tab say anything".
            TabId.Queue => string.Join("\n", QueueRows(state)),
            TabId.Flights => Flights(state),
            TabId.Runners => Runners(state),
            TabId.Live => Live(state),
            TabId.Browse => Browse(state),
            TabId.Repositories => Repositories(state),
            TabId.Envelope => Envelope(state),
            TabId.Allowances => FleetAllowances(state),
            _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "unknown tab"),
        };
    }

    /// <summary>One line per flight needing me.</summary>
    public static IReadOnlyList<string> QueueRows(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Above the rows, and present when there are none. An empty queue is
        // the state a broken egress produces, so a notice shown only alongside
        // flights would be invisible in the case it exists for.
        var notices = state.Notices.Select(NoticeRow);

        if (state.Queue.Count == 0)
        {
            // Nothing needing me and nothing printed look identical, and one of
            // them is a queue that failed to load.
            return
            [
                .. notices,
                // AND WHERE TO LOOK INSTEAD. "Nothing needs you" is true of a
                // tenant whose flight asked a question the envelope never
                // turned into a gate, and a person reading it had nowhere to
                // go. The tab is on the title line above; this is the sentence
                // that sends them to it.
                state.Diagnosis is { Length: > 0 }
                    ? "(could not load)"
                    : "nothing needs you · tab for every recent flight",
            ];
        }

        return
        [
            .. notices,
            .. state.Queue.Select(row =>
            {
                var unread = row.UnreadArrivals > 0 ? $" ({row.UnreadArrivals})" : "";
                return Clean($"{row.FlightNumber,-9} {Reason(row.Reason),-18} {row.Name}{unread}");
            }),
        ];
    }

    /// <summary>
    /// A degradation, with what to do about it when there is something.
    /// </summary>
    /// <remarks>
    /// Rendered whole and never rewritten. gg names no forge, so the sentence
    /// is the control plane's - and a console that said something was broken
    /// without saying what to do would send somebody to a support channel to
    /// be told a sentence we already had.
    /// </remarks>
    private static string NoticeRow(TenantNotice notice) =>
        Clean(notice.Remedy is { Length: > 0 } remedy
            // No trailing separator when there is no remedy: a dash with
            // nothing after it reads as text that got cut off.
            ? $"! {notice.Detail} - {remedy}"
            : $"! {notice.Detail}");

    /// <summary>Why a flight is in the queue, in words rather than an enum name.</summary>
    public static string Reason(QueueReason reason) => reason switch
    {
        QueueReason.AwaitingDecision => "awaiting a decision",
        QueueReason.LeaseExpiredTwice => "expired twice",
        QueueReason.RunnerOffline => "runner offline",
        // Article XI: a reason nothing can render halts rather than showing a
        // blank cell that reads as "nothing wrong".
        _ => throw new InvalidOperationException(
            $"Queue reason '{reason}' has no rendering. A row nobody can explain must not be shown as one that needs nothing."),
    };

    /// <summary>
    /// The selected flight: state, pinned refs, credential identity, facts.
    /// </summary>
    /// <remarks>
    /// Thin, and honestly thin. Pinned refs arrive with materialize, credential
    /// identity at step 5 and facts at step 6 - each is named as absent rather
    /// than omitted, because a pane that silently lacks a section reads as a
    /// flight that has nothing to say.
    /// </remarks>
    /// <summary>
    /// Every flight this tenant has recently, newest first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE PANE THAT ANSWERS "where did the thing I just started go".</b>
    /// The queue is flights NEEDING ME and it is right to be: a flight with no
    /// gate, no expired lease and no stranded runner needs nobody. GG-52 was
    /// exactly that - an agent asked a question, the envelope had no obligation
    /// conditioned on one, so the flight landed needing nothing - and the
    /// console said "nothing needs you", which was true and left a person with
    /// nowhere to look.
    /// </para>
    /// <para>
    /// <b>Over a read that already happened.</b> <c>AppState.Flights</c> is
    /// what the boot fetched to derive the queue; this costs no request.
    /// </para>
    /// <para>
    /// <b>The loop's outcome sits beside the flight's state, and that is the
    /// column worth having.</b> GG-52 is <c>landed</c> AND its loop was
    /// <c>blocked</c>: the flight reached an ending and the work did not. A row
    /// carrying only the state reads as a success. Nothing is invented for a
    /// flight whose facts say nothing about a loop - "completed" there would be
    /// the console answering for a runner.
    /// </para>
    /// </remarks>
    public static string Flights(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Flights is not { } list)
        {
            // NOT "no flights". A tenant with none and a request that did not
            // answer are different facts, and a person shown the first when the
            // second happened stops looking.
            return "  (could not load the flight list)";
        }

        if (list.Flights.Count == 0)
        {
            return "  no flights yet. `n` opens one.";
        }

        var text = new StringBuilder();

        foreach (var flight in list.Flights.OrderByDescending(f => f.CreatedAt))
        {
            // Newest first, because a person opens this pane after doing
            // something and the thing they did is the thing they are looking
            // for.
            text.AppendLine(
                $"  {Clean(flight.FlightNumber),-8}{Clean(flight.State),-10}"
              + $"{LoopEnding(flight),-10}{Age(flight.CreatedAt),-6}{Clean(flight.Name)}");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// How this flight's last loop ended, or nothing.
    /// </summary>
    /// <remarks>
    /// The LAST one by observation, because a flight can run more than one
    /// attempt and the newest is the one a person is deciding about. Empty
    /// rather than a word when no <c>loop.outcome</c> fact reached the control
    /// plane: a runner that has not spoken has not said "completed".
    /// </remarks>
    internal static string LoopEndingOf(FlightSummary flight) => LoopEnding(flight);

    /// <summary>How long ago, for a table that has its own idea of columns.</summary>
    internal static string AgeOf(DateTimeOffset created) => Age(created);

    private static string LoopEnding(FlightSummary flight) =>
        flight.Facts
            .Where(f => string.Equals(f.Kind, FactKinds.LoopOutcome, StringComparison.Ordinal))
            .OrderByDescending(f => f.ObservedAt)
            .Select(f => f.Loop?.Outcome)
            .FirstOrDefault(outcome => outcome is { Length: > 0 }) is { } ending
            ? Clean(ending)
            : "";

    /// <summary>How long ago, in the coarsest unit that is still true.</summary>
    /// <remarks>
    /// A timestamp is what the flight pane shows, and it is the right answer
    /// there. On a list of twenty rows it is twenty things to subtract, so this
    /// says 3m, 2h, 4d - and nothing at all for a clock that disagrees with the
    /// row, because a negative age reads as a bug in the row rather than in the
    /// clock.
    /// </remarks>
    private static string Age(DateTimeOffset created)
    {
        var since = DateTimeOffset.UtcNow - created;

        return since switch
        {
            { Ticks: < 0 } => "",
            { TotalMinutes: < 1 } => "now",
            { TotalHours: < 1 } => $"{(int)since.TotalMinutes}m",
            { TotalDays: < 1 } => $"{(int)since.TotalHours}h",
            _ => $"{(int)since.TotalDays}d",
        };
    }

    public static string Flight(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Diagnosis is { Length: > 0 } diagnosis)
        {
            return Clean(diagnosis, lines: true);
        }

        var story = state.Story;
        var flight = state.Flight;

        if (story is null && flight is null)
        {
            // NOT AN EMPTY PANE. A read that has not answered and a flight
            // nothing has happened to are different facts, and a person shown
            // the second when the first is true stops looking.
            return state.Selected is null ? "  (no flight selected)" : "loading…";
        }

        var text = new StringBuilder();

        // WHICHEVER ARRIVED, and both when both did. The two reads answer
        // separately and a pane that waited for the pair would go blank whenever
        // either failed - which is the case a person is most likely to be looking
        // at it in.
        text.AppendLine(
            $"  {Clean(flight?.FlightNumber ?? story!.FlightNumber)}  "
          + $"{Clean(flight?.Name ?? story?.WorkKind)}".TrimEnd());
        text.AppendLine($"  id            {Clean(flight?.FlightId ?? story!.FlightId)}");

        // STAGE AND STATE, which this pane has never shown. Two axes - how far it
        // got and what became of it - so a person can tell a flight that is still
        // going from one that finished without leaving the console.
        if (story is not null)
        {
            text.AppendLine($"  stage         {Staged(story.Stage)}");
            text.AppendLine($"  state         {Stated(story.State)}");

            if (story.HeldBy is { } holder)
            {
                var until = story.HeldUntil is { } expiry ? $" until {expiry:u}" : "";
                text.AppendLine($"  held by       {Clean(holder.Name)}{until}");
            }
        }

        if (flight is not null)
        {
            text.AppendLine($"  opened        {flight.CreatedAt:u}");
            text.AppendLine($"  intent        {Intent(flight.Intent)}");
            text.AppendLine($"  constitution  {Clean(flight.ConstitutionVersion)}");
            text.AppendLine($"  envelope      {Clean(flight.EnvelopeVersion)}");
            text.AppendLine($"  vocabulary    {Clean(flight.FactVocabularyVersion)}");
        }

        text.AppendLine();
        text.AppendLine("  pinned refs   (none until the flight is materialized)");
        text.AppendLine($"  credential    {Credentials(state)}");

        if (flight is not null)
        {
            text.AppendLine($"  facts         {FactsOf(flight)}");
        }

        // WHAT IT WAITS ON, in the contract's own words. `gg show` renders the same
        // sentence from the same function, so this pane and that verb cannot word
        // one reason two ways.
        if (story?.Waiting is { } waiting)
        {
            text.AppendLine($"  {Clean(Gg.Contracts.Reason.Sentence(waiting.Kind, waiting.Params))}");
        }

        text.AppendLine();
        text.AppendLine(Why(state));

        if (story is { Outstanding.Count: > 0 })
        {
            text.AppendLine();
            text.AppendLine("  waiting on somebody");
            foreach (var owed in story.Outstanding)
            {
                text.AppendLine($"    {Clean(Gg.Contracts.FlightStory.Sentence(owed.Kind, owed.Params))}");
            }
        }

        if (story is { Entries.Count: > 0 })
        {
            text.AppendLine();
            foreach (var entry in story.Entries)
            {
                var attempt = entry.Attempt is { } which ? $"#{which} " : "";

                // A SENTENCE, NOT A KIND. This printed `entry.Kind` and dropped the
                // detail, so a halt read as `obligation-halted` and the diagnosis -
                // the only thing that says what to do about it - went nowhere.
                text.AppendLine($"  {entry.At:u}  {attempt}"
                              + Clean(Gg.Contracts.FlightStory.Sentence(entry.Kind, entry.Params)));

                if (entry.Said is { Length: > 0 } said)
                {
                    text.AppendLine($"      {Clean(said, lines: true)}");
                }
            }
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>A stage this build can name, or a refusal.</summary>
    /// <remarks>
    /// <b>The CLI's own argument, in the second surface.</b> A stage outside the
    /// six is a flight standing somewhere this build has no word for, and printing
    /// it raw puts a value nobody chose in front of a person deciding what to do.
    /// </remarks>
    internal static string Staged(string stage) =>
        FlightStages.All.Contains(stage, StringComparer.Ordinal)
            ? stage
            : throw new InvalidOperationException(
                $"Flight stage '{stage}' has no published name, so this pane cannot show it "
              + "as one that does.");

    /// <summary>A state this build can name, or a refusal.</summary>
    /// <remarks>
    /// The plausible guess is <c>open</c>, and it would show a finished flight as
    /// one somebody is still working on.
    /// </remarks>
    internal static string Stated(string state) =>
        FlightStates.All.Contains(state, StringComparer.Ordinal)
            ? state
            : throw new InvalidOperationException(
                $"Flight state '{state}' has no published name, so this pane cannot show it "
              + "as one that does.");

    /// <summary>
    /// The rules in force, as the command line prints them.
    /// </summary>
    /// <remarks>
    /// <b>Rendered by the CLI's own renderer.</b> A second layout of one
    /// document is two views that drift, and this is the document arguments are
    /// had about.
    /// <para>
    /// <b>An unread envelope is not an absent one.</b> "No envelope is in force"
    /// means every flight is ungoverned, which is a sentence somebody would act
    /// on immediately - so it may only be said when it was asked and answered.
    /// </para>
    /// </remarks>
    public static string Envelope(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // THE DOCUMENTS FIRST, THEN THE ANSWER THEY COMPOSE TO. A person on
        // this pane is either reading what governs or looking for where to
        // change it, and the second question is the one the pane could not
        // answer at all - so it goes where somebody arriving will see it,
        // above a rendering that can run to a screenful.
        //
        // AND NEITHER HALF GATES THE OTHER, which it did: an early return on a
        // null envelope swallowed the whole airspace section, including the key
        // that answers an unset one - so the tab said nothing about the
        // airspace on exactly the machines that had not configured it.
        return Estate(state)
             + "\n"
             + (state.Envelope is { } applied
                 ? Clean(Gg.Client.VerbOutput.ToText(
                     new Gg.Client.VerbResult.EnvelopeShown(applied)), lines: true)
                 : "the envelope in force: not read - press e");
    }

    /// <summary>What the airspace field holds: the path, or nothing.</summary>
    /// <remarks>
    /// <b>Thin on purpose, and it earns its place twice.</b> Views are never
    /// the source of truth here, so the field's text comes from the model
    /// through a function rather than being assigned from whatever the widget
    /// last held. And it keeps the path under test: moving it into a TextField
    /// took it out of every pure string this pane renders, which quietly
    /// removed five assertions about a person being able to see which tree they
    /// are about to write.
    /// </remarks>
    public static string AirspacePath(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Estate?.Root ?? "";
    }

    /// <summary>
    /// What the airspace box says above the path inside it.
    /// </summary>
    /// <remarks>
    /// <b>Three states a person acts on differently, and a path cannot tell
    /// them apart.</b> Nothing set is a box to type in. A path git cannot see
    /// is one pull will overwrite without being able to warn, which is the
    /// doctor's whole argument for that check. And while the field holds the
    /// keyboard, saying so is what tells somebody their next keystroke is a
    /// character rather than a command.
    /// </remarks>
    public static string AirspaceBox(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // BEING EDITED: the way out is the one thing to say, because every
        // other key is a character now.
        if (state.Mode == UiMode.AirspacePath)
        {
            return "airspace - enter to set, ctrl-v paste, ctrl-d this directory, "
                 + "esc to leave";
        }

        // FOCUSED AND INERT, which is where the box spends its time. Focus
        // alone does not tell anybody a field can be typed into, so the key
        // that starts is named in every one of these.
        if (state.Estate?.Root is not { Length: > 0 })
        {
            return "airspace - not set, enter to edit";
        }

        return state.Estate.IsRepository
            ? "airspace - enter to edit"
            : "airspace - not a git tree; enter to edit";
    }

    /// <summary>
    /// The documents the tenant has, and what the working copy says about them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joined here and nowhere else.</b> The names and the working-copy
    /// changes arrive as two verb results and are matched by name at the
    /// render, so nothing in the console decides which document is in sync or
    /// which way a changed one moves - the diff already answered both, from the
    /// comparator the door itself runs.
    /// </para>
    /// <para>
    /// <b>Three absences, and they are three different sentences.</b> Nothing
    /// read is a key to press. A topology with no working copy is a verb to
    /// run. A working copy that would not read is a diagnosis. An empty pane
    /// would be all three at once, which is the failure the envelope's own
    /// no-envelope line already exists to avoid.
    /// </para>
    /// </remarks>
    public static string Estate(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // NO ESTATE AT ALL is a state the boot no longer produces - LocalFacts
        // fills the root before any key is pressed - but a hand-built model
        // still can, and a pane that threw or blanked on one would be a worse
        // answer than a line saying what it knows.
        if (state.Estate is not { } estate)
        {
            return "documents: not read";
        }

        var text = new StringBuilder();

        // NOT THE PATH, WHICH THE BOX AT THE BOTTOM HOLDS. Saying it twice on
        // one screen is two things to keep in agreement, and the box is the one
        // a person edits - so it is the one that has to be right.
        text.AppendLine("documents");

        if (estate.Diagnosis is { Length: > 0 } wrong)
        {
            text.AppendLine("  " + Clean(wrong));
        }

        var changed = estate.Working?.Changes ?? [];

        foreach (var name in estate.Names?.Names ?? [])
        {
            var change = changed.FirstOrDefault(
                c => string.Equals(c.Name, name.Name, StringComparison.Ordinal));

            // THE FIELD RIDES THE DIRECTION, because "something widened" sends
            // a person reading a whole document to find out what - and the diff
            // already knows.
            var said = change is null
                ? ""
                : change.Field is { Length: > 0 } field
                    ? $"  {change.Direction} ({Clean(field)})"
                    : $"  {change.Direction}";

            text.AppendLine($"  {Clean(name.Role),-10} {Clean(name.Name)}{said}");
        }

        // ONE OF THESE STOPS EVERY APPLY, so it is named rather than counted:
        // applying the rest would land part of a changeset somebody meant as a
        // whole, and a person told only that something is wrong has to go and
        // find which file.
        foreach (var path in estate.Working?.Unreadable ?? [])
        {
            text.AppendLine($"  unreadable {Clean(path)} - this stops every apply");
        }

        // AN INTENT, NOT AN ACT. There is no delete verb: retiring a name is
        // applying a terminal version of it, gated like any other change.
        foreach (var name in estate.Working?.Retiring ?? [])
        {
            text.AppendLine($"  {Clean(name)} is missing from the tree - retiring a name is "
                          + "its own gated change");
        }

        if (estate.Working is null && estate.Diagnosis is null)
        {
            text.AppendLine("  nothing is pulled here yet - gg airspace pull renders them");
        }

        return text.ToString();
    }

    /// <summary>
    /// What is holding this flight, in the control plane's own words.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Rendered, never computed.</b> The halt and each obligation's reason
    /// arrive already decided; a console that worked out for itself why an
    /// obligation attached would explain a verdict it did not produce, and the
    /// two would drift.
    /// </para>
    /// <para>
    /// <b>The section is present even when there is nothing to put in it</b>,
    /// and it says WHICH nothing. `not read for this row` and `nothing is
    /// holding this flight` are opposite facts - the second is good news - and
    /// an absent section is both of them at once.
    /// </para>
    /// </remarks>
    private static string Why(AppState state)
    {
        if (state.Attribution is not { } attribution)
        {
            return "  why           not read for this row - press g to read it";
        }

        var text = new StringBuilder();
        text.AppendLine($"  why           {Clean(attribution.Halt ?? "nothing is holding this flight")}");

        foreach (var obligation in attribution.Obligations)
        {
            text.AppendLine(
                $"    {Clean(obligation.ObligationId),-22} {Clean(obligation.Attachment),-10} "
              + Clean(obligation.Outcome ?? "no outcome recorded"));

            if (obligation.Because is { Length: > 0 } because)
            {
                // THE CONTINUATION UNDER THE COLUMN THE LABEL OPENED. A reason
                // written by a person keeps its line breaks deliberately, and
                // pasted in raw the second line lands at column zero - where the
                // conventions of this pane make it a new field. `gg show` shipped
                // exactly that defect and rendered one three-line question as
                // three gates.
                foreach (var line in because.Replace("\r\n", "\n", StringComparison.Ordinal)
                             .Split('\n'))
                {
                    text.AppendLine($"      {Clean(line)}");
                }
            }
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// Who the flight would read as, and where that secret lives.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The identity and the locator, which are the two things a person needs
    /// when a flight will not start. There is no value here to withhold - the
    /// model holds references and the control plane holds references, which is
    /// the whole product in one line of a pane.
    /// </para>
    /// <para>
    /// It says "none registered" rather than going blank. A section that
    /// vanishes reads as a flight with nothing to say about credentials, and
    /// that is precisely the case somebody is looking at this pane to diagnose.
    /// </para>
    /// </remarks>
    private static string Credentials(AppState state)
    {
        if (state.Credentials is not { Credentials.Count: > 0 } list)
        {
            return "none registered";
        }

        return string.Join(", ", list.Credentials.Select(
            c => Clean($"{c.Reference.Identity} ({c.Reference.Locator})")));
    }

    /// <summary>
    /// What the runner observed, one line each.
    /// </summary>
    /// <remarks>
    /// The first thing this console shows that no part of the control plane
    /// could have known. It arrives on the flight summary the existing verb
    /// already returns - there is no fetch route for facts, and a pane that
    /// could reach one would be a pane whose output <c>--json</c> cannot
    /// reproduce.
    /// </remarks>
    internal static string FactsOf(FlightSummary flight)
    {
        if (flight.Facts.Count == 0)
        {
            return "(none yet)";
        }

        return string.Join(", ", flight.Facts.Select(f => f switch
        {
            { Source: { } source } => Clean(
                $"{f.Kind} {source.HeadCommit[..Math.Min(8, source.HeadCommit.Length)]}"
              + (source.HeadIsFork ? $" (fork {source.ForkSlug})" : "")),
            { Change: { } change } => Clean(
                $"{f.Kind} {change.FilesChanged} file(s)"
              + (change.Resolution == ChangeResolution.Directories ? " (by directory)" : "")
              + (change.PathsWithheld > 0 ? $", {change.PathsWithheld} withheld" : "")),
            { Environment: { } environment } => Clean($"{f.Kind} {environment.Provenance}"),
            _ => Clean(f.Kind),
        }));
    }

    private static string Intent(FlightIntent intent) => intent.Kind switch
    {
        FlightIntentKinds.Uri => Clean(intent.Uri),
        FlightIntentKinds.Ticket => $"{Clean(intent.Provider)}#{Clean(intent.Id)}",
        _ => Clean(intent.Text, lines: true),
    };

    /// <summary>
    /// The digest, rendered.
    /// </summary>
    /// <remarks>
    /// Empty until the runner computes one. Saying so beats an empty pane,
    /// which reads as evidence that was checked and found to be nothing.
    /// </remarks>
    public static string Evidence(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Payload is not { } payload)
        {
            // SAID, not blank. A pane with nothing in it reads as one that failed to load.
            //
            // ON THE FLIGHT THIS MODAL IS SHOWING, which is Detailed and not
            // Selected. Those are two different cursors: Selected is the QUEUE's
            // row and is null whenever the queue is empty, while the modal titles
            // itself from the FLIGHTS list. Keyed on Selected, this said "No
            // flight selected" under a title naming a flight for every reader
            // whose queue was empty - which is what a healthy tenant looks like.
            //
            // The two sentences are about different subjects and that is why the
            // wrong one is worse than a blank: the first is about the CONSOLE,
            // the second about the FLIGHT.
            return Detailed(state) is null
                ? "No flight selected."
                : "Nothing is waiting on you for this flight.";
        }

        var text = new StringBuilder();

        foreach (var item in payload.Items)
        {
            // WHOSE WORDS. Measured is derived from facts and stated is somebody's
            // account, and the difference has to survive being rendered - an injected
            // "editing deploy/ is authorised" sitting unlabelled among measurements is
            // exactly the confusion the field exists to prevent.
            var voice = string.Equals(item.Voice, EvidenceVoices.Stated, StringComparison.Ordinal)
                ? "said"
                : "measured";

            text.AppendLine($"{item.Item} [{voice}]");

            text.AppendLine(item.Disposition switch
            {
                EvidenceDispositions.Inline => item.Inline,

                // LABELLED AS A SUMMARY. A reduction read as the whole thing is worse than
                // no reduction, because nothing tells the person they are deciding on less.
                EvidenceDispositions.Digest => $"  summary: {item.Digest}",

                // NAMED, NOT FETCHED. Enough to go and look - and the looking is theirs,
                // from their own systems, authenticated as themselves. Retrieving it here
                // would pull the content across the boundary this disposition exists for.
                EvidenceDispositions.Reference =>
                    $"  {item.Reference!.Path} @ {Short(item.Reference.Commit)} "
                  + $"({item.Reference.ByteSize} bytes, {item.Reference.MediaType}) "
                  + "- not fetched; open it yourself",

                _ => throw new InvalidOperationException(
                    $"Evidence disposition '{item.Disposition}' has no rendering. An item "
                  + "nobody can render must not be shown as one that says nothing."),
            });
        }

        // ALWAYS SAID, even when the list is empty. Absence and silence must not look
        // alike, and an empty delta is an answer rather than a section that failed.
        text.AppendLine();
        text.AppendLine($"since last decided: {payload.DeltaNote}");

        foreach (var path in payload.Delta)
        {
            text.AppendLine($"  {path}");
        }

        return text.ToString().TrimEnd();
    }

    private static string Short(string commit) => commit[..Math.Min(7, commit.Length)];

    /// <summary>
    /// The runner's normalised output.
    /// </summary>
    /// <remarks>
    /// <b>With no executor this is the strongest available version of "the
    /// console is not a viewer":</b> there is nothing to watch, and the design
    /// still has to be good. Lines are typed by kind from the start so
    /// verbosity is a data model rather than a regex applied to a screen later.
    /// </remarks>
    public static string Live(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Live.Count == 0)
        {
            // THREE SILENCES, THREE SENTENCES. An empty box cannot say why it is
            // empty, and a person who reads "nothing is writing" as "nothing has
            // been said yet" concludes the feature is broken.
            return state.Silence switch
            {
                LiveSilence.NotStarted =>
                    "No live view for this flight. Nothing is writing one: the flight has not "
                  + "been claimed, or it ran before runners wrote them.",
                LiveSilence.Stopped =>
                    "The tail stopped. Something went wrong reading this flight's live view; "
                  + "the flight is unaffected, and detaching and attaching again restarts it.",
                LiveSilence.NothingYet =>
                    "Watching. The flight is writing a live view and the agent has not said "
                  + "anything yet.",
                // AND NOTHING ELSE. It went on "This pane is off by default and
                // is meant to stay that way", which is a note to whoever built
                // it rather than an answer to whoever is looking: a person who
                // opened the pane knows they opened it, and the design's own
                // argument is not something the pane has to make to them.
                //
                // The three above keep their sentences, because each says WHICH
                // silence it is - an empty box cannot, and reading "nothing is
                // writing" as "nothing has been said yet" is how somebody
                // concludes the feature is broken.
                _ => "Nothing is running.",
            };
        }

        var text = new StringBuilder();
        foreach (var line in state.Live)
        {
            text.AppendLine($"{Marker(line.Kind)} {Clean(line.Text, lines: true)}");
        }

        if (state.Frozen && state.Held.Count > 0)
        {
            // Said out loud, because a frozen screen that is silently behind
            // looks like a run that stopped.
            text.AppendLine();
            text.AppendLine($"— frozen, {state.Held.Count} line(s) waiting —");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// What this tenant can fly against, and which one is chosen.
    /// </summary>
    /// <remarks>
    /// <b>Never asked and told none are different sentences</b>, the
    /// distinction <see cref="Browse"/> and <see cref="Live"/> both draw. An
    /// empty box says neither.
    /// </remarks>
    /// <summary>
    /// The fleet, and what this machine's runner is doing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Never "nothing has been read yet", unlike its neighbours.</b> The boot
    /// fetches the runner list for the queue's stranded-runner reason, so this
    /// pane has an answer from the first frame. An empty fleet is an empty
    /// fleet.
    /// </para>
    /// <para>
    /// <b>The line about this machine comes first and names the command.</b>
    /// Nothing registered here is not an error, it is a thing to go and do, and
    /// a pane that only said "no local runner" would leave a person guessing
    /// which command makes one.
    /// </para>
    /// </remarks>
    /// <summary>
    /// What is wrong with this machine's runner, and the key that fixes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The key, not the command.</b> It used to say <c>gg runner up</c>,
    /// which is a command a person cannot type while this console owns the
    /// terminal it is telling them to type into - the same dead end the sign-in
    /// modal exists to remove.
    /// </para>
    /// <para>
    /// <b>Empty when there is nothing to fix</b>, which is what lets the pane
    /// and the view both ask "is there a notice" rather than each deciding.
    /// </para>
    /// </remarks>
    public static string RunnerNotice(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!Rows.NoRunnerHere(state))
        {
            return "";
        }

        var mine = Rows.Runners(state).FirstOrDefault(r => r.Mine);

        var what = mine is null
            ? "No runner is registered on this machine, so nothing here can fly a flight."
            : mine.Heard == "never"
                ? "This machine has a runner registered and the control plane has never heard "
                + "from it, so it is not running."
                : $"This machine's runner has stopped heartbeating - last heard {mine.Heard}.";

        // NO KEY NAMED. There is a button under this saying what it does, one
        // arrow up from the table - and a notice naming a key nothing resolves
        // is the shape this console has a guard for.
        return what;
    }

    public static string Runners(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var rows = Rows.Runners(state);
        var text = new StringBuilder();

        // ABOVE THE TABLE, which is where somebody looks before they read a
        // list of runners that does not have theirs in it.
        if (RunnerNotice(state) is { Length: > 0 } notice)
        {
            text.AppendLine(notice);
        }
        else if (rows.FirstOrDefault(r => r.Mine) is { } mine)
        {
            text.AppendLine(mine.State == RunnerStates.Busy
                ? $"This machine's runner is working on {mine.Work}."
                : "This machine's runner is up and waiting for work.");
        }
        else if (state.Here is { Up: true } starting)
        {
            // COMING UP, which the fleet cannot say yet. It registers and then
            // heartbeats, so for a few seconds there is no row for it and the
            // only thing that knows is the console that started it.
            text.AppendLine(
                $"A runner is coming up here as process {starting.Pid} - enter to watch it.");
        }

        text.AppendLine();

        if (rows.Count == 0)
        {
            text.AppendLine(
                "This tenant has no runners at all. The control plane answered, and the "
              + "answer was an empty fleet - so every flight opened here waits for somebody "
              + "to bring a runner up.");

            return text.ToString().TrimEnd();
        }

        text.AppendLine($"{rows.Count} in the fleet");
        text.AppendLine();

        foreach (var row in rows)
        {
            // THE ARROW IS THE POINT OF THE COLUMN, as it is in the
            // repositories pane: a list where the one that is yours is not
            // marked is a list somebody has to cross-reference against a
            // command they would have to run in another terminal.
            var work = row.Work is { Length: > 0 } flight ? $"  on {flight}" : "";
            text.AppendLine($"{row.Here} {row.Runner}  {row.State}{work}{Spent(state, row.Id)}");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// What the allowance this runner spends from has left, or nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only beside the machines that REPORTED it.</b> Two machines share an
    /// allowance on purpose, so the tempting rendering is once per pane — and
    /// that is how somebody comes to believe their laptop is spending a
    /// subscription it has never touched.
    /// </para>
    /// <para>
    /// <b>The session window, because that is the one that decides whether a
    /// machine can work in the next hour.</b> The week is the one a floor will
    /// be set against, and it belongs where somebody is deciding rather than
    /// where they are glancing.
    /// </para>
    /// <para>
    /// <b>No ceiling is said in words.</b> A window rendered as 0% because
    /// nobody configured a limit reads as plenty left.
    /// </para>
    /// </remarks>
    private static string Spent(AppState state, string runnerId)
    {
        if (state.Allowances?.Allowances is not { Count: > 0 } held) { return ""; }

        var mine = held.FirstOrDefault(
            a => a.Runners.Contains(runnerId, StringComparer.Ordinal));

        if (mine is null) { return ""; }

        var session = mine.Windows.FirstOrDefault(w => w.Kind == AllowanceWindows.Session);

        if (session is null) { return $"  {Clean(mine.Name)}"; }

        return session.Limit is { } ceiling and > 0
            ? $"  {Clean(mine.Name)} {(int)Math.Floor(session.Tokens * 100.0 / ceiling)}%"
            : $"  {Clean(mine.Name)} {session.Tokens:N0} spent";
    }

    /// <summary>
    /// The question, naming the allowance it is about.
    /// </summary>
    /// <remarks>
    /// <b>Naming it is the point.</b> Two machines can share one allowance and
    /// one person can lend several, so "how much to keep?" without the
    /// subscription is a question nobody can answer safely. It also says what
    /// the share is OF, because a floor is a fraction of a ceiling somebody
    /// configured on the machines - not of anything this console knows.
    /// </remarks>
    private static string FloorChoice(AppState state)
    {
        if (AllowanceRows.SelectedName(state) is not { } allowance)
        {
            return "The machine on this row reports no allowance, so there is nothing to "
                 + "keep back. A machine reports one when its own configuration names one.";
        }

        var text = new StringBuilder();

        text.AppendLine(
            $"Keep a share of {Clean(allowance)} back, so fleet work stops before it is "
          + "gone and your own is still there.");
        text.AppendLine();
        text.AppendLine("A share of each window, against the ceiling the machines were");
        text.AppendLine("configured with. `gg allowances floor` takes any percentage, and");
        text.AppendLine("the two windows separately.");

        if (Spent(state, Rows.Selected(state)?.Id ?? "") is { Length: > 0 } spent)
        {
            text.AppendLine();
            text.AppendLine($"Now:{spent}");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// Every allowance the fleet spends from, and who is lending it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What the control plane sent, which for an administrator is the whole
    /// tenant.</b> This pane does no narrowing of its own: the answer is
    /// already scoped to what this person may see, and a second filter here
    /// would be a client deciding an authorization question.
    /// </para>
    /// <para>
    /// <b>Owners, because the question is who to ask.</b> An administrator
    /// looking at a nearly-spent allowance wants the person, not the machine -
    /// and the machines are on the runners pane already.
    /// </para>
    /// </remarks>
    private static string FleetAllowances(AppState state)
    {
        if (state.Allowances is not { } listed)
        {
            return "Nothing has been read yet. This pane asks the control plane what every "
                 + "allowance in this tenant has left.";
        }

        if (listed.Allowances.Count is 0)
        {
            return "No machine in this tenant reports an allowance. A machine reports one "
                 + "when its own configuration names one, and naming one is how somebody "
                 + "says they are lending it.";
        }

        var text = new StringBuilder();

        text.AppendLine($"{listed.Allowances.Count} in the fleet");
        text.AppendLine();

        foreach (var held in listed.Allowances)
        {
            var lenders = held.Owners.Count > 0
                ? string.Join(", ", held.Owners.Select(o => Clean(o)))
                : "nobody the control plane could name";

            text.AppendLine(
                $"{Clean(held.Name)}  lent by {lenders}  "
              + $"{held.Runners.Count} machine{(held.Runners.Count is 1 ? "" : "s")}");

            foreach (var window in held.Windows)
            {
                text.AppendLine(window.Limit is { } ceiling and > 0
                    ? $"  {Clean(window.Kind),-9} "
                      + $"{(int)Math.Floor(window.Tokens * 100.0 / ceiling)}% spent"
                    : $"  {Clean(window.Kind),-9} {window.Tokens:N0} tokens, no ceiling set");
            }

            if (held.Floor is { } floor)
            {
                text.AppendLine($"  keeps back{Kept(floor)}");
            }

            if (held.Override is { } spending)
            {
                text.AppendLine(
                    $"  {Clean(spending.By)} is spending that floor until "
                  + $"{spending.Until:HH:mm} - {Clean(spending.Reason)}");
            }

            text.AppendLine();
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>What a floor keeps, per window, in words.</summary>
    private static string Kept(Gg.Contracts.AllowanceFloor floor)
    {
        var kept = new List<string>();

        if (floor.SessionFraction is { } session)
        {
            kept.Add($" {(int)Math.Round(session * 100)}% of the session");
        }

        if (floor.WeekFraction is { } week)
        {
            kept.Add($" {(int)Math.Round(week * 100)}% of the week");
        }

        return kept.Count > 0 ? string.Join(" and", kept) : " nothing";
    }

    public static string Repositories(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Repositories is not { } listed)
        {
            return "Nothing has been read yet. This pane asks the control plane what this "
                 + "tenant can fly against.";
        }

        if (listed.Repositories.Count == 0)
        {
            return "This tenant has nothing registered to fly against. The control plane "
                 + "answered, and the answer was nothing registered - so a flight here "
                 + "resolves its repository from the envelope or from nothing at all.";
        }

        var text = new StringBuilder();
        text.AppendLine($"{listed.Repositories.Count} registered");
        text.AppendLine();

        foreach (var repository in listed.Repositories)
        {
            // THE ARROW IS THE WHOLE POINT OF THE COLUMN. A list where the
            // chosen row looks like every other row is a list that cannot tell
            // a person what the next flight will do.
            var mark = string.Equals(state.ChosenRepository, repository.Path, StringComparison.Ordinal)
                ? "\u2192"
                : " ";

            text.AppendLine($"{mark} {Clean(repository.Path),-40} {Clean(repository.Name)}");
        }

        text.AppendLine();
        text.AppendLine(state.ChosenRepository is { Length: > 0 }
            ? "Choosing the chosen one again lets the envelope decide instead."
            : "Nothing chosen: the envelope decides. Choose one to name it on every flight.");

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// The work a person can pick from, or why there is none to pick.
    /// </summary>
    /// <remarks>
    /// <b>FIVE ENDINGS, FIVE SENTENCES</b>, which is <see cref="Live"/>'s rule
    /// with more ways to end. Nothing configured, a reader that could not be
    /// asked, and a tracker with no work in it are three different things to go
    /// and do, and an empty box says none of them.
    /// </remarks>
    public static string Browse(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // NOT CONFIGURED, AND IT NAMES THE VARIABLES. The GG_POOL_ENDPOINT
        // shape: refused loudly, naming the variable, because a person looking
        // at an empty pane needs to know it is configuration and which line to
        // write. Both are named - one declares a tracker this binary reads,
        // the other a server an operator installed, and either is a valid
        // answer.
        if (state.Browse is not { } listing)
        {
            return "No tracker is configured to browse. Declare one in "
                 + $"{Gg.Local.IntentConfiguration.ServedVariable} - a provider key and the "
                 + "tracker's host - or, for a tracker this binary has no shape for, a tool "
                 + $"server in {Gg.Local.IntentConfiguration.ReadersVariable}.";
        }

        if (listing.Absence is { Length: > 0 } why)
        {
            // THE READER'S OWN WORDS. It already said why it could not answer;
            // rewording here would be a second answer to one question.
            return why;
        }

        if (listing.Items.Count == 0)
        {
            return $"'{listing.ProviderKey}' has no work to show. The tracker answered, and "
                 + "the answer was nothing - this is not a reader that failed.";
        }

        var text = new StringBuilder();
        text.AppendLine($"{listing.ProviderKey} — {listing.Items.Count} item(s)");
        text.AppendLine();

        foreach (var item in listing.Items)
        {
            // WHAT HAS ALREADY FLOWN, from what the boot already holds. A local
            // join rather than a request per row: FlightIntent carries the
            // provider and the id, so the answer is in the model already.
            var flown = AlreadyFlown(state, listing.ProviderKey, item.Id);

            text.AppendLine(
                $"{item.Id,-8} {item.State,-12} {Clean(item.Title)}{flown}");
        }

        // AN ABSENCE THIS LIST CANNOT SEE HAS TO BE STATED. `?intent=` takes
        // provider#id only, so a flight opened from a pasted url can never
        // match a row - and showing nothing would report an absence rather
        // than an inability to look. Printed only when such a flight exists,
        // because a footnote about a case that does not apply teaches people
        // to stop reading footnotes.
        if (state.Flights is { } all
            && all.Flights.Any(flight => flight.Intent.Provider is null or ""
                                      || flight.Intent.Id is null or ""))
        {
            text.AppendLine();
            text.AppendLine(
                "Some flights were opened from a pasted url and name no work item, so this "
              + "list cannot tell you about them.");
        }

        if (listing.NextCursor is { Length: > 0 })
        {
            // A person who cannot tell a full page from the whole backlog stops
            // looking at the first screenful.
            text.AppendLine();
            text.AppendLine("— more to come —");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// The question asked before a second flight on one work item.
    /// </summary>
    /// <remarks>
    /// <b>It names the item.</b> A modal saying "this already has a flight"
    /// while a list scrolled underneath is a modal about nothing in particular,
    /// and the answer would be given about whatever is on screen.
    /// </remarks>
    /// <summary>What grounding this flight would mean, before it happens.</summary>
    /// <remarks>
    /// <b>It names the flight, because the key is pressed over a modal and the
    /// modal is the only thing on the screen.</b> A confirmation that said
    /// "ground it?" would be a question about whichever flight the person
    /// believes they are looking at.
    /// </remarks>
    private static string ConfirmGround(AppState state) =>
        Detailed(state) is not { } flight
            ? ""
            : $"Ground {flight.FlightNumber}, {flight.Name}?\n\n"
            + "It stops the attempt rather than withdrawing the question, so the flight is "
            + "still a record of having been tried. You will be asked to write why, and a "
            + "flight is not grounded until you do.";

    /// <summary>What flying it again would mean, before the editor opens.</summary>
    /// <remarks>
    /// <b>It says the editor comes next, because that is where the decision
    /// really lands.</b> Nothing is opened by answering this: what follows is
    /// the same editor `n` opens, on this flight's own words, and abandoning it
    /// there opens nothing either.
    /// </remarks>
    private static string ConfirmFlyAgain(AppState state) =>
        Detailed(state) is not { } flight
            ? ""
            : $"Open a new flight on {flight.FlightNumber}'s intent?\n\n"
            + "The editor opens on what it said, so you can change it first. Nothing is "
            + "opened until you save and quit, and a flight opened by accident is a record "
            + "somebody has to explain and a number that is now taken.";

    private static string ConfirmFlight(AppState state) =>
        state.PendingFlight is not { } pending
            ? ""
            : $"{pending.Why}\n\n"
            + $"Open a second flight for {pending.Provider}#{pending.Id}?\n"
            + "Two flights on one work item is allowed, and is usually a mistake.";

    /// <summary>
    /// The flights already opened for one work item, oldest first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE PROVIDER IS HALF THE KEY.</b> Two trackers can both hold an item
    /// 18398, and matching on the number alone would attribute somebody else's
    /// flight to this row - the same reason the provider is on the screen at
    /// all.
    /// </para>
    /// <para>
    /// <b>Oldest first</b>, the correlation surface's own ordering, so a
    /// classify flight and what it opened read as one thread rather than in
    /// whatever order the control plane answered.
    /// </para>
    /// <para>
    /// <b>Empty where nothing has been fetched.</b> A null flight list means
    /// nobody has looked, which is not the same as nothing having flown, and
    /// this returns the same blank for both - the caveat below the list is
    /// where the difference is stated.
    /// </para>
    /// </remarks>
    private static string AlreadyFlown(AppState state, string providerKey, string id)
    {
        if (state.Flights is not { } all)
        {
            return "";
        }

        var flown = all.Flights
            .Where(flight => string.Equals(flight.Intent.Provider, providerKey, StringComparison.Ordinal)
                          && string.Equals(flight.Intent.Id, id, StringComparison.Ordinal))
            .OrderBy(flight => flight.CreatedAt)
            .Select(flight => flight.FlightNumber)
            .ToList();

        return flown.Count == 0 ? "" : "  [" + string.Join(", ", flown) + "]";
    }

    /// <summary>A one-character gutter, so kind survives into the rendering.</summary>
    public static string Marker(StreamLineKind kind) => kind switch
    {
        StreamLineKind.Text => " ",
        StreamLineKind.Tool => "⚙",
        StreamLineKind.Raw => "|",
        StreamLineKind.Meta => "·",
        StreamLineKind.Setup => "+",
        _ => throw new InvalidOperationException(
            $"Stream line kind '{kind}' has no marker. Output nobody can classify must not be shown as ordinary text."),
    };

    /// <summary>Whatever modal is open.</summary>
    /// <summary>
    /// The line that says what the last key press did, or nothing at all.
    /// </summary>
    /// <remarks>
    /// <b>Its own line, above the hints.</b> Sharing one would make a long outcome
    /// truncate the list of keys or the other way round, and both are things a person
    /// is reading at the moment they need them.
    /// </remarks>
    public static string Activity(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // A CHOSEN REPOSITORY IS ANNOUNCED HERE, not only inside its pane.
        // It changes what every flight this console opens will name, and
        // invisible state that changes what a write does is the worst kind -
        // somebody who chose one an hour ago and forgot must not open a flight
        // against it without being told.
        var chosen = state.ChosenRepository is { Length: > 0 } repository
            ? $"flying against {Clean(repository)}"
            : "";

        var said = state.LastAction is { Length: > 0 } action ? Clean(action) : "";

        return (said, chosen) switch
        {
            ("", "") => "",
            ("", _) => chosen,
            (_, "") => said,
            _ => said + " · " + chosen,
        };
    }

    /// <summary>
    /// What the runner on this machine is doing, and what it has said.
    /// </summary>
    /// <remarks>
    /// <b>The state first, the path second, the log after.</b> Somebody opens
    /// this while a runner is coming up, so the top line has to answer "is it
    /// working" without being read past - and the path is here because a log
    /// this modal shows the tail of is a log somebody will want the whole of.
    /// </remarks>
    /// <summary>
    /// The runner modal, as one string.
    /// </summary>
    /// <remarks>
    /// <b>Composed from the same producers the widgets bind.</b> This was a
    /// renderer of its own, and it drifted the moment the modal grew fields:
    /// the widgets showed why a runner was parked and this said nothing about
    /// it. One author, or two screens that disagree.
    /// </remarks>
    private static string Runner(AppState state) => RunnerDetails.Linear(state);

    /// <summary>
    /// Why a flight was not flown by hand, in full.
    /// </summary>
    /// <remarks>
    /// <b>Wrapped, which is the whole reason this is a modal.</b> The same words
    /// on the activity line lost their last clause off the right edge, and the
    /// clause it lost was the remedy.
    /// </remarks>
    private static string HandFlight(AppState state)
    {
        if (state.HandFlightProblem is not { Length: > 0 } problem)
        {
            return "";
        }

        var text = new StringBuilder();

        // ONE SENTENCE PER LINE. The refusal is a requirement followed by a
        // remedy, and a paragraph of them is read as one thing to give up on.
        foreach (var sentence in problem.Split(". ", StringSplitOptions.RemoveEmptyEntries))
        {
            text.AppendLine(sentence.TrimEnd('.') + ".");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// What a person reads across the top of a modal.
    /// </summary>
    /// <remarks>
    /// <b>Written, not derived.</b> It was <c>Mode.ToString()</c>, so somebody
    /// opening a refusal read <c>HandFlight</c> - a name that exists for the
    /// compiler, on a screen, looking enough like a word to survive review.
    /// Splitting the camel case would have been the same name with a space in
    /// it; these say what the thing is.
    /// </remarks>
    /// <summary>
    /// The same, for a modal that can name the thing it is about.
    /// </summary>
    /// <remarks>
    /// <b>"This flight" is true of every flight, which is what made it worth
    /// nothing across the top of one.</b> The mode's own title is right for a
    /// question - a refusal is a refusal - and wrong for a document about one
    /// subject, so the subject gets the heading when there is one. The overload
    /// above stays: it is what a mode with nothing to name still says, and it
    /// is what this falls back to.
    /// </remarks>
    public static string ModalTitle(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // A MODAL ABOUT ONE SUBJECT GETS THE SUBJECT. The rest keep the title
        // written for them, because a refusal is a refusal whichever one it is.
        return state.Mode switch
        {
            UiMode.FlightDetail => FlightDetails.Title(state),
            UiMode.Runner => RunnerDetails.Title(state),
            _ => ModalTitle(state.Mode),
        };
    }

    /// <summary>How a launch path composed its intent, said out loud.</summary>
    /// <remarks>
    /// <b>S33.4-04: a path that cannot offer both says which it is using.</b>
    /// Two of the three doors ask; the third cannot, because a work item IS the
    /// intent and there is no text for either composer to write. A path that
    /// quietly used one while its neighbours asked would teach somebody that the
    /// question is optional — so the one that does not ask gives the reason.
    /// </remarks>
    public static string ComposedBy(ComposingFor what) => what switch
    {
        ComposingFor.WorkItem =>
            "The work item is the intent, so nothing was composed: the flight carries the "
          + "item's provider and id, which is what keeps it linked back to the item.",
        _ => "",
    };

    /// <summary>What the two ways of composing mean.</summary>
    /// <remarks>
    /// <para>
    /// <b>It had none, and the box was empty.</b> The modal drew a title and
    /// nothing under it, so the answers lived only on the hint line at the foot
    /// of the screen - the furthest point from where somebody who has just
    /// pressed <c>n</c> is looking. Nothing caught it because the test asserts
    /// the HINTS name both ways, which they do.
    /// </para>
    /// <para>
    /// <b>It does not name the keys, because the buttons under it do.</b> An
    /// earlier version listed <c>w</c> and <c>m</c> down the left, which was
    /// right when the hint line was the only place they appeared and is a third
    /// copy now. What is left is what a button cannot say: what each choice
    /// actually does.
    /// </para>
    /// </remarks>
    private static string ComposeChoice() =>
        "Write it yourself and your editor opens on a blank page.\n"
      + "Whatever you save is what this flight will do.\n"
      + "\n"
      + "Or work it out with an agent. It reads what it needs,\n"
      + "talks it through with you, and hands the finished thing\n"
      + "back when you are both happy with it.\n"
      + "\n"
      + "While an agent is running, gg keeps a row at the top of\n"
      + "the screen. Press ctrl-g there to see the rules it is\n"
      + "working under.";

    /// <summary>What is actually being decided.</summary>
    /// <remarks>
    /// <para>
    /// <b>The worse of the two empty boxes.</b> This asked a person to approve
    /// or reject and said nothing at all about what - a title reading "Waiting
    /// on you" over blank space, with <c>a</c> and <c>r</c> on the hint line at
    /// the foot of the screen. Approving is attributed to whoever does it,
    /// which is the last place a console should be terse.
    /// </para>
    /// <para>
    /// <b>Rendered by the function <c>gg gates</c> already uses, not a second
    /// one.</b> A first version wrote the flight, obligation and approver out by
    /// hand - a SECOND rendering of a gate, missing <c>because</c>, which is the
    /// Engine's own words for why the obligation attached and the whole decision
    /// when the condition is "the loop asked". It also has a layout rule of its
    /// own, established by <c>GatePresentationTests</c>: prose an agent wrote
    /// keeps its line breaks, and continuations are indented. None of that is
    /// inherited by a copy, and the copy is the one nobody notices drifting.
    /// </para>
    /// </remarks>
    private static string GateDecision(AppState state)
    {
        if (state.SelectedGate is not { } gate)
        {
            // SAID, NOT BLANK. A gate answered somewhere else, or a list
            // re-read underneath a person, is a thing to explain - and an empty
            // box for it is indistinguishable from the defect this arm exists
            // because of.
            return "There is no decision waiting on this row any more.\n"
                 + "Somebody may have answered it already.";
        }

        return Clean(
            Gg.Client.VerbOutput.ToText(
                new Gg.Client.VerbResult.Gates(new Gg.Contracts.GateList { Gates = [gate] })),
            lines: true);
    }

    public static string ModalTitle(UiMode mode) => mode switch
    {
        UiMode.Help => "Keys",
        UiMode.FlightActions => "What can be done",
        UiMode.FlightDetail => "This flight",
        UiMode.HandFlight => "Nothing was created",
        UiMode.Runner => "No runner",
        UiMode.ConfirmFlight => "This has flown before",
        UiMode.ConfirmGround => "Ground this flight?",
        UiMode.ConfirmApply => "Apply the working copy?",
        UiMode.ConfirmFlyAgain => "Fly this again?",
        UiMode.GateDecision => "Waiting on you",
        UiMode.SignIn => "Nobody is signed in",
        UiMode.FloorChoice => "How much to keep back",
        UiMode.ComposeChoice => "How do you want to write this flight?",
        _ => "",
    };

    /// <summary>
    /// The changeset an apply would submit, in the order it will take.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read from the diff, in the order the diff already put them in.</b>
    /// Apply lands tightenings before widenings so no intermediate state is
    /// looser than either endpoint (ADR-0016 § 7), and the diff verb sorts its
    /// own answer that way for exactly this reason - <i>the order a person
    /// reads is the order that will happen</i>. Sorting again here would be a
    /// second opinion about a sequence.
    /// </para>
    /// <para>
    /// <b>Widenings are marked, because they do not land.</b> One opens a
    /// flight and waits for whoever the widened document names, so somebody who
    /// expected a version would go looking for one that was never minted.
    /// </para>
    /// </remarks>
    private static string ConfirmApply(AppState state)
    {
        var changes = state.Estate?.Working?.Changes ?? [];
        var retiring = state.Estate?.Working?.Retiring ?? [];

        if (changes.Count == 0 && retiring.Count == 0)
        {
            return "Nothing to apply: the working copy matches the airspace.";
        }

        var text = new StringBuilder();

        text.AppendLine("One flight per document, in this order:");
        text.AppendLine();

        foreach (var change in changes)
        {
            var waits = string.Equals(
                change.Direction, Gg.Client.Changeset.Widening, StringComparison.Ordinal)
                ? change.Field is { Length: > 0 } field
                    ? $" - widens {Clean(field)}, so it opens a flight and waits at a gate"
                    : " - a widening, so it opens a flight and waits at a gate"
                : " - a tightening, so it lands";

            text.AppendLine($"  {Clean(change.Name)}{waits}");
        }

        foreach (var name in retiring)
        {
            text.AppendLine($"  {Clean(name)} is missing from the tree - reported, never "
                          + "performed: retiring a name is its own gated change");
        }

        return text.ToString();
    }

    /// <summary>
    /// Whether this modal shows something a person reads down.
    /// </summary>
    /// <remarks>
    /// <b>A property of what the mode shows, and it was a literal in the
    /// view.</b> A modal added later inherited the small size and nothing asked
    /// whether that was right - the runner's opened with a log in it and seven
    /// visible lines. The three below render documents; the rest are a few lines
    /// and two keys, and a box the size of the screen around one of those reads
    /// as something having gone wrong.
    /// </remarks>
    public static bool ModalIsADocument(UiMode mode) =>
        mode is UiMode.Help or UiMode.FlightDetail or UiMode.Runner;

    /// <summary>How wide a question's words may run.</summary>
    /// <remarks>
    /// <b>The box is sized to its longest line</b>, so this is what stops a
    /// body being a request for a dialog wider than the terminal. Sixty-four
    /// plus the border and the padding is a box inside eighty columns, which is
    /// the narrowest screen anybody here has - and comfortably narrower than
    /// the button row, which sets the floor anyway.
    /// </remarks>
    public const int QuestionColumns = 64;

    /// <summary>
    /// The same words, broken between them rather than at the box's edge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Here rather than in the view, so it can be read without a
    /// terminal.</b> Every other layout decision in this console is arithmetic
    /// a test can check; a wrap done by a widget is one only a person looking
    /// at a screen would ever know had gone wrong.
    /// </para>
    /// <para>
    /// <b>Blank lines survive.</b> A blank line is a paragraph break somebody
    /// wrote on purpose - the question, then what answering it means - and a
    /// wrapper that ate it would run the two together.
    /// </para>
    /// <para>
    /// <b>A word longer than the line is left whole.</b> A url, a flight id, a
    /// path: breaking one makes it uncopyable, which is worse than a box one
    /// line too wide. It is also the case a naive wrapper loops forever on.
    /// </para>
    /// </remarks>
    public static string Wrapped(string text, int columns)
    {
        ArgumentNullException.ThrowIfNull(text);

        var wrapped = new List<string>();

        foreach (var paragraph in text.ReplaceLineEndings("\n").Split('\n'))
        {
            if (paragraph.Length <= columns)
            {
                wrapped.Add(paragraph);
                continue;
            }

            var line = new System.Text.StringBuilder();

            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > columns)
                {
                    wrapped.Add(line.ToString());
                    line.Clear();
                }

                if (line.Length > 0)
                {
                    line.Append(' ');
                }

                line.Append(word);
            }

            if (line.Length > 0)
            {
                wrapped.Add(line.ToString());
            }
        }

        return string.Join("\n", wrapped);
    }

    public static string Modal(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // NOT WRAPPED HERE. This is the CONTENT and the wrap is presentation:
        // ConsoleScreen applies it before sizing the box, because the box is
        // sized to its longest line. Wrapping here instead broke a test
        // asserting a sentence the wrap had split - and sixteen others were
        // passing only because their phrases happened not to straddle a break,
        // which is a whole file of assertions resting on where words fall.
        return state.Mode switch
        {
            UiMode.Help => Help(state),
            UiMode.FlightDetail => FlightDetail(state),
            UiMode.HandFlight => HandFlight(state),
            UiMode.Runner => Runner(state),
            UiMode.FlightActions => Actions(state),
            UiMode.ConfirmFlight => ConfirmFlight(state),
            UiMode.ConfirmGround => ConfirmGround(state),
            UiMode.ConfirmApply => ConfirmApply(state),
            UiMode.ConfirmFlyAgain => ConfirmFlyAgain(state),
            UiMode.SignIn => SignIn(state),
            UiMode.GateDecision => GateDecision(state),
            UiMode.FloorChoice => FloorChoice(state),
            UiMode.ComposeChoice => ComposeChoice(),
            _ => "",
        };
    }

    /// <summary>
    /// Why the console behind this is empty, and the two steps out of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It names no command.</b> What this replaced was <i>"Not signed in.
    /// Run gg login."</i> - true, and impossible to act on, because gg had the
    /// terminal it was telling somebody to type into. Everything here is done
    /// with the keys on the hint line.
    /// </para>
    /// <para>
    /// <b>Two lines, because a modal is read standing up.</b> The queue behind
    /// it says "nothing needs you" whether nothing does or nobody could be
    /// asked, so the cause is worth one sentence - and the rest of what could
    /// be said here is either on the hint line already or is not what somebody
    /// blocked from their own console wants to read.
    /// </para>
    /// <para>
    /// <b>The address and the code get a line each, indented.</b> They are
    /// transcribed by a human being into another device, and a code inside a
    /// sentence is a code somebody reads the punctuation of.
    /// </para>
    /// </remarks>
    private static string SignIn(AppState state)
    {
        var text = new StringBuilder();

        if (state.SignIn is { } pending)
        {
            text.AppendLine($"  Open:  {Clean(pending.VerificationUri)}");
            text.AppendLine($"  Code:  {Clean(pending.UserCode)}");
            text.AppendLine();
            // LABELLED, AND CONVERTED SO THE LABEL IS TRUE. Every other time
            // this product puts in front of a person is written UTC and said to
            // be - an unlabelled local time is ambiguous the moment the text is
            // read anywhere but the machine that drew it, and this record ends
            // up in a state dump and a diagnostics bundle. ToUniversalTime
            // rather than trusting the offset that arrived, so the three
            // letters cannot become a lie if the control plane ever sends one.
            //
            // AND IT SAYS WHO IS WAITING. "Then come back" was an instruction
            // when coming back meant pressing a key; now that the console is
            // watching, the same words would have somebody sitting in front of
            // a modal waiting to be told to do something that is already
            // happening.
            text.AppendLine(
                $"Approve it there and this console will carry on by itself. "
                + $"Expires {pending.ExpiresAt.ToUniversalTime():HH:mm} UTC.");

            return text.ToString().TrimEnd();
        }

        text.AppendLine("You are not signed in, so there is nothing to show.");
        text.AppendLine();
        text.AppendLine("Signing in gives you a code to type into a browser.");

        // WHAT THE LAST TRY CAME TO, and only when there was one. Expired,
        // declined and pressed-a-moment-early all land back on this screen, and
        // a person who reads the same offer as before concludes the key did
        // nothing.
        if (!string.IsNullOrWhiteSpace(state.LastSignIn))
        {
            text.AppendLine();

            // Cleaned like every other line here: this one can carry a display
            // name the control plane chose, in "Signed in as …".
            text.AppendLine(Clean(state.LastSignIn));
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// Help, generated from the bindings.
    /// </summary>
    /// <remarks>
    /// Written by hand it would be a third list of keys, after the keymap and
    /// the hint line, and the one people read when they are already confused.
    /// </remarks>
    private static string Help(AppState state) =>
        Tabs(state.HelpPage) + "\n\n"
        + (state.HelpPage == HelpPage.Environment ? HelpEnvironment(state) : HelpKeys(state));

    /// <summary>
    /// The tab bar, marking the page a person is on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A SECOND PAGE NOBODY CAN SEE IS A SECOND PAGE NOBODY OPENS.</b> This
    /// replaced a line of prose telling a person which key to press, which is
    /// strictly worse: it asks them to read an instruction and remember it,
    /// where a bar shows both pages at once and says which one they are on.
    /// </para>
    /// <para>
    /// <b>Text, like every other pane here.</b> A Terminal.Gui TabView would put
    /// "which page is showing" inside a widget, where no test can assert it and
    /// the state dump cannot reproduce it. <c>HelpPage</c> is in the model and
    /// this renders it.
    /// </para>
    /// </remarks>
    private static string Tabs(HelpPage page)
    {
        var keys = page == HelpPage.Keys ? "[ Keys ]" : "  Keys  ";
        var environment = page == HelpPage.Environment ? "[ Environment ]" : "  Environment  ";

        // The key is named on the bar rather than in a sentence below it: tabs
        // a person cannot work out how to change are decoration.
        return $"  {keys}  {environment}      tab";
    }

    /// <summary>
    /// What this machine is configured to do, and what decides it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE PAGE THAT ANSWERS "why did that key do nothing".</b> `n` hands the
    /// terminal to <c>$EDITOR</c>; pointed at an editor that forks and returns,
    /// it comes back with an empty file and the console says no intent was
    /// written. That is a correct sentence about a confusing outcome, and the
    /// value that explains it was one keystroke away and unreachable.
    /// </para>
    /// <para>
    /// <b>Unset is a row, not an omission.</b> The variable worth reading is
    /// usually the one that is not set.
    /// </para>
    /// </remarks>
    private static string HelpEnvironment(AppState state)
    {
        var text = new StringBuilder();

        // WHAT SOMEBODY ELSE PROPOSES, above the rows it would change. Said
        // even when the answer is none: a page that mentions offers only while
        // one is waiting cannot be used to check that none is, which is the
        // question somebody opens this page to answer.
        text.AppendLine("  " + Offered(state));
        text.AppendLine();

        if (state.Settings.Count == 0)
        {
            // NOT "nothing is set". The composition root builds this list and a
            // test host does not, so an empty one is a console that was never
            // told - a different fact, and the only honest thing to print.
            text.AppendLine("  This console was not told which variables it reads.");
            return text.ToString().TrimEnd();
        }

        foreach (var setting in state.Settings)
        {
            text.AppendLine($"  {setting.Name}");
            text.AppendLine(setting.Value is { Length: > 0 } value
                ? $"      = {Clean(value)}"
                : "      not set");
            text.AppendLine($"      {Clean(setting.Why)}");

            // A BLANK LINE BETWEEN THEM. Ten variables at three lines each is a
            // wall, and a wall is read as one thing rather than ten.
            text.AppendLine();
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>One line about what this control plane offers this machine.</summary>
    /// <remarks>
    /// <b>Four states and four sentences.</b> Nothing offered; an offer already
    /// in force; one that repoints something, which is the case the whole tier
    /// exists for; and one that does not. Only the last two end in a key,
    /// because asking somebody to take a document they already have is asking
    /// them to decide twice.
    /// </remarks>
    private static string Offered(AppState state)
    {
        if (state.Offered is not { } offered)
        {
            return "Nothing is offered to this machine by its control plane.";
        }

        var what = offered.Settings == 1 ? "1 setting" : $"{offered.Settings} settings";

        if (offered.AlreadyAccepted)
        {
            return $"Offer {Clean(offered.Version)} ({what}) is already in force here.";
        }

        // NAMES THE REASON, not just the requirement. "It repoints something"
        // is why a person is being asked at all, and it is the sentence the
        // offerable set was argued into existence on.
        var why = offered.NeedsAPerson
            ? " It repoints something, so only a person can take it."
            : "";

        return $"Offer {Clean(offered.Version)} ({what}) is waiting.{why} Press `o' to take it.";
    }

    /// <summary>
    /// Every key, grouped by what owns the keyboard when it works.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>FROM THE CATALOGUE RATHER THAN FROM ONE CONTEXT.</b> This asked the
    /// keymap for the bindings of the context the console happened to be in, so
    /// the page was missing <c>f</c> whenever neither the live pane nor browse
    /// was showing - which is how a console starts - and it never held the gate
    /// modal's <c>a</c> and <c>r</c> at all. The hint line is right to show
    /// only what is live. Somebody reading this page is here because they do
    /// not know a key.
    /// </para>
    /// <para>
    /// <b>Grouped by mode, because a key means what the mode says it means.</b>
    /// <c>a</c> is actions in Normal and approve in a gate decision, and a flat
    /// list of every key would put those two rows next to each other reading as
    /// a contradiction. The Normal group carries no heading: it is what the
    /// console is doing when nobody opened anything.
    /// </para>
    /// <para>
    /// <b>Every key, including the ones that are not on the hint line.</b> The
    /// two are different claims: a key leaves the LINE because it is advertised
    /// somewhere else - on its own tab, or here - and leaves THIS PAGE only
    /// when the thing it does has another key entirely. That is j and k, whose
    /// work the arrows do.
    /// </para>
    /// </remarks>
    private static string HelpKeys(AppState state)
    {
        var text = new StringBuilder();

        foreach (var group in Keymap.Catalogue()
                     .Where(entry => !entry.Binding.Untaught)
                     .GroupBy(entry => entry.Mode))
        {
            if (group.Key != UiMode.Normal)
            {
                text.AppendLine();
                text.AppendLine($"  {ModeHeading(group.Key)}");
            }

            foreach (var entry in group)
            {
                var when = entry.Binding.When is { Length: > 0 } condition
                    ? $"   ({condition})"
                    : "";
                text.AppendLine($"  {entry.Binding.Key.Name,-8}{entry.Binding.Description}{when}");
            }
        }

        text.AppendLine();
        text.AppendLine($"  {Keymap.Interrupt.Name,-8}quit from anywhere");
        text.AppendLine();
        text.AppendLine($"  queue order: {QueueSort.Default.Name}");

        return text.ToString().TrimEnd();
    }

    /// <summary>What to call a mode on the help page.</summary>
    /// <remarks>
    /// The enum name would do for three of the five and not for
    /// <c>ConfirmFlight</c>, which is a question rather than a place. Written
    /// out as the sentence a person would use: they are reading this because
    /// something is on the screen and they do not know what it wants.
    /// </remarks>
    private static string ModeHeading(UiMode mode) => mode switch
    {
        UiMode.Help => "While this page is open",
        UiMode.FlightActions => "While the actions list is open",
        UiMode.ConfirmFlight => "When asked whether to open a second flight",
        UiMode.ConfirmGround => "When asked whether to ground a flight",
        UiMode.ConfirmFlyAgain => "When asked whether to fly one again",
        UiMode.GateDecision => "While answering a gate",
        _ => "",
    };

    /// <summary>
    /// What can be done to the selected flight.
    /// </summary>
    /// <remarks>
    /// Nothing yet, and it says so. Taking a flight is slice two; this step
    /// builds its precondition and nothing more, and an action that appeared
    /// here and did nothing would be Article XI's failure mode with a border
    /// around it.
    /// </remarks>
    private static string Actions(AppState state) =>
        state.Selected is not { } row
            ? "  No flight selected."
            : $"  {Clean(row.FlightNumber)}  {Clean(row.Name)}\n\n"
            // WHAT THIS CONSOLE CAN DO, and why the one thing it cannot is
            // absent. `t` is offered only when a tree is held and this console
            // never holds one, so the key is correctly missing - but nothing
            // said why, and an absent key with no explanation reads as a bug.
            //
            // The previous text promised takeover "arrives in slice two". It
            // arrived. A sentence that was true once and wrong ever since is
            // the failure this pane exists to avoid.
            + "  d  decide a gate on this flight\n"
            + "  v  the evidence behind it\n\n"
            + "  Taking this flight over is not offered here. It needs the flight's\n"
            + "  working tree, and this console never holds one — the branch is what\n"
            + "  is authoritative. It can be done on the machine that ran the flight.";

    /// <summary>
    /// Last line of defence before a terminal.
    /// </summary>
    /// <remarks>
    /// Text is stored clean, so in a healthy system this removes nothing. It is
    /// here because this is the last code between a control plane and a screen
    /// that acts on escape sequences.
    /// </remarks>
    private static string Clean(string? value, bool lines = false) => ControlText.Strip(value, lines);
}
