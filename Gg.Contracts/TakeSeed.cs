using System.Text;

namespace Gg.Contracts;

/// <summary>Why the agent's closing account is not in the seed.</summary>
/// <remarks>
/// <b>A closed vocabulary rather than an enumeration, because this crosses now.</b>
/// It was a C# enum while the seed was a document one console composed for itself.
/// A enum on the wire serializes as an integer: unreadable to the auditor this
/// package exists for, and invisible to the mechanism that discovers closed
/// vocabularies by shape and forces a version when one gains a value.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class AccountStates
{
    /// <summary>It is here, in the agent's own words.</summary>
    public const string Present = "present";

    /// <summary>There is none, and the seed says so out loud.</summary>
    public const string Missing = "missing";

    /// <summary>It was too long and the seed says where it stops.</summary>
    public const string Truncated = "truncated";

    public static IReadOnlyList<string> All { get; } = [Present, Missing, Truncated];
}

/// <summary>Why the loop's transcript is not in the seed.</summary>
/// <remarks>
/// <para>
/// <b>There is no <c>present</c>, and its absence is the honest part.</b> A
/// transcript holds customer code, so it never crosses; <see cref="ArtifactScopes"/>
/// has exactly one value and the reference only resolves on the machine that
/// produced it. So the two states are "it exists somewhere you cannot reach" and
/// "there is none" - and a person sent to those two places needs different
/// sentences.
/// </para>
/// <para>
/// When a Storage port ships, <c>tenant</c> joins <see cref="ArtifactScopes"/> and a
/// third value belongs here. Until then, declaring a state nothing can produce
/// would be a promise the platform does not keep.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class TranscriptStates
{
    /// <summary>It exists, on the machine that produced it, and cannot be fetched.</summary>
    public const string Elsewhere = "elsewhere";

    /// <summary>The flight produced none.</summary>
    public const string None = "none";

    public static IReadOnlyList<string> All { get; } = [Elsewhere, None];
}

/// <summary>
/// What we measured about a loop. Computed by us, always present.
/// </summary>
/// <remarks>
/// Every field here is something a machine counted from the event stream. That
/// is what makes them required: they exist for every flight that ran at all, and
/// nothing in the takeover path may depend on anything that does not.
/// </remarks>
[PinnedId("49230185-d6b9-401f-8274-259d69ffeb76")]
public sealed record TakeMeasurements
{
    public required IReadOnlyList<string> FilesEdited { get; init; }

    public required IReadOnlyList<string> FilesReadNotEdited { get; init; }

    public required IReadOnlyList<string> Searches { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }

    /// <summary>
    /// Tools the agent used that the envelope's moves did not declare.
    /// </summary>
    /// <remarks>
    /// <b>Used, not refused, and the difference is not cosmetic.</b> This was called
    /// <c>RefusedMoves</c> and rendered as "moves refused", which says the system
    /// stopped something. It did not: the allow-list passed to the executor does not
    /// bind - measured, both directions, in <c>EnforcesMovesTests</c> - so these are
    /// tools the agent reached for, was not declared for, and used anyway.
    /// </remarks>
    public required IReadOnlyList<string> UndeclaredMovesUsed { get; init; }

    public required int Attempts { get; init; }

    /// <summary>Where the loop stopped, from <see cref="LoopOutcomes"/>.</summary>
    public required string StopReason { get; init; }

    /// <summary>What the obligation decided, when one was evaluated.</summary>
    public string? Verdict { get; init; }
}

/// <summary>
/// Everything a person or a resuming loop needs to pick a flight up, and nothing
/// that belongs to one machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measurements, plus the agent's account, marked as its words.</b> The
/// account is the thing that stops somebody re-deriving a decision the agent
/// already made and recorded nowhere else - and marking it is what stops that
/// decision being read as a measurement.
/// </para>
/// <para>
/// <b>Nothing here may require the account.</b> It is a <c>string?</c> and the
/// state beside it is a closed vocabulary, so "the agent said nothing" and "its
/// words were dropped" cannot render the same way. That is structural rather than
/// a null check somebody remembers, because this project's most repeated defect
/// is a plausible value where the absence should have been loud.
/// </para>
/// <para>
/// <b>And the account never enters the digest.</b> Article XIII compares
/// accumulated flights, which needs records computed identically every time;
/// agent prose differs every run. The digest is a fact and this is a document -
/// and a test says so.
/// </para>
/// <para>
/// <b>There is no tree path, and its absence is the whole point of this type.</b>
/// It used to carry one - "where the work is, on this machine" - which made a
/// handoff work only for somebody at that keyboard. Git already moves a working
/// tree between machines; what git cannot reconstruct is what was tried and ruled
/// out, and that is this.
/// </para>
/// </remarks>
[PinnedId("efb13db0-fb07-488c-936f-44d004e97289")]
public sealed record TakeSeed
{
    /// <summary>
    /// What shape a seed composed by this build is in.
    /// </summary>
    /// <remarks>
    /// <b>A second number, and it earns itself.</b> The protocol revision says
    /// which conversation the two sides are having. This says what shape the
    /// document is in - and the document is read by an AGENT as declared context,
    /// so a shape that changed silently would change what every future resumption
    /// knows with nothing anywhere to point at.
    /// </remarks>
    public const int CurrentRevision = 1;

