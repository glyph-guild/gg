namespace Gg.Contracts;

/// <summary>
/// Why something did not happen, as data: a closed kind, its params, and the
/// family the kind belongs to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice eight's prose retires.</b> The waiting sentence was a string
/// built control-plane-side and pattern-matched by clients and scripts -
/// wording as protocol, undeclared. The kind is the declaration; the
/// sentence a person reads is derived from it by <see cref="Sentence"/>, one
/// grammar both repositories compile against, so no surface can reword what
/// another surface asserted.
/// </para>
/// <para>
/// <b>The family is derivable, and carried anyway.</b> A reader filtering
/// refusals from waits should not need the kind table; but a stored family
/// that disagrees with the kind is a lie one of them must be telling, so
/// <see cref="Validate"/> refuses the disagreement and <see cref="For"/>
/// builds the pair that cannot disagree.
/// </para>
/// <para>
/// <b>An unknown kind poisons, never blanks.</b> <see cref="Sentence"/> and
/// <see cref="ReasonKinds.FamilyOf"/> throw: a renderer that shrugs at a
/// kind it does not know turns a governed refusal into silence, which reads
/// as health - Article XI's exact shape, so the gap fails a build or a
/// render, never an audit.
/// </para>
/// </remarks>
[PinnedId("82ac1916-26bd-49b3-9e53-480d36b49d07")]
public sealed record Reason
{
    /// <summary>One of <see cref="ReasonFamilies"/>. Derivable from the kind, carried for the reader.</summary>
    public required string Family { get; init; }

    /// <summary>One of <see cref="ReasonKinds"/>. The closed vocabulary; the sentence derives from it.</summary>
    public required string Kind { get; init; }

    /// <summary>The facts the sentence names, in the order the grammar reads them.</summary>
    public IReadOnlyList<string> Params { get; init; } = [];

    /// <summary>Builds the pair that cannot disagree: the family is derived from the kind.</summary>
    public static Reason For(string kind, IReadOnlyList<string> parameters) => new()
    {
        Family = ReasonKinds.FamilyOf(kind),
        Kind = kind,
        Params = parameters,
    };

