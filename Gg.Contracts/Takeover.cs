namespace Gg.Contracts;

/// <summary>How a person left a flight they took over.</summary>
/// <remarks>
/// Three, because three is what a person actually does. <c>handing-back</c> is
/// declared here and served by nothing until step 7: the vocabulary is the thing
/// both sides read, and a value that arrives before its handler is refused by
/// name rather than mistaken for something else.
/// </remarks>
public static class TakeoverOutcomes
{
    /// <summary>They finished the work.</summary>
    public const string Completed = "completed";

    /// <summary>They stopped, and the work stands where they left it.</summary>
    public const string Abandoned = "abandoned";

    /// <summary>They want the agent to carry on. Step 7.</summary>
    public const string HandingBack = "handing-back";

    public static IReadOnlyList<string> All { get; } = [Completed, Abandoned, HandingBack];
}

/// <summary>
/// What a person writes when they are done, read back by the console.
/// </summary>
/// <remarks>
/// <para>
/// <b>A schema rather than a summary.</b> The console reads this file after
/// handing the terminal to somebody for minutes, and it has no idea what happened
/// in between. Optimism here produces a client that silently applies a garbled
/// decision, so the parser returns nothing at all rather than a best effort, and
/// the flight is left untouched for a person to resolve.
/// </para>
/// <para>
/// <b><see cref="FlightId"/> is the field that makes this safe.</b> A file left
/// over from a previous takeover parses perfectly and describes a different
/// flight; applying it would put one flight's decision on another, which is worse
/// than losing the decision entirely. The id is required for that reason alone.
/// </para>
/// </remarks>
[PinnedId("2a9f4c17-8b30-4e65-9d82-c1f70ea34b58")]
public sealed record TakeoverReturn
{
    /// <summary>Which flight this decides. Checked against the one that was taken.</summary>
    public required string FlightId { get; init; }

    /// <summary>One of <see cref="TakeoverOutcomes"/>.</summary>
    public required string Outcome { get; init; }

    /// <summary>What they want to say about it, if anything.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// The diagnosis, or null when there is nothing wrong.
    /// </summary>
    /// <remarks>
    /// Every failure here ends the same way - the flight is untouched - so the
    /// diagnosis exists to tell a person which of the three it was. "The return
    /// file could not be read" sends them looking at the disk; "it describes
    /// GG-7 and you took GG-9" tells them what happened.
    /// </remarks>
    public static string? Validate(TakeoverReturn? decision, string expectedFlightId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedFlightId);

        if (decision is null)
        {
            return "The return file could not be read as a decision. The flight is untouched.";
        }

        if (string.IsNullOrWhiteSpace(decision.FlightId))
        {
            return "The return file names no flight, so there is no way to know what it decides. "
                 + "The flight is untouched.";
        }

        if (!string.Equals(decision.FlightId, expectedFlightId, StringComparison.Ordinal))
        {
            return $"The return file decides flight '{decision.FlightId}' and the flight taken was "
                 + $"'{expectedFlightId}'. It is left where it is rather than applied to the wrong "
                 + "one: a decision on the wrong flight is worse than a decision lost.";
        }

        return TakeoverOutcomes.All.Contains(decision.Outcome, StringComparer.Ordinal)
            ? null
            : $"'{decision.Outcome}' is not an outcome this version understands. Expected one of: "
            + string.Join(", ", TakeoverOutcomes.All) + ". The flight is untouched.";
    }
}

/// <summary>
/// That somebody took a flight over, and what came back.
/// </summary>
/// <remarks>
/// <para>
/// Article XII: actions that cannot be attributed do not happen. A person held
/// this flight for a while and a machine did not, and the log has to be able to
/// say so - otherwise the record reads as though the agent produced whatever is
/// there.
/// </para>
/// <para>
/// <b>The absence of a return file is itself recorded.</b> Somebody who took a
/// flight and wrote nothing is a different event from somebody who decided, and
/// the two must not read alike.
/// </para>
/// </remarks>
[PinnedId("7e15d3ba-6c94-4f28-a03d-58b2917fe6c4")]
public sealed record TakeoverRecord
{
    /// <summary>Who took it. The session's principal, never a name typed in.</summary>
    public required string By { get; init; }

    /// <summary>When the console handed the terminal over.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>How long they held it.</summary>
    public required long HeldForMs { get; init; }

    /// <summary>
    /// What the return file said, or null when there was none or it was unusable.
    /// </summary>
    /// <remarks>
    /// Null and an outcome are different facts. "They took it and said nothing"
    /// is the ordinary end of an abandoned takeover; a diagnosis alongside a null
    /// says the file existed and could not be trusted.
    /// </remarks>
    public string? Outcome { get; init; }

    /// <summary>Why there is no outcome, when there is none.</summary>
    public string? Diagnosis { get; init; }

    /// <summary>What they wrote, if anything.</summary>
    public string? Note { get; init; }

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    public static string? Validate(TakeoverRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.By))
        {
            return "A takeover names who took it. An action nobody can be attributed to did not "
                 + "happen.";
        }

        if (record.HeldForMs < 0)
        {
            return "A takeover lasts a length of time, and this one is negative.";
        }

        return record.Outcome is { Length: > 0 } outcome
            && !TakeoverOutcomes.All.Contains(outcome, StringComparer.Ordinal)
                ? $"Unknown takeover outcome '{outcome}'. Expected one of: "
                + string.Join(", ", TakeoverOutcomes.All) + "."
                : null;
    }
}
