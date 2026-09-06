namespace Gg.Contracts;

/// <summary>
/// What an attended session could not measure about itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>Closed, because the only safe response to an unknown gap is to halt.</b> A
/// reader that met a gap it did not recognise and carried on would be treating
/// <i>something was not measured and I cannot tell you what</i> as though it had
/// been handled — which is worse than an undeclared absence, because it reads as
/// having been considered.
/// </para>
/// <para>
/// <b>A set rather than the kind's existence, so it can shrink.</b> An executor
/// that later measures moves for an attended session says so by omitting
/// <see cref="Moves"/>, and nothing about the fact has to change. Deriving the
/// gaps from "this is a <c>loop.attended</c>" would freeze them at whatever the
/// first attended executor happened to be unable to see.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Fact)]
public static class AttendedGaps
{
    /// <summary>
    /// How many times round the loop went.
    /// </summary>
    /// <remarks>
    /// <c>LoopOutcome.Attempts</c> counts them from a stream this session had
    /// none of. Zero is the helpful lie: it reads as <i>it worked first time</i>.
    /// </remarks>
    public const string Turns = "turns";

    /// <summary>
    /// Which moves were reached for.
    /// </summary>
    /// <remarks>
    /// <c>LoopOutcome.MovesUsed</c>, and the most expensive of the three to
    /// invent. An empty list is FALSE for a move condition, which discharges
    /// the obligation — so every move-gate on every hand-flown flight would
    /// report satisfied by never having been measured.
    /// <c>MovesUsedIsNullNotEmptyTests</c> is the ratchet on the other side of
    /// that.
    /// </remarks>
    public const string Moves = "moves";

    /// <summary>
    /// Whether the declared move list was enforced at all.
    /// </summary>
    /// <remarks>
    /// <c>EnvironmentIdentity.MoveEnforcement</c> is measured by INVOKING the
    /// port, so probing an attended session means handing a person the canary
    /// task and waiting for them, and probing the headless executor instead
    /// measures a different session — which is the worse of the two, because it
    /// looks safe. Unknown is not none.
    /// </remarks>
    public const string MoveBound = "move-bound";

    /// <summary>Every gap that validates.</summary>
    public static IReadOnlyList<string> All { get; } = [Turns, Moves, MoveBound];
}

/// <summary>
/// A session a person flew, and what holding the terminal cost the measurement.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 3, and it is the whole subject of the fact.</b> A person held the
/// terminal, so there was no stream to read. Turns, moves used and the outcome
/// are all required on <c>ExecutorRun</c> and all three would be invented, so the
/// executor answers null and what it could not see is DECLARED here rather than
/// guessed at there. Every one of those three has an expressible, plausible,
/// silently wrong default.
/// </para>
/// <para>
/// <b>It is not a <c>loop.outcome</c> wearing a different name.</b> Nothing on it
/// says what the work did — no outcome, no moves used, no attempts, no manifest.
/// The change manifest is measured from the TREE and ships exactly as it does
/// for an agent, which is the single strongest reason this design is cheap: a
/// person editing a tree measures identically to an agent editing it, because
/// the extractor reads the tree and not the actor.
/// </para>
/// <para>
/// <b>And it names nobody.</b> Who flew it is derived from the session
/// control-plane-side, on <see cref="HumanAccount"/>'s argument and rule 8: a
/// member here would be a runner asserting an identity, which is the one thing a
/// runner may not do.
/// </para>
/// </remarks>
[FactKind(FactKinds.LoopAttended)]
[PinnedId("b1b630e9-6653-414f-aa0b-f1d17d77b898")]
public sealed record LoopAttended
{
    /// <summary>Which loop of the envelope was flown.</summary>
    public required string LoopId { get; init; }

    /// <summary>
    /// The rung the loop declared, recorded and never coerced.
    /// </summary>
    /// <remarks>
    /// <b>A person operating an agent is not a person doing the work.</b> An
    /// attended flight whose loop declares <c>frontier</c> records
    /// <c>frontier</c>: somebody sat at the terminal and an agent still did the
    /// work. Recording <c>human</c> because a person was present would make
    /// every later count of <i>how much did the machine do</i> wrong in the
    /// flattering direction, on the one measurement this product exists to be
    /// honest about — which is <see cref="ExecutorRungs.Human"/>'s own argument
    /// run in the direction that does not flatter it.
    /// </remarks>
    public required string Rung { get; init; }

    /// <summary>The agent binary the person was handed.</summary>
    public required string Binary { get; init; }