    /// <summary>Which shape this seed is, from <see cref="CurrentRevision"/>.</summary>
    /// <remarks>
    /// Carried rather than assumed: a reader has a payload, not a build, and
    /// cannot see a constant.
    /// </remarks>
    public required int Revision { get; init; }

    /// <summary>What a person types. GG-42.</summary>
    public required string FlightNumber { get; init; }

    /// <summary>What a return has to name.</summary>
    public required string FlightId { get; init; }

    public required TakeMeasurements Measurements { get; init; }

    /// <summary>The agent's closing words, when there are any.</summary>
    public string? Account { get; init; }

    /// <summary>One of <see cref="AccountStates"/>.</summary>
    public required string AccountState { get; init; }

    /// <summary>How much of the account was kept, when it was truncated.</summary>
    public int AccountBytes { get; init; }

    /// <summary>Why there is no account, when there is none.</summary>
    /// <remarks>
    /// "no account: the runner was killed" and "no account: the agent produced
    /// none" send a person to different places. An absent section sends them
    /// nowhere.
    /// </remarks>
    public string? AccountAbsence { get; init; }

    /// <summary>
    /// Where the loop's transcript is, when there is one.
    /// </summary>
    /// <remarks>
    /// <b>Named, never followed.</b> The locator resolves on one machine and the
    /// bytes hold customer code, so what is carried is the reference - hash, size,
    /// media type and scope - and a reader learns the artifact exists and that
    /// this platform cannot fetch it. Dereferencing is a capability this platform
    /// does not have, declared on the artifact rather than discovered by an empty
    /// fetch.
    /// </remarks>
    public ArtifactReference? Transcript { get; init; }

    /// <summary>One of <see cref="TranscriptStates"/>.</summary>
    public required string TranscriptState { get; init; }

    /// <summary>Why there is no transcript to name, when there is none.</summary>
    public string? TranscriptAbsence { get; init; }

    /// <summary>
    /// What the last person to work on this said they did, in their own name.
    /// </summary>
    /// <remarks>
    /// <b>A human assertion, and marked as one.</b> Three kinds of claim now
    /// reach whoever takes a flight over - what we measured, what the agent
    /// said about itself, and what a previous taker asserted - and a reader who
    /// cannot tell them apart will weigh a guess like a measurement.
    /// </remarks>
    public HumanAccount? PriorHuman { get; init; }

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    /// <remarks>
    /// Checked on the way out of a composer and on the way in from the wire. A
    /// seed is read by an agent as context, so a malformed one is not a display
    /// problem.
    /// </remarks>
    public static string? Validate(TakeSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        if (seed.Revision <= 0)
        {
            return "A seed says what shape it is in. Revision 0 is a seed composed by something "
                 + "that did not know it had a shape.";
        }

        if (!AccountStates.All.Contains(seed.AccountState, StringComparer.Ordinal))
        {
            return $"Unknown account state '{seed.AccountState}'. Expected one of: "
                 + string.Join(", ", AccountStates.All) + ".";
        }

        if (!TranscriptStates.All.Contains(seed.TranscriptState, StringComparer.Ordinal))
        {
            return $"Unknown transcript state '{seed.TranscriptState}'. Expected one of: "
                 + string.Join(", ", TranscriptStates.All) + ".";
        }

        // THE PAIR, both ways. A state saying the transcript is elsewhere with no
        // reference beside it tells a person an artifact exists and gives them no
        // way to know which; a reference with the state saying there is none is
        // the same contradiction wearing the other face.
        if (string.Equals(seed.TranscriptState, TranscriptStates.Elsewhere, StringComparison.Ordinal)
            && seed.Transcript is null)
        {
            return "The seed says a transcript is on another machine and names none. A reader "
                 + "cannot act on an artifact with no locator and no hash.";
        }

        return !string.Equals(
                   seed.TranscriptState, TranscriptStates.Elsewhere, StringComparison.Ordinal)
               && seed.Transcript is not null
            ? "The seed names a transcript and says there is none. One of the two is wrong and a "
            + "reader cannot tell which."
            : null;
    }
}

