using Gg.Client;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>One agent session the mux started.</summary>
public sealed record MuxSession(string Id, string Label, string Directory, DateTimeOffset Started);

/// <summary>The sessions this machine's mux started. Declared; nothing is kept yet.</summary>
public sealed class MuxLedger(string path)
{
    public string Path { get; } = path;

    public void Keep(MuxSession session)
    {
    }

    public IReadOnlyList<MuxSession> Read() => [];
}

public enum HistoryKind
{
    Heading,
    Proposal,
    Session,
}

public sealed record HistoryRow(string Text, HistoryKind Kind, string? Reference = null, string? Directory = null);

/// <summary>The history screen's rows. Declared; nothing is listed yet.</summary>
public static class MuxHistory
{
    public static IReadOnlyList<HistoryRow> Rows(
        ItineraryDrafts drafts, MuxLedger? ledger, Func<string, BoardPage?> plan) => [];

    public static string Describe(string itinerary, BoardPage? plan) => "";
}
