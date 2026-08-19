namespace Gg.Contracts;

/// <summary>How a person left a flight they took over.</summary>
/// <remarks>
/// Three, because three is what a person actually does. <c>handing-back</c> is
/// declared here and served by nothing until step 7: the vocabulary is the thing
/// both sides read, and a value that arrives before its handler is refused by
/// name rather than mistaken for something else.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
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
/// That the flight is yours, until when, and at which generation.
/// </summary>
/// <remarks>
/// <b>A hold rather than a record, and the difference is when it is written.</b>
/// What this replaced was posted when somebody had already finished - it carried
/// how long they held the flight - so two people on two machines could both take
/// the same stopped flight and both find out afterwards. This is written when they
/// start, atomically, and exactly one of two simultaneous claimants gets one.
/// </remarks>
[PinnedId("6bb92f29-58b3-4641-a26c-418461ab9b19")]
public sealed record TakeoverClaimed
{
    /// <summary>
    /// Which hold this is, incremented every time the flight is claimed.
    /// </summary>
    /// <remarks>
    /// The fence. Every later act on this hold carries it, so a renewal or a
    /// decision from somebody whose hold lapsed is refused rather than applied to
    /// whoever holds it now.
    /// </remarks>
    public required int Generation { get; init; }

    /// <summary>When it lapses if nobody renews it.</summary>
    public required DateTimeOffset HeldUntil { get; init; }

    /// <summary>
    /// How long before <see cref="HeldUntil"/> to renew.
    /// </summary>
    /// <remarks>
    /// <b>The server's number, not the client's.</b> A cadence a client invents is
    /// wrong on one of the two machines this feature exists to span, and there is
    /// no other rate limiter here.
    /// <c>DeviceAuthorizationStarted.PollIntervalSeconds</c> is the precedent.
    /// </remarks>
    public required int RenewWithinSeconds { get; init; }
}

/// <summary>
/// Why the claim was refused: somebody else has it.
/// </summary>
/// <remarks>
/// <b>The refusal is the product here.</b> "Somebody else has this" sends a person
/// nowhere. A name and two instants tell them whether to wait, to ask, or to come
/// back later - and this is the only thing standing between a portable handoff and
/// two people confidently editing divergent copies of one flight, each believing
/// they hold it.
/// </remarks>
[PinnedId("e895142c-0493-4081-b966-2f3da607bde0")]
public sealed record TakeoverHeld
{
    /// <summary>Who holds it. A principal's display, never a name typed in.</summary>
    public required string By { get; init; }

    /// <summary>When they claimed it.</summary>
    public required DateTimeOffset Since { get; init; }

    /// <summary>When it lapses if they do not renew.</summary>
    public required DateTimeOffset HeldUntil { get; init; }
}

/// <summary>Asking to keep a hold, at the generation the caller believes it is.</summary>
[PinnedId("0013b3a4-b885-4e03-8ab6-dbda919492d5")]
public sealed record TakeoverRenewalRequest
{
    /// <summary>The generation this caller was granted.</summary>
    public required int Generation { get; init; }
}

/// <summary>The hold, extended.</summary>
[PinnedId("754570f8-7932-4345-bc37-1356a0c0e3df")]
public sealed record TakeoverRenewed
{
    /// <summary>Unchanged by a renewal. A NEW generation means somebody else has it.</summary>
    public required int Generation { get; init; }

    public required DateTimeOffset HeldUntil { get; init; }
}

/// <summary>
/// A decision, on the wire, against the hold that made it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not <see cref="TakeoverReturn"/>, and the split is about who writes
/// each.</b> That one is a FILE a person writes by hand, so it names the flight -
/// a file left over from a previous takeover parses perfectly and describes a
/// different flight, and applying it would put one flight's decision onto
/// another. It cannot carry a generation, because nobody types one.
/// </para>
/// <para>
/// This is what <c>gg</c> posts. The flight is in the path, so naming it again
/// would be a second way to say one thing; what it needs instead is the
/// generation, which is what stops a decision landing on a hold that has since
/// moved to somebody else.
/// </para>
/// <para>
/// <b>How long the flight was held is NOT here.</b> The control plane knows when
/// the hold was claimed and what time it is; a client-supplied duration would be
/// an attributed measurement taken from the party it is about. Article XII is
/// about being able to read who did something, and a number they chose is not
/// that.
/// </para>
/// </remarks>
[PinnedId("1bee7042-6b44-44bb-8a61-756cf3ebbccd")]
public sealed record TakeoverReturnRequest
{
    /// <summary>The generation this caller holds.</summary>
    public required int Generation { get; init; }

    /// <summary>One of <see cref="TakeoverOutcomes"/>.</summary>
    public required string Outcome { get; init; }

    /// <summary>What they want to say about it, if anything.</summary>
    public string? Note { get; init; }

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    public static string? Validate(TakeoverReturnRequest? request)
    {
        if (request is null)
        {
            return "The return could not be read as a decision. The flight is untouched.";
        }

        if (request.Generation <= 0)
        {
            return "A return names the hold it decides. Generation 0 is a decision from nobody in "
                 + "particular, and applying one would attribute it to whoever holds the flight "
                 + "now.";
        }

        return TakeoverOutcomes.All.Contains(request.Outcome, StringComparer.Ordinal)
            ? null
            : $"'{request.Outcome}' is not an outcome this version understands. Expected one of: "
            + string.Join(", ", TakeoverOutcomes.All) + ". The flight is untouched.";
    }
}
