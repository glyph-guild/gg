namespace Gg.Contracts;

/// <summary>
/// Whether a nomination is still waiting to become a flight.
/// </summary>
/// <remarks>
/// <b>One live value and a closed set of endings, which is <c>FlightStates</c>'
/// shape one noun earlier.</b> A row is standing until something happens to it;
/// what happened is <see cref="NominationEndings"/>. The two are separate
/// because standing is the ABSENCE of an ending rather than one of them, and a
/// single enumeration holding both would let a row be recorded as having ended
/// in the state that means it has not.
/// </remarks>
public static class NominationStates
{
    /// <summary>No ending has been reached, and one still can be.</summary>
    public const string Standing = "standing";
}

/// <summary>
/// The ways a nomination stops standing. Six, and each names who ended it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Closed, for <c>FlightStates</c>' reason.</b> A seventh ending is a version
/// event rather than a value quietly appearing: every reader refuses a word it
/// does not know, which is what closing the vocabulary buys and what a quiet
/// addition would spend.
/// </para>
/// <para>
/// <b>Six rather than three, because who ended it is the part worth reading.</b>
/// A person asking "why is there no flight for this?" is asking which of six
/// things happened, and a vocabulary that answered "it ended" would send them to
/// the sentence to find out — which is a sentence they would then have to parse.
/// This vault already holds the line one noun over: <c>grounded</c> is not
/// <c>withdrawn</c>, and for the same reason.
/// </para>
/// <para>
/// <b>The contract ledger and not the fact one</b>, which is
/// <see cref="FlightStates"/>' membership for its reason: an ending is decided
/// control-plane-side by the board and travels outward to whoever reads it. No
/// runner ships one inside a fact, so a value added here changes what the wire
/// can say and never what a measurement can.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class NominationEndings
{
    /// <summary>Admission opened a flight from it.</summary>
    /// <remarks>
    /// The board's own act, and the only ending that leaves something behind.
    /// The flight it opened is named on the row; the flight does not name the
    /// row's nominator, which is what keeps two flights from referring to each
    /// other (ADR-0019 § 1).
    /// </remarks>
    public const string Opened = "opened";

    /// <summary>A newer version of the same subject arrived.</summary>
    /// <remarks>
    /// Also the board's, and it is what "the last nomination wins" became once
    /// the loser had a row. An agent that nominated twice changed its mind, and
    /// the answer that was withdrawn is visible rather than skipped.
    /// </remarks>
    public const string Superseded = "superseded";

    /// <summary>The subject ended, so the question ceased to apply.</summary>
    /// <remarks>
    /// The world's, not ours: the pull request merged, the work item closed, the
    /// tag came off. The one word this vocabulary shares with
    /// <see cref="FlightStates"/>, and it means there exactly what it means
    /// here.
    /// </remarks>
    public const string Withdrawn = "withdrawn";

    /// <summary>A person said no.</summary>
    /// <remarks>
    /// Distinct from <see cref="Refused"/> on purpose. A person deciding against
    /// work and a rule forbidding it are different answers to whoever asks why,
    /// and one word for both would make the reviewable case indistinguishable
    /// from the governed one.
    /// </remarks>
    public const string Declined = "declined";

    /// <summary>The rules said no, and the sentence says which rule.</summary>
    /// <remarks>
    /// The menu, the selection bound, the chain refusal, or a budget. Refused
    /// rather than clamped, and the sentence names what was asked for AND what
    /// was permitted — because a person reading it has to decide whether the
    /// envelope is wrong or the prompt is.
    /// </remarks>
    public const string Refused = "refused";

    /// <summary>Nobody opened it inside its window.</summary>
    /// <remarks>
    /// The clock's, and on an <c>auto</c> row it is a fault rather than a
    /// timeout: one that should have opened the moment it stood and did not has
    /// something wrong with it, and the sentence says how long it stood.
    /// </remarks>
    public const string Lapsed = "lapsed";

    /// <summary>Every ending, and the only words a recorded ending may carry.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Opened, Superseded, Withdrawn, Declined, Refused, Lapsed];
}
