using System.Text;
using System.Text.Json;
using Gg.Client;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Console;

/// <summary>One agent session the mux started, as the ledger keeps it: no transcript, only where.</summary>
/// <param name="Machine">
/// The runner a remote session ran on, so history resumes it there; null for a session on
/// this machine (slice seventy).
/// </param>
public sealed record MuxSession(
    string Id, string Label, string Directory, DateTimeOffset Started, string? Machine = null);

/// <summary>
/// The sessions this machine's mux started, one JSON line each, at
/// <c>StateRoot/mux/sessions.jsonl</c> (slice sixty-nine).
/// </summary>
/// <remarks>
/// <b>Local, like the drafts.</b> History is this machine's record. A session's transcript stays
/// Claude Code's; this knows its id, what gg called it, where it ran and when it started, which
/// is what resuming it needs.
/// </remarks>
public sealed class MuxLedger(string path)
{
    private static readonly Lock Writing = new();

    public static MuxLedger ForThisMachine(string? stateHome = null) =>
        new(System.IO.Path.Combine(LocalPaths.StateRoot(stateHome), "mux", "sessions.jsonl"));

    public string Path { get; } = path;

    public void Keep(MuxSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("id", session.Id);
            json.WriteString("label", session.Label);
            json.WriteString("directory", session.Directory);
            json.WriteString("started", session.Started);

            // ONLY WHEN IT RAN ELSEWHERE, so a local session's line is the line it always was.
            if (session.Machine is { Length: > 0 } machine)
            {
                json.WriteString("machine", machine);
            }

            json.WriteEndObject();
        }

        lock (Writing)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.AppendAllText(Path, Encoding.UTF8.GetString(buffer.ToArray()) + "\n");
        }
    }

    /// <summary>Every session, newest first. A line that does not read is skipped, not fatal.</summary>
    public IReadOnlyList<MuxSession> Read()
    {
        if (!File.Exists(Path))
        {
            return [];
        }

        var sessions = new List<MuxSession>();
        foreach (var line in File.ReadAllLines(Path))
        {
            try
            {
                using var read = JsonDocument.Parse(line);
                var root = read.RootElement;
                sessions.Add(new MuxSession(
                    root.GetProperty("id").GetString()!,
                    root.GetProperty("label").GetString()!,
                    root.GetProperty("directory").GetString()!,
                    root.GetProperty("started").GetDateTimeOffset(),
                    root.TryGetProperty("machine", out var machine) ? machine.GetString() : null));
            }
            catch (Exception unreadable) when (unreadable is JsonException or KeyNotFoundException
                                                  or InvalidOperationException or FormatException)
            {
            }
        }

        return [.. sessions.OrderByDescending(session => session.Started)];
    }
}

/// <summary>What opening a history row does.</summary>
public enum HistoryKind
{
    Heading,
    Proposal,
    Session,
}

/// <summary>One row of the history screen.</summary>
/// <param name="Text">What the row says.</param>
/// <param name="Kind">Whether it is a heading, a proposal or a session.</param>
/// <param name="Reference">The plan's ITN reference, or the session's id.</param>
/// <param name="Directory">Where a session ran, which is where it resumes.</param>
/// <param name="Machine">The runner a remote session ran on; null for one on this machine.</param>
public sealed record HistoryRow(
    string Text, HistoryKind Kind, string? Reference = null, string? Directory = null, string? Machine = null);

/// <summary>
/// The history screen's rows (slice sixty-nine): every proposal recorded beside a draft, with its
/// plan's state read live, then every session the mux started, newest first.
/// </summary>
public static class MuxHistory
{
    /// <param name="drafts">Where the drafts and their <c>.proposed</c> records are.</param>
    /// <param name="ledger">The mux's sessions.</param>
    /// <param name="plan">
    /// Reads one plan from the control plane, or null when it cannot be read. Asked once per
    /// proposal each time history opens: the state is the plan's now, not when it was proposed.
    /// </param>
    public static IReadOnlyList<HistoryRow> Rows(
        ItineraryDrafts drafts, MuxLedger? ledger, Func<string, BoardPage?> plan)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        ArgumentNullException.ThrowIfNull(plan);

        var rows = new List<HistoryRow> { new("Proposals", HistoryKind.Heading) };

        var proposed = drafts.ProposedDrafts();
        if (proposed.Count == 0)
        {
            rows.Add(new("  nothing proposed from this machine yet", HistoryKind.Heading));
        }

        foreach (var (draft, record) in proposed.OrderByDescending(p => p.Plan.At))
        {
            rows.Add(new(
                $"  {record.Itinerary} · {StateOf(plan(record.Itinerary))} · draft {draft} · {When(record.At)}",
                HistoryKind.Proposal,
                record.Itinerary));
        }

        rows.Add(new("", HistoryKind.Heading));
        rows.Add(new("Sessions", HistoryKind.Heading));

        var sessions = ledger?.Read() ?? [];
        if (sessions.Count == 0)
        {
            rows.Add(new("  no agent sessions started from gg yet", HistoryKind.Heading));
        }

        foreach (var session in sessions)
        {
            rows.Add(new(
                $"  {session.Label} · {When(session.Started)} · {session.Directory}",
                HistoryKind.Session,
                session.Id,
                session.Directory,
                session.Machine));
        }

        return rows;
    }

    /// <summary>A plan's state, in a few words, from its legs.</summary>
    public static string StateOf(BoardPage? plan)
    {
        if (plan is null)
        {
            return "could not be read";
        }

        if (plan.Nominations.Count == 0)
        {
            return "no legs";
        }

        var said = plan.Nominations
            .GroupBy(leg => leg.Ending ?? leg.State)
            .OrderByDescending(group => group.Count())
            .Select(group => $"{group.Count()} {group.Key}");

        return $"{plan.Nominations.Count} legs: " + string.Join(", ", said);
    }

    /// <summary>One plan, opened: each leg, its kind, and where it stands.</summary>
    public static string Describe(string itinerary, BoardPage? plan)
    {
        if (plan is null)
        {
            return $"{itinerary} could not be read from the control plane.";
        }

        var text = new StringBuilder().Append(itinerary).Append(" · ").Append(StateOf(plan)).Append("\n\n");
        foreach (var leg in plan.Nominations)
        {
            text.Append("  ").Append(leg.Subject).Append(" · ").Append(leg.WorkKind).Append(" · ")
                .Append(leg.Ending ?? leg.State);
            if (leg.FlightNumber is { } flight)
            {
                text.Append(" · ").Append(flight);
            }

            text.Append('\n');
        }

        return text.ToString();
    }

    private static string When(DateTimeOffset at) => at.ToLocalTime().ToString("MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}
