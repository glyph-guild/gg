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
            TabId.Evidence => Evidence(state),
            TabId.Live => Live(state),
            TabId.Browse => Browse(state),
            TabId.Repositories => Repositories(state),
            TabId.Checklist => Checklist(state),
            TabId.Envelope => Envelope(state),
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

        if (state.Flight is not { } flight)
        {
            return state.Selected is null ? "" : "loading…";
        }

        var text = new StringBuilder();
        text.AppendLine($"  {Clean(flight.FlightNumber)}  {Clean(flight.Name)}");
        text.AppendLine($"  id            {Clean(flight.FlightId)}");
        text.AppendLine($"  opened        {flight.CreatedAt:u}");
        text.AppendLine($"  intent        {Intent(flight.Intent)}");
        text.AppendLine($"  constitution  {Clean(flight.ConstitutionVersion)}");
        text.AppendLine($"  envelope      {Clean(flight.EnvelopeVersion)}");
        text.AppendLine($"  vocabulary    {Clean(flight.FactVocabularyVersion)}");
        text.AppendLine();
        text.AppendLine("  pinned refs   (none until the flight is materialized)");
        text.AppendLine($"  credential    {Credentials(state)}");
        text.AppendLine($"  facts         {Facts(flight)}");
        text.AppendLine();
        text.AppendLine(Why(state));

        if (state.FlightLog is { Entries.Count: > 0 } log)
        {
            text.AppendLine();
            foreach (var entry in log.Entries)
            {
                text.AppendLine($"  {entry.At:u}  {Clean(entry.Kind)}");
            }
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// What must hold before the selected flight can start.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Each item with its satisfier and its disposition</b>, which is what
    /// makes the answer a job rather than a mood: an unmet requirement whose
    /// satisfier is a label nobody advertises is a different task from one
    /// waiting on an approver, and a list of requirement names cannot tell them
    /// apart.
    /// </para>
    /// <para>
    /// <b>An unread checklist says so, and it matters more here than anywhere.</b>
    /// An empty list reads as <i>nothing is stopping this flight</i>, which is
    /// the opposite of <i>nobody asked</i> - and one of those is good news.
    /// </para>
    /// </remarks>
    public static string Checklist(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var text = new StringBuilder();

        // THE FLEET FOLLOWS WHATEVER THE CHECKLIST SAID, INCLUDING NOTHING.
        // Showing it only when the checklist read succeeded would hide the
        // fleet exactly when the checklist failed - and the fleet is what a
        // person reads to find out why.
        if (state.Checklist is not { } checklist)
        {
            text.AppendLine(state.Selected is null
                ? "No flight selected."
                : "not read for this row - press p to read it");
        }
        else
        {
            text.AppendLine($"  envelope      {Clean(checklist.EnvelopeVersion)}");

            if (checklist.Repository is { Length: > 0 } repository)
            {
                text.AppendLine($"  repository    {Clean(repository)}");
            }

            text.AppendLine($"  labels        {Labels(checklist.RequiredLabels)}");
            text.AppendLine();

            if (checklist.Items.Count == 0)
            {
                text.AppendLine("  nothing is required before this flight can start.");
            }

            foreach (var item in checklist.Items)
            {
                text.AppendLine(
                    $"  {Clean(item.Disposition),-8} {Clean(item.Requirement)}");
                text.AppendLine(
                    $"           satisfied by {Clean(item.Satisfier)}, checked by "
                  + Clean(item.Verification));
            }
        }

        text.AppendLine();
        text.Append(FleetText(state));

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// What the fleet advertises, each label beside its disposition.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Under the checklist because that is where the question is asked.</b>
    /// An item reading <c>unmet  environment=docker</c> is answered by what the
    /// runners advertise, and a person who has to change panes to find out is
    /// comparing two screens from memory.
    /// </para>
    /// <para>
    /// <b>The disposition is never separated from the name.</b> That is
    /// <c>AdvertisedLabel</c>'s own rule and the reason the type exists: a
    /// stated claim read as a measurement is what a bare name invites.
    /// </para>
    /// <para>
    /// <b>And a fleet that was not read is not an empty fleet.</b> "No runners
    /// are registered" is a claim about the estate with an action attached;
    /// saying it because nothing was read sends somebody to build a machine
    /// they already have.
    /// </para>
    /// </remarks>
    private static string FleetText(AppState state)
    {
        if (state.Runners is not { } fleet)
        {
            return "  fleet         not read";
        }

        if (fleet.Runners.Count == 0)
        {
            return "  fleet         no runners are registered. Run gg runner up on a machine "
                 + "that should take work.";
        }

        var text = new StringBuilder();
        text.AppendLine("  fleet");

        foreach (var runner in fleet.Runners)
        {
            text.AppendLine($"    {Clean(runner.State),-8} {Clean(runner.Label)}");

            if (runner.Labels.Count == 0)
            {
                // A fact somebody diagnosing a waiting flight needs, not an
                // absence to hide.
                text.AppendLine("             (advertises nothing)");
                continue;
            }

            foreach (var label in runner.Labels)
            {
                text.AppendLine(
                    $"             {Clean(label.Name),-34} {Clean(label.Disposition)}");
            }
        }

        return text.ToString().TrimEnd();
    }

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

        if (state.Envelope is not { } applied)
        {
            return "not read - press e to read the envelope in force";
        }

        return Clean(Gg.Client.VerbOutput.ToText(
            new Gg.Client.VerbResult.EnvelopeShown(applied)), lines: true);
    }

    /// <summary>
    /// What the fleet has to advertise, said out loud.
    /// </summary>
    /// <remarks>
    /// "none" rather than a blank: a checklist requiring no labels and one whose
    /// labels failed to render look identical otherwise, and the first is the
    /// ordinary case.
    /// </remarks>
    private static string Labels(IReadOnlyList<string> labels) =>
        labels.Count == 0 ? "none" : string.Join(", ", labels.Select(l => Clean(l)));

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
    private static string Facts(FlightSummary flight)
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
            // ON THE SELECTION, NOT ON THE FLIGHT. The question this sentence
            // answers is "has anybody selected anything", and Flight answers
            // "did that row's detail load" - two questions that were the same
            // only while nothing assigned Flight at all. They are different
            // now: the reducer leaves Flight null for a row it loaded nothing
            // for, deliberately, so keying on it would tell somebody with a row
            // highlighted that they had selected nothing.
            return state.Selected is null
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
                _ =>
                    "Nothing is running. This pane is off by default and is meant to stay that "
                  + "way.",
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

    public static string Modal(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Mode switch
        {
            UiMode.Help => Help(state),
            UiMode.FlightActions => Actions(state),
            UiMode.ConfirmFlight => ConfirmFlight(state),
            UiMode.SignIn => SignIn(state),
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
            text.AppendLine(
                $"Approve it there, then come back. Expires {pending.ExpiresAt.ToUniversalTime():HH:mm} UTC.");

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