    /// <summary>
    /// What that binary answered when asked its version.
    /// </summary>
    /// <remarks>
    /// <b>Load-bearing, not decoration.</b> The runner pins no CLI version —
    /// <c>binary = "claude"</c>, whatever is on PATH — and the tool surface moved
    /// from 28 to 29 between the two versions slices twenty-six and twenty-seven
    /// measured. So <c>move-bound</c> above is a claim about a NAMED BINARY AT A
    /// NAMED VERSION, or it is a claim that expires quietly on a machine nobody
    /// upgraded on purpose.
    /// </remarks>
    public required string BinaryVersion { get; init; }

    /// <summary>What the envelope's loop allowed, in seconds.</summary>
    public required int BudgetSeconds { get; init; }

    /// <summary>
    /// How long the person actually held the terminal, in seconds.
    /// </summary>
    /// <remarks>
    /// <b>Rule 6: recorded and not enforced.</b> Nobody's terminal is killed at
    /// the envelope's wall clock, so this may exceed <see cref="BudgetSeconds"/>
    /// and that is a valid fact rather than a refused one. A validator refusing
    /// an overrun would mean the only flights able to report one are the flights
    /// that did not have one.
    /// </remarks>
    public required int HeldSeconds { get; init; }

    /// <summary>What this session could not measure. See <see cref="AttendedGaps"/>.</summary>
    public required IReadOnlyList<string> Unmeasured { get; init; }

    /// <summary>
    /// Which of the operator's settings sources were cleared for this session.
    /// </summary>
    /// <remarks>
    /// <b>Rule 10, where a reader can find it later.</b> An attended session runs
    /// with the operator's setting sources cleared and their tool servers
    /// withheld — which is the only reason the envelope's bound means anything
    /// here — and the executor says so at the terminal before the child starts.
    /// This is the same claim, kept.
    /// <para>
    /// <b>Not a closed vocabulary, deliberately.</b> These are a vendor's source
    /// names and they will move. Nothing branches on them, so a value nobody
    /// recognises costs a reader nothing — unlike a gap, where it costs
    /// everything.
    /// </para>
    /// </remarks>
    public required IReadOnlyList<string> SettingsCleared { get; init; }

    /// <summary>What is wrong with this fact, or null when nothing is.</summary>
    public static string? Validate(LoopAttended attended)
    {
        ArgumentNullException.ThrowIfNull(attended);

        if (string.IsNullOrWhiteSpace(attended.LoopId))
        {
            return "An attended session names the loop it flew.";
        }

        if (!ExecutorRungs.All.Contains(attended.Rung, StringComparer.Ordinal))
        {
            return $"Unknown rung '{attended.Rung}'. Expected one of: "
                 + string.Join(", ", ExecutorRungs.All) + ". The rung is the LOOP's own "
                 + "declaration, carried through rather than decided here.";
        }

        if (string.IsNullOrWhiteSpace(attended.Binary))
        {
            return "An attended session names the binary the person was handed.";
        }

        if (string.IsNullOrWhiteSpace(attended.BinaryVersion))
        {
            return "An attended session names the version that binary answered with. A gap "
                 + "declared against an unnamed tool surface is a claim that expires quietly: "
                 + "the surface moved from 28 tools to 29 between two versions nobody chose "
                 + "between.";
        }

        if (attended.BudgetSeconds < 0 || attended.HeldSeconds < 0)
        {
            return "Seconds are not negative. A budget or a duration below zero is a clock "
                 + "read backwards, not a short session.";
        }

        // NOT COMPARED. Held may exceed the budget - rule 6 records the wall
        // clock for an attended session and does not enforce it, and refusing
        // the overrun would leave only the flights that did not overrun able to
        // say so.
        if (attended.Unmeasured is not [_, ..])
        {
            return "An attended session declares at least one thing it could not measure. One "
                 + "declaring none measured everything, which would have shipped a loop.outcome "
                 + "and never produced this fact at all.";
        }

        if (attended.Unmeasured.FirstOrDefault(
                gap => !AttendedGaps.All.Contains(gap, StringComparer.Ordinal)) is { } unknown)
        {
            return $"Unknown gap '{unknown}'. Expected one of: "
                 + string.Join(", ", AttendedGaps.All) + ". A gap nothing downstream knows is "
                 + "an absence no reader can act on, which is worse than an undeclared one: it "
                 + "reads as having been handled.";
        }

        return attended.Unmeasured.Distinct(StringComparer.Ordinal).Count()
            != attended.Unmeasured.Count
            ? "A gap is declared once. The same absence twice is a list somebody appended to "
            + "without reading."
            : null;
    }
}