    /// <summary>The canonical prose for a kind - one grammar, contract-side.</summary>
    /// <remarks>
    /// The waiting sentence is byte-compatible with what every surface and
    /// script asserted before kinds existed: the kind arrives UNDER the
    /// sentence, not instead of it, so nothing a person reads moved.
    /// </remarks>
    public static string Sentence(string kind, IReadOnlyList<string> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return kind switch
        {
            ReasonKinds.NoRunnerAdvertises =>
                "waiting: no runner advertises " + string.Join(", ", parameters),

            ReasonKinds.CannotBeShownToTighten =>
                $"refused: the change moves {First(parameters)} in a way that cannot be shown "
              + "to tighten, and what cannot be shown to tighten is treated as widening - "
              + "incomparable is not neutral. A widening lands only through its gate.",

            ReasonKinds.FlightsInTheAir =>
                $"refused: '{First(parameters)}' still governs flights that are in the "
              + $"air — {Second(parameters)}. Retiring it now would leave them declared "
              + "against a work kind that no longer exists, which is a flight nobody can "
              + "finish and nobody can read the rules for. Let them land, or take them "
              + "over, and retire it after.",

            ReasonKinds.StaleWorkingCopy =>
                $"refused: this apply says it was based on {First(parameters)}, and the "
              + $"document in force is {Second(parameters)}. The stream moved after the "
              + "working copy was rendered, so applying now would overwrite a change "
              + "nobody here has read. Pull, look at what changed, and apply again.",

            ReasonKinds.WideningRequiresAGate =>
                $"refused: this change widens {First(parameters)}, and the document in force "
              + $"declares no obligation with 'when: {AttachmentConditions.Widens}' to gate "
              + "it. Declaring that obligation is itself a tightening and lands on the "
              + "owner's say-so; the widening then rides a flight to its approver.",

            ReasonKinds.DeclaredAndAbsent =>
                $"'{First(parameters)}' declares its narrowings at "
              + $"'{Second(parameters)}', and there is no such directory at the commit this "
              + "flight pinned. The layer is on and the rules cannot be read, so this flight "
              + "waits rather than being measured against rules nobody could see. Commit the "
              + "directory, or turn the declaration off - the directory often exists on a "
              + "branch and not at the commit this flight is working from.",

            ReasonKinds.ForgeUnreachable =>
                $"'{First(parameters)}' could not be reached to read its narrowings. Nothing "
              + "is wrong with what was set up: this flight cannot be evaluated because the "
              + "policy it is governed by is somewhere nobody can currently ask about. It "
              + "will be readable again when the forge is.",

            ReasonKinds.Uncharted =>
                $"refused: '{First(parameters)}' is not in the environment chart - nothing "
              + "has charted it, so no envelope may select it. Chart it first "
              + "(POST /v1/environments).",

            ReasonKinds.RegistrationIsAWidening =>
                $"refused: registering widens {First(parameters)}, and no envelope is in "
              + "force to gate it - the estate has no floor. Apply a root envelope first "
              + "(PUT /v1/envelope); its first version is the ratification and lands on "
              + "the owner's say-so.",

            ReasonKinds.BlockedByBound => Bound(parameters),

            ReasonKinds.RunnerReserved => Reserved(parameters),

            ReasonKinds.RunnerParked => Parked(parameters),

            ReasonKinds.DirectedRunnerAbsent =>
                $"waiting: this flight is for '{First(parameters)}', and that runner is not "
              + "currently asking for work. Nothing is misconfigured - a directed flight is "
              + "claimable by exactly one machine, and this one is not answering. Start it, or "
              + "re-open the flight without naming a runner.",

            ReasonKinds.PoolWarming =>
                $"the pool '{First(parameters)}' is warming toward this flight's label; "
              + "a runner advertising it clears this.",

            _ => throw new InvalidOperationException(
                $"'{kind}' is not a reason kind this build knows. A sentence cannot be "
              + "derived for it, and deriving silence instead would read as health."),
        };

        static string First(IReadOnlyList<string> parameters) =>
            parameters.Count > 0 ? parameters[0] : "(unnamed)";

        // The second half of a two-version sentence. Named rather than
        // indexed inline, so a reason that needs both cannot quietly render
        // one and leave the reader to guess which.
        static string Second(IReadOnlyList<string> parameters) =>
            parameters.Count > 1 ? parameters[1] : "(unnamed)";

        // A RESERVATION NAMES A PERSON, OR SAYS IT CANNOT. Two params is the
        // ordinary case - the label and the holder's display. One param is the
        // reservation whose holder has left the tenant: nothing releases it on
        // its own, so a runner that takes no work forever is what this
        // prevents, and "(unnamed)" here would read as a rendering bug rather
        // than as the fact it is.
        static string Reserved(IReadOnlyList<string> parameters)
        {
            var label = First(parameters);

            return parameters.Count > 1
                ? $"waiting: every runner advertising {label} is reserved to "
                + $"{parameters[1]}, so this flight is not offered to any of them. The capacity "
                + "is there and somebody is holding it - ask them, bring up a runner that is not "
                + "reserved, or have the reservation released."
                : $"waiting: every runner advertising {label} is reserved to somebody who is no "
                + "longer in this tenant. A reservation is cleared by a person and by nothing "
                + "else - there is no expiry - so this will not resolve on its own. Release it "
                + "(DELETE /v1/runners/{id}/reservation), or bring up a runner that is not "
                + "reserved.";
        }

        // A PARKING QUOTES THE REASON SOMEBODY GAVE, and copes when they gave
        // none. The reason is nullable where it is written, and a sentence that
        // required one would throw on the parking made in a hurry - turning a
        // governed wait into a failure of the surface meant to explain it.
        static string Parked(IReadOnlyList<string> parameters)
        {
            var label = First(parameters);

            return parameters.Count > 1
                ? $"waiting: every runner advertising {label} is parked - \"{parameters[1]}\". "
                + "Parked runners keep beating and take no new work; this flight moves when one "
                + "of them is unparked, or when another runner advertising that label comes up."
                : $"waiting: every runner advertising {label} is parked, with no reason recorded. "
                + "Parked runners keep beating and take no new work; this flight moves when one "
                + "of them is unparked, or when another runner advertising that label comes up.";
        }

        // The clearing is the sentence's other half - the remedy. An unknown
        // clearing THROWS, one param deeper than an unknown kind, because a
        // branch that blanked on a value nobody declared would read as
        // health. The authority clearing arrives with the first metered
        // strategy row; today it poisons here, deliberately.
        static string Bound(IReadOnlyList<string> parameters)
        {
            var bound = parameters.Count > 0 ? parameters[0] : "(unnamed)";
            var clearing = parameters.Count > 1 ? parameters[1] : "(none)";

            if (string.Equals(clearing, BoundClearings.Capacity, StringComparison.Ordinal))
            {
                return $"declined by your own bound: {bound} is reached; clears when a "
                     + "peer flight releases.";
            }

            if (string.Equals(clearing, BoundClearings.Schedule, StringComparison.Ordinal))
            {
                return parameters.Count > 2
                    ? $"declined by your own bound: outside {bound}; opens {parameters[2]}."
                    : throw new InvalidOperationException(
                        $"A schedule clearing on '{bound}' carries no eta. A wait with no "
                      + "end a reader can plan around is not a schedule - the producer "
                      + "derives the opening time from the declared hours.");
            }

            throw new InvalidOperationException(
                $"'{clearing}' is not a bound clearing this build knows. A sentence "
              + "cannot be derived for it, and deriving silence instead would read as "
              + "health.");
        }
    }

