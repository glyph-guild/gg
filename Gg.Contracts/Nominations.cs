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

/// <summary>
/// One nomination, as the board shows it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Keyed on the nomination, and the flight is a member.</b> A standing row
/// has opened no flight — that is the whole state the board exists to render —
/// so <see cref="FlightId"/> is nullable and a summary keyed on a flight would
/// have nothing to say about most of what a person is reading.
/// </para>
/// <para>
/// <b>The ending and its sentence travel together, and both are absent while a
/// row stands.</b> A sentence with no ending would be a reason for something
/// that has not happened.
/// </para>
/// <para>
/// <b>What governed it is deliberately NOT here.</b> The row records it, and a
/// person auditing one asks for it by name; putting a digest and a layer list
/// on every line of a queue would make the thing a person scans mostly
/// provenance.
/// </para>
/// </remarks>
[PinnedId("6e8ace42-0322-48a6-8f10-47e51d6988b0")]
public sealed record NominationSummary
{
    public required Guid NominationId { get; init; }

    /// <summary>Who nominated: the flight an agent ran in, or the watch that swept.</summary>
    public required string Nominator { get; init; }

    public required string Subject { get; init; }

    /// <summary>The version of that subject which was nominated.</summary>
    public required string Version { get; init; }

    /// <summary>The kind to open, or the word that says a flight must decide.</summary>
    public required string WorkKind { get; init; }

    /// <summary>auto or gated, as it was in force when this was nominated.</summary>
    public required string Mode { get; init; }

    /// <summary>standing, or the ending's own word.</summary>
    public required string State { get; init; }

    /// <summary>Null exactly while the row stands.</summary>
    public string? Ending { get; init; }

    /// <summary>Why it ended, in a sentence. Null exactly while the row stands.</summary>
    public string? Because { get; init; }

    /// <summary>The flight this opened into, or null - which is every standing row.</summary>
    public Guid? FlightId { get; init; }

    /// <summary>Rendered, e.g. GG-42. Null wherever <see cref="FlightId"/> is.</summary>
    public string? FlightNumber { get; init; }

    public required DateTimeOffset MadeAt { get; init; }

    public DateTimeOffset? EndedAt { get; init; }
}

/// <summary>What the board answers with.</summary>
/// <remarks>
/// <b>It says whether it showed the ended rows, because a reader cannot infer
/// it.</b> A page of standing rows and a page that happens to contain no ended
/// ones look identical, and somebody reading "nothing was declined" off the
/// second would be reading a filter rather than a fact. The same reason
/// <c>envelope-version: none</c> is written down rather than left absent.
/// </remarks>
[PinnedId("311b2c1c-c80a-4912-bf73-6e6b665bc2db")]
public sealed record BoardPage
{
    public required IReadOnlyList<NominationSummary> Nominations { get; init; }

    /// <summary>Whether ended rows were included, rather than simply absent.</summary>
    public required bool IncludedEnded { get; init; }
}

/// <summary>What a person answers a standing nomination with.</summary>
/// <remarks>
/// <b>The outcome is an ENDING rather than a word of its own.</b> Opening a
/// nomination ends it <c>opened</c> and declining ends it <c>declined</c>, so
/// the door speaks the vocabulary the row records. A second set of words for
/// the same two states would be two spellings to keep agreeing, and the day
/// they stop is the day a door produces something no row can carry.
/// </remarks>
[PinnedId("c41d6e1a-2845-474e-9611-2d4c9ca3228c")]
public sealed record NominationDecision
{
    /// <summary>One of <see cref="NominationDecisions.All"/>.</summary>
    public required string Outcome { get; init; }

    /// <summary>
    /// Why, in a sentence somebody can read.
    /// </summary>
    /// <remarks>
    /// <b>Required, because every ending carries one.</b> A row whose reason is
    /// blank tells whoever finds it that something happened and nothing about
    /// what - and a decision is the ending most worth being able to ask about,
    /// because a person made it and can be asked why.
    /// </remarks>
    public required string Because { get; init; }
}

/// <summary>
/// The endings a person may cause.
/// </summary>
/// <remarks>
/// <b>Two of the six, and the other four are nobody's to type.</b> Superseding
/// is the board's, withdrawal is the world's, lapsing is the clock's and
/// refusal is the rules'. A door that accepted one of those would let somebody
/// record that the clock did what they did.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class NominationDecisions
{
    public static IReadOnlyList<string> All { get; } =
        [NominationEndings.Opened, NominationEndings.Declined];
}
