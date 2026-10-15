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
    public static ItemInstructionsRead Decide(
        string id, string field, string? value, IReadOnlyList<int> touched,
        IReadOnlyList<EarlierFinding> findings) =>
        new() { Field = field, Standing = InstructionStandings.Unrecorded };

    /// <summary>Reads the field and decides; a read that fails is said, never thrown.</summary>
    public static Task<ItemInstructionsRead> ReadAsync(
        IItemFieldHistory tracker, string id, string field, IReadOnlyList<EarlierFinding> findings,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ItemInstructionsRead { Field = field, Standing = InstructionStandings.Unreadable });
}
