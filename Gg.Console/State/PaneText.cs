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
                state.Diagnosis is { Length: > 0 } ? "(could not load)" : "nothing needs you",
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
            text.AppendLine(
                $"{item.Id,-8} {item.State,-12} {Clean(item.Title)}");
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

        return state.LastAction is { Length: > 0 } said ? Clean(said) : "";
    }

    public static string Modal(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Mode switch
        {
            UiMode.Help => Help(state),
            UiMode.FlightActions => Actions(state),
            _ => "",
        };
    }

    /// <summary>
    /// Help, generated from the bindings.
    /// </summary>
    /// <remarks>
    /// Written by hand it would be a third list of keys, after the keymap and
    /// the hint line, and the one people read when they are already confused.
    /// </remarks>
    private static string Help(AppState state)
    {
        var text = new StringBuilder();
        foreach (var binding in Keymap.Bindings(
            new KeymapContext(UiMode.Normal, state.LiveVisible, state.Frozen,
                state.TakeableTree is not null, state.TakenOver)))
        {
            text.AppendLine($"  {binding.Key.Name,-8}{binding.Description}");
        }
        text.AppendLine($"  {Keymap.Interrupt.Name,-8}quit from anywhere");
        text.AppendLine();
        text.AppendLine($"  queue order: {QueueSort.Default.Name}");
        return text.ToString().TrimEnd();
    }

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
            + "  Nothing can be done from here yet.\n"
            + "  Taking a flight arrives in slice two; this console builds\n"
            + "  its precondition and does not pretend to more.";

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
