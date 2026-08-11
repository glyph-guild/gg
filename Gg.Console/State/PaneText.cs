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

        if (state.Queue.Count == 0)
        {
            // Nothing needing me and nothing printed look identical, and one of
            // them is a queue that failed to load.
            return [state.Diagnosis is { Length: > 0 } ? "(could not load)" : "nothing needs you"];
        }

        return
        [
            .. state.Queue.Select(row =>
            {
                var unread = row.UnreadArrivals > 0 ? $" ({row.UnreadArrivals})" : "";
                return Clean($"{row.FlightNumber,-9} {Reason(row.Reason),-18} {row.Name}{unread}");
            }),
        ];
    }

    /// <summary>Why a flight is in the queue, in words rather than an enum name.</summary>
    public static string Reason(QueueReason reason) => reason switch
    {
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
        text.AppendLine("  facts         (none produced yet)");

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

    private static string Intent(FlightIntent intent) => intent.Kind switch
    {
        FlightIntentKinds.Uri => Clean(intent.Uri),
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

        return state.Flight is null
            ? "No flight selected."
            : "No evidence yet. The digest is computed by the runner, before the filter, "
            + "and nothing produces one until the executor exists.";
    }

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
            return "Nothing is running. This pane is off by default and is meant to stay that way.";
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
        foreach (var binding in Keymap.Bindings(new KeymapContext(UiMode.Normal, state.LiveVisible, state.Frozen)))
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