    /// <summary>Null when coherent; the diagnosis otherwise.</summary>
    public static string? Validate(Reason reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        var derived = ReasonKinds.FamilyOf(reason.Kind);
        return string.Equals(reason.Family, derived, StringComparison.Ordinal)
            ? null
            : $"A reason of kind '{reason.Kind}' claims family '{reason.Family}', and the "
            + $"kind belongs to '{derived}'. The family is derivable; a stored disagreement "
            + "is a lie one of them must be telling.";
    }
}

/// <summary>The three ways something does not happen.</summary>
/// <remarks>
/// <b>declined</b> - a person said no; <b>failed</b> - nobody said no, the
/// world cannot satisfy it right now; <b>refused</b> - the rules said no.
/// The declined family ships without a kind: its first kind arrives with the
/// producer that mints it, because a constant nothing produces is a promise
/// nobody has to keep.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class ReasonFamilies
{
    public const string Declined = "declined";

    public const string Failed = "failed";

    public const string Refused = "refused";

    public static IReadOnlyList<string> All { get; } = [Declined, Failed, Refused];
}

/// <summary>The closed kind vocabulary, and each kind's family.</summary>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class ReasonKinds
{
    /// <summary>The fleet cannot satisfy the flight's labels right now. Failed, not refused.</summary>
    public const string NoRunnerAdvertises = "no-runner-advertises";

    /// <summary>The direction comparator's refusal-shaped constant: unordered is widening.</summary>
    public const string CannotBeShownToTighten = "cannot-be-shown-to-tighten";

    /// <summary>A widening against a document that designates no gate. Absence means no.</summary>
    public const string WideningRequiresAGate = "widening-requires-a-gate";

    /// <summary>
    /// An apply whose stated precondition has been overtaken by the stream.
    /// </summary>
    /// <remarks>
    /// The working copy's refusal, and the first reason kind about a
    /// PRECONDITION rather than about a document. It names both versions
    /// because the useful question is not "are you stale" but "how far" — a
    /// person one version behind pulls and re-applies; a person eight versions
    /// behind has a colleague to talk to.
    /// </remarks>
    public const string StaleWorkingCopy = "stale-working-copy";

    /// <summary>
    /// A work kind cannot be retired while flights declared against it are
    /// still running.
    /// </summary>
    /// <remarks>
    /// The one refusal retirement needs that direction cannot supply. Retiring
    /// a narrowing is a widening and rides its gate; retiring a WORK KIND is
    /// not obviously a widening at all — removing the sets makes less possible
    /// rather than more — and its real hazard was never direction. It is
    /// orphaning a flight already declared against it.
    /// </remarks>
    public const string FlightsInTheAir = "flights-in-the-air";

    /// <summary>
    /// A repository declares narrowings and there is no such directory at the
    /// commit this flight pinned.
    /// </summary>
    /// <remarks>
    /// <b>A MISCONFIGURATION, and it is usually a sequencing one.</b> The tap
    /// was turned on before the team committed anything, or the directory
    /// exists on somebody's branch and not at the commit this flight is
    /// working from. ADR-0018 § 6: absent and <i>constrains nothing</i> must
    /// not be the same answer, because the first is a mistake and the second is
    /// a decision.
    /// </remarks>
    public const string DeclaredAndAbsent = "declared-and-absent";

    /// <summary>The forge could not be asked at all.</summary>
    /// <remarks>
    /// <b>AN OUTAGE, and nothing for the tenant to fix.</b> Kept apart from
    /// <see cref="DeclaredAndAbsent"/> deliberately: collapsing them makes a
    /// forge incident look like somebody's mistake, and sends the person having
    /// the outage to go and check a declaration that is perfectly correct.
    /// </remarks>
    public const string ForgeUnreachable = "forge-unreachable";

    /// <summary>A selection of a name the chart does not hold.</summary>
    public const string Uncharted = "uncharted";

    /// <summary>A registration with no envelope in force to gate it - the floor refusal.</summary>
    public const string RegistrationIsAWidening = "registration-is-a-widening";

    /// <summary>
    /// A bound the tenant declared binds: the flight waits, naming the bound
    /// and its clearing. THE DECLINED FAMILY'S FIRST KIND - slice ten
    /// reserved exactly this ("its first kind arrives with the producer that
    /// mints it"), and the producer is the control-plane decider, which
    /// declines by not deciding. Params: [bound, clearing] or
    /// [bound, clearing, eta]; the bound is the strategy field's own name
    /// (pool-maximum, active-hours), the clearing one of
    /// <see cref="BoundClearings"/>.
    /// </summary>
    public const string BlockedByBound = "blocked-by-bound";

    /// <summary>
    /// The pool is warming toward this flight's label. Neither a gap nor a
    /// bound - without its own kind the state would have to wear one of
    /// their clothes, and both remedies would be wrong. Params: [pool].
    /// </summary>
    public const string PoolWarming = "pool-warming";

    /// <summary>
    /// Every runner advertising a required label is reserved to somebody who is
    /// not this flight's author. Params: [label, holder] or [label].
    /// </summary>
    /// <remarks>
    /// <b>NOT A CAPABILITY GAP, and that is the whole reason it has its own
    /// kind.</b> <see cref="NoRunnerAdvertises"/> means bring a machine up; this
    /// means the machine is up, is advertising exactly what is needed, and a
    /// person is holding it. Same silence, opposite remedy — and the wrong one
    /// sends somebody to provision capacity they already own.
    /// <para>
    /// The holder is a DISPLAY and is dropped when the principal has left the
    /// tenant, which the one-param sentence says out loud. A reservation is
    /// cleared by a person and by nothing else, so a departed holder's runner
    /// takes no work until somebody notices — this is how they notice.
    /// </para>
    /// </remarks>
    public const string RunnerReserved = "runner-reserved";

    /// <summary>
    /// Every runner advertising a required label is parked. Params:
    /// [label, reason] or [label].
    /// </summary>
    /// <remarks>
    /// <b>SOMEBODY DECLARED THIS, like <see cref="DeclaredAndAbsent"/>.</b> A
    /// parked runner is beating and healthy and deliberately taking nothing, so
    /// every fleet-health surface reads correct while the flight waits. The
    /// parking reason is carried because "a runner taking nothing for a
    /// fortnight with no reason attached" is the failure mode parking most
    /// likely produces, and this sentence is where it is quoted back.
    /// </remarks>
    public const string RunnerParked = "runner-parked";

    /// <summary>
    /// A flight names a runner and that runner is not beating. Params: [runner].
    /// </summary>
    /// <remarks>
    /// <b>NOBODY DECLARED THIS, like <see cref="ForgeUnreachable"/>, and the
    /// split from the other two is the point.</b> A closed laptop is not a
    /// misconfiguration: the direction is correct, the machine is simply not
    /// asking for work. Collapsing it into the declared withholdings sends an
    /// operator off to check a configuration that is right, which is the exact
    /// mistake <see cref="ForgeUnreachable"/> exists to prevent one layer over.
    /// </remarks>
    public const string DirectedRunnerAbsent = "directed-runner-absent";

    /// <summary>Every kind, for the closed-vocabulary fingerprint.</summary>
    public static IReadOnlyList<string> All { get; } =
        [NoRunnerAdvertises, CannotBeShownToTighten, WideningRequiresAGate,
         Uncharted, RegistrationIsAWidening, BlockedByBound, PoolWarming,
         StaleWorkingCopy, FlightsInTheAir, DeclaredAndAbsent, ForgeUnreachable,
         RunnerReserved, RunnerParked, DirectedRunnerAbsent];

    /// <summary>The family a kind belongs to. Throws on a kind nobody declared.</summary>
    public static string FamilyOf(string kind) => kind switch
    {
        // A HALT IS NOT A REFUSAL. Nothing was refused - the flight was
        // admitted and cannot be evaluated - so filing these under `refused`
        // would put a flight that is WAITING in the same bucket as a document
        // somebody was told no about.
        // The three withholdings join them: a withheld flight was ADMITTED and
        // is waiting for a person, which is a wait however deliberate the
        // holding is. Filing them under `refused` would put a flight that is
        // going to run in the bucket somebody was told no in.
        NoRunnerAdvertises or PoolWarming or DeclaredAndAbsent or ForgeUnreachable
            or RunnerReserved or RunnerParked or DirectedRunnerAbsent =>
            ReasonFamilies.Failed,
        BlockedByBound => ReasonFamilies.Declined,
        CannotBeShownToTighten or WideningRequiresAGate or Uncharted
            or RegistrationIsAWidening or StaleWorkingCopy or FlightsInTheAir =>
            ReasonFamilies.Refused,
        _ => throw new InvalidOperationException(
            $"'{kind}' is not a reason kind this build knows - its family cannot be "
          + "derived, and guessing one would file a refusal under a wait."),
    };
}

/// <summary>
/// How a bound clears — the remedy half of the declined sentence.
/// </summary>
/// <remarks>
/// Closed at two, and the absence is the design: the <c>authority</c>
/// clearing (a spend ceiling with no period — somebody must decide) arrives
/// with the first metered strategy row, because docker-host meters no spend
/// and a constant nothing produces is a promise nobody has to keep.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class BoundClearings
{
    /// <summary>Clears when a peer flight releases its environment.</summary>
    public const string Capacity = "capacity";

    /// <summary>Clears when the declared hours open; the sentence carries the eta.</summary>
    public const string Schedule = "schedule";

    public static IReadOnlyList<string> All { get; } = [Capacity, Schedule];
}