/// <summary>
/// Turns a flight's evidence into the thing a person reads before taking over.
/// </summary>
/// <remarks>
/// <para>
/// <b>In the contract, so there is one composer rather than two.</b> It ran in
/// the console from a local digest; the control plane composes it now, from facts
/// it already holds, and serves it. Reimplementing the composition on that side
/// would be two derivations of one document and they would drift on the first
/// change to either - the same argument <c>CredentialLocator.ForRepo</c> is here
/// for.
/// </para>
/// <para>
/// <b>Composed here, rendered here, and stripped here.</b> Control sequences are
/// stripped at production, and it is re-asserted anyway: this is the one place in
/// the product where text is deliberately put in front of a terminal or an agent,
/// and inheriting a property is not the same as having it.
/// </para>
/// <para>
/// <b>The account is bounded.</b> A seed nobody can read is a seed nobody reads.
/// </para>
/// </remarks>
public static class TakeSeedComposer
{
    /// <summary>How much of the agent's account is worth carrying.</summary>
    /// <remarks>
    /// Enough for a closing paragraph and not enough for a transcript. When it
    /// bites, the seed says so - a summary that stops mid-sentence with no mark
    /// reads as an agent that stopped mid-sentence.
    /// </remarks>
    public const int MaxAccount = 4000;

    /// <summary>The seed for one flight.</summary>
    /// <param name="flightNumber">What a person types.</param>
    /// <param name="flightId">What a return must name.</param>
    /// <param name="digest">What we measured, when the flight got far enough to be measured.</param>
    /// <param name="account">The agent's closing words, or null.</param>
    /// <param name="transcript">The transcript's reference, when the flight produced one.</param>
    /// <param name="verdict">The obligation's decision, when one was reached.</param>
    /// <param name="priorHuman">What a previous taker asserted, in their own name.</param>
    public static TakeSeed Compose(
        string flightNumber,
        string flightId,
        LoopDigest? digest,
        string? account,
        ArtifactReference? transcript = null,
        string? verdict = null,
        HumanAccount? priorHuman = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flightNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(flightId);

        var measurements = new TakeMeasurements
        {
            // A flight killed before it produced a digest still gets a seed.
            // Empty measurements are measurements; a takeover that refused to
            // start because a list was empty would fail exactly the flight most
            // likely to need taking over.
            FilesEdited = Clean(digest?.FilesEdited),
            FilesReadNotEdited = Clean(digest?.FilesReadNotEdited),
            Searches = Clean(digest?.Searches),
            Errors = Clean(digest?.Errors.Select(e => $"{e.Source}: {e.Detail}").ToList()),
            UndeclaredMovesUsed = Clean(digest?.RefusedMoves),
            Attempts = digest?.Attempts ?? 0,
            StopReason = ControlText.Strip(digest?.StopReason) is { Length: > 0 } stop
                ? stop
                : "unknown",
            Verdict = verdict is null ? null : ControlText.Strip(verdict),
        };

        var seed = new TakeSeed
        {
            Revision = TakeSeed.CurrentRevision,
            FlightNumber = flightNumber,
            FlightId = flightId,
            Measurements = measurements,
            Account = null,
            AccountState = AccountStates.Missing,
            AccountAbsence = "the flight produced none",
            PriorHuman = priorHuman,
            Transcript = transcript,
            TranscriptState = transcript is null
                ? TranscriptStates.None
                : TranscriptStates.Elsewhere,
            TranscriptAbsence = transcript is null ? "the flight produced none" : null,
        };

        if (account is not { Length: > 0 })
        {
            return seed;
        }

        // Line breaks survive: this is prose somebody wrote for a reader.
        var cleaned = ControlText.Strip(account, allowLineBreaks: true);
        var truncated = cleaned.Length > MaxAccount;

        return seed with
        {
            Account = truncated ? cleaned[..MaxAccount] : cleaned,
            AccountState = truncated ? AccountStates.Truncated : AccountStates.Present,
            AccountBytes = truncated ? MaxAccount : cleaned.Length,
            AccountAbsence = null,
        };
    }

