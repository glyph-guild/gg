using Gg.Contracts;

namespace Gg.Runner.Intent;

/// <summary>How a field read as instructions stands against what wrote it (slice eighty-two).</summary>
public static class InstructionStandings
{
    /// <summary>The field holds what an earlier flight wrote, unchanged since.</summary>
    public const string AsWritten = "as-written";

    /// <summary>A person changed the field after an earlier flight wrote it; their text governs.</summary>
    public const string EditedSince = "edited-since";

    /// <summary>No recorded flight wrote the field.</summary>
    public const string Unrecorded = "unrecorded";

    /// <summary>The field is empty or absent; the flight runs on the item.</summary>
    public const string Empty = "empty";

    /// <summary>The field could not be read; the flight runs on the item.</summary>
    public const string Unreadable = "unreadable";
}

/// <summary>What a flight was given from its item's field, and how it stands.</summary>
public sealed record ItemInstructionsRead
{
    public required string Field { get; init; }

    public string? Text { get; init; }

    public required string Standing { get; init; }

    /// <summary>The flight that wrote it, when one is recorded.</summary>
    public string? WrittenBy { get; init; }

    /// <summary>Why the field could not be read, when it could not.</summary>
    public string? Diagnosis { get; init; }
}

/// <summary>A tracker that can say a field's value and which revisions touched it.</summary>
public interface IItemFieldHistory
{
    /// <summary>The field's current value, and the revision number of every update that set it.</summary>
    Task<(string? Value, IReadOnlyList<int> Touched)> FieldAsync(
        string id, string field, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads a flight's instructions from its item's field and says how they stand (slice
/// eighty-two, ADR-0041 Decision 10).
/// </summary>
public static class ItemInstructions
{
    /// <summary>How a field stands, from its value, the revisions that touched it and the findings.</summary>
    /// <remarks>
    /// <b>By revision, never by text</b> (owner, 2026-10-15): a tracker rewrites rich text on save.
    /// The newest finding that wrote this field on this item says which revision its write
    /// produced; any later revision that touched the field is a person's edit.
    /// </remarks>
    public static ItemInstructionsRead Decide(
        string id, string field, string? value, IReadOnlyList<int> touched,
        IReadOnlyList<EarlierFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(touched);
        ArgumentNullException.ThrowIfNull(findings);

        if (string.IsNullOrWhiteSpace(value))
        {
            return new() { Field = field, Standing = InstructionStandings.Empty };
        }

        var wrote = findings
            .Where(f => string.Equals(f.Target, id, StringComparison.Ordinal)
                && string.Equals(f.Field, field, StringComparison.Ordinal))
            .MaxBy(f => f.FoundAt);

        if (wrote is null)
        {
            return new() { Field = field, Text = value, Standing = InstructionStandings.Unrecorded };
        }

        // WRITTEN, BUT NO REVISION RECORDED: a flight from before the write was a fact. Who wrote
        // it is known; whether a person changed it since is not, and that is said.
        if (wrote.Revision is not { } revision)
        {
            return new()
            {
                Field = field, Text = value, Standing = InstructionStandings.Unrecorded,
                WrittenBy = wrote.FlightNumber,
            };
        }

        return new()
        {
            Field = field,
            Text = value,
            Standing = touched.Any(r => r > revision)
                ? InstructionStandings.EditedSince
                : InstructionStandings.AsWritten,
            WrittenBy = wrote.FlightNumber,
        };
    }

    /// <summary>Reads the field and decides; a read that fails is said, never thrown.</summary>
    public static async Task<ItemInstructionsRead> ReadAsync(
        IItemFieldHistory tracker, string id, string field, IReadOnlyList<EarlierFinding> findings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tracker);

        try
        {
            var (value, touched) = await tracker.FieldAsync(id, field, cancellationToken);
            return Decide(id, field, value, touched, findings);
        }
        catch (Exception failed) when (failed is HttpRequestException or System.Text.Json.JsonException
            or InvalidOperationException or TaskCanceledException)
        {
            return new()
            {
                Field = field, Standing = InstructionStandings.Unreadable, Diagnosis = failed.Message,
            };
        }
    }
}