    /// <summary>
    /// The seed as a person reads it.
    /// </summary>
    /// <remarks>
    /// <b>The marking is the point.</b> The measurements are what we counted; the
    /// account is what the agent said; the last section is what a colleague
    /// asserted. A reader who cannot tell them apart will treat a claim as a
    /// measurement, which is the whole failure this design is avoiding - so each
    /// gets a header saying whose words these are, and every absence gets a line
    /// rather than a gap.
    /// </remarks>
    public static string Render(TakeSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        var text = new StringBuilder();

        text.AppendLine($"{seed.FlightNumber} — taking over");
        text.AppendLine();
        text.AppendLine($"Flight id: {seed.FlightId}");
        text.AppendLine();

        text.AppendLine("MEASURED (by gg, from the run's own event stream)");
        text.AppendLine($"  stopped:  {seed.Measurements.StopReason} after "
                      + $"{seed.Measurements.Attempts} turn(s)");

        if (seed.Measurements.Verdict is { Length: > 0 } verdict)
        {
            text.AppendLine($"  verdict:  {verdict}");
        }

        Section(text, "changed", seed.Measurements.FilesEdited);
        Section(text, "read, not changed", seed.Measurements.FilesReadNotEdited);
        Section(text, "searched for", seed.Measurements.Searches);
        Section(text, "errors", seed.Measurements.Errors);
        // NOT "moves refused". Nothing refused them - the allow-list does not bind, so
        // this is what the agent did outside what the envelope declared. A person
        // taking the flight over reads this and acts on it.
        Section(text, "moves used but not declared", seed.Measurements.UndeclaredMovesUsed);

        text.AppendLine();

        switch (seed.AccountState)
        {
            case AccountStates.Present:
                text.AppendLine("THE AGENT'S OWN ACCOUNT (its words, not a measurement)");
                text.AppendLine(Indent(seed.Account!));
                break;

            case AccountStates.Truncated:
                text.AppendLine("THE AGENT'S OWN ACCOUNT (its words, not a measurement)");
                text.AppendLine(Indent(seed.Account!));
                text.AppendLine($"  [truncated at {seed.AccountBytes} characters]");
                break;

            case AccountStates.Missing:
                // LOUD. An absent section and an agent that said nothing must not
                // read the same way, and the difference is a line that says which.
                text.AppendLine($"NO ACCOUNT: {seed.AccountAbsence}");
                text.AppendLine("  The measurements above are everything there is.");
                break;
        }

        text.AppendLine();
        Transcript(text, seed);

        // LAST, and marked hardest of the three. Whoever reads this is picking up
        // work somebody else did, and the one thing they must not do is mistake a
        // colleague's assertion for something a machine measured.
        if (seed.PriorHuman is { } human)
        {
            text.AppendLine();
            text.AppendLine(
                $"A PERSON WORKED ON THIS BEFORE YOU — {human.By} says (their words, a human "
              + "assertion):");
            text.AppendLine(Indent(human.Statement));
            text.AppendLine($"  [{Confirmed(human)}, {human.ConfirmedAt:yyyy-MM-dd HH:mm}]");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// Where the transcript is, said out loud in both cases.
    /// </summary>
    /// <remarks>
    /// <b>The locator is quoted as a locator, never as a path.</b> It is an
    /// absolute path on somebody's runner, and printing it as one would put a
    /// machine's filesystem into a document whose whole claim is that it has none -
    /// so what is rendered is the scope, the size and the hash, which is what
    /// identifies the artifact, plus the fact that this platform cannot follow it.
    /// Somebody who needs the bytes goes to that machine knowing exactly what to
    /// look for.
    /// </remarks>
    private static void Transcript(StringBuilder text, TakeSeed seed)
    {
        if (seed.Transcript is not { } reference)
        {
            text.AppendLine($"NO TRANSCRIPT: {seed.TranscriptAbsence}");
            return;
        }

        text.AppendLine($"TRANSCRIPT: {reference.Bytes} bytes of {reference.MediaType}, scope "
                      + $"{reference.Scope}");
        text.AppendLine($"  sha256 {reference.Sha256}");
        text.AppendLine("  It is on the machine that ran this flight and cannot be fetched from "
                      + "here.");
        text.AppendLine("  A transcript holds the customer's own code, so it never crosses. The "
                      + "measurements above are what did.");
    }

    /// <summary>
    /// How much of that account was theirs.
    /// </summary>
    /// <remarks>
    /// An accepted account is one they read and agreed with; an edited one is a
    /// sentence they changed; a written one is entirely theirs. All three are
    /// their assertion, and the difference is still worth a reader knowing.
    /// </remarks>
    private static string Confirmed(HumanAccount human) => human.Confirmation switch
    {
        AccountConfirmations.Accepted => "accepted an account proposed to them",
        AccountConfirmations.Edited => "edited the account proposed to them",
        AccountConfirmations.Replaced => "wrote this themselves",
        _ => human.Confirmation,
    };

    private static void Section(StringBuilder text, string title, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        text.AppendLine($"  {title}:");
        foreach (var item in items)
        {
            text.AppendLine($"    - {item}");
        }
    }

    private static string Indent(string account) =>
        string.Join('\n', account.Split('\n').Select(l => "  " + l));

    private static IReadOnlyList<string> Clean(IReadOnlyList<string>? values) =>
        values is null ? [] : [.. values.Select(v => ControlText.Strip(v))];
}
