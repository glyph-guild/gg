namespace Gg.Contracts;

/// <summary>
/// The two rules a nominator's bound and its budget are held to, in one place.
/// </summary>
/// <remarks>
/// <para>
/// <b>ONE SPELLING, and it is the reason this is a class rather than a habit.</b>
/// ADR-0022 § 5: <i>"A second menu shaped like `opens:` would have its own
/// validation, its own direction arms, and no way for a narrowing to reach it —
/// which is how one of two spellings stops agreeing."</i> Slice thirty-eight
/// wrote these rules inside <see cref="RegisterRepositoryRequest.Validate"/>;
/// slice thirty-nine needed them at a second door, and copying them there would
/// have been exactly the failure that sentence describes.
/// </para>
/// <para>
/// <b>The leaves are shared and the composition is not.</b> Whether a budget may
/// stand without a bound is a different answer at each door — a repository's
/// bound is optional, so a budget without one is a limit on an act nothing
/// declared; a watch's bound is required, so the case cannot arise. Each caller
/// composes; only what is genuinely one rule lives here.
/// </para>
/// <para>
/// <b>The sentences name no noun</b>, which is what lets both doors return them
/// unchanged. <c>AWatchsBoundIsADestinationTests</c> asserts the two doors
/// answer with the same string for the same malformed bound, and a sentence
/// saying "this repository" could not have passed that.
/// </para>
/// </remarks>
public static class NominationBounds
{
    /// <summary>Why this bound cannot govern a nomination, or null.</summary>
    public static string? Invalid(Destination bound)
    {
        ArgumentNullException.ThrowIfNull(bound);

        // ONLY A FLIGHT NOMINATES, which is `opens:`' rule at the same door. A
        // bound of any other kind governs nothing and reads to whoever wrote it
        // as a control they set.
        if (!string.Equals(bound.Kind, DestinationKinds.Flight, StringComparison.Ordinal))
        {
            return $"A nomination bound is a '{bound.Kind}' destination, and only a "
                 + $"'{DestinationKinds.Flight}' opens anything. A bound of any other kind is "
                 + "a control that governs nothing.";
        }

        return DestinationOpening.Refused(bound);
    }

    /// <summary>Why this budget bounds nothing, or null.</summary>
    public static string? Invalid(NominationBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);

        // ZERO AND ABSENT MUST NOT BE THE SAME VALUE. Absent is unbounded; zero
        // would be a nominator permitted nothing at all, which somebody might
        // mean and must therefore not be reachable by leaving a number out or
        // by typing the wrong one.
        if (budget.Flights <= 0)
        {
            return $"A budget of {budget.Flights} flights is not a budget. It is a number of "
                 + "them greater than none, and leaving the budget out is how a nominator "
                 + "draws on nothing.";
        }

        return !EnvelopeDurations.TryParse(budget.Window, out var window) || window <= TimeSpan.Zero
            ? $"A budget window of '{budget.Window}' is not a duration this reads. Whole "
              + "seconds, minutes or hours - 30m, 24h."
            : null;
    }
}

/// <summary>
/// What a sweep looks at. Closed at one, and the reason is a missing server.
/// </summary>
/// <remarks>
/// <b>The gap is named rather than papered over.</b> ADR-0023's consequences:
/// <i>"`tracker` and `gg` are the servers that exist. … Checking pull requests
/// from a runner needs a forge reader server that does not exist — pull-request
/// observation is control-plane today — and this ADR names that gap rather than
/// papering it."</i> So a forge shape is absent because nothing could serve it,
/// and slice thirty-eight's control-plane path stays where it is.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class WatchShapes
{
    /// <summary>A tracker query — the one shape both servers can already reach.</summary>
    public const string WorkItems = "work-items";

    public static IReadOnlyList<string> All { get; } = [WorkItems];
}

/// <summary>When a sweep runs.</summary>
/// <remarks>
/// <b>Its own member rather than one of the bounds, which is ADR-0022 § 5's own
/// split.</b> A watch declares <i>"its trigger — periodic, or on an event where
/// the shape offers one"</i> and, separately, its bounds. Periodic is the only
/// form this version reads; an event trigger needs a shape that offers one, and
/// the one shape that exists does not.
/// </remarks>
[PinnedId("2a6e9f04-8c51-4d7b-b3e2-71f0a94c6d18")]
public sealed record WatchTrigger
{
    /// <summary>How often, as a duration — <c>15m</c>, <c>1h</c>.</summary>
    public required string Every { get; init; }
}

/// <summary>
/// What the shape reported, as the three members a nomination can take from it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three fields, and the exclusions are the decision.</b> A nomination
/// carries more than three members and the rest are not the mapping's to fill:
/// the work kind comes from the bound's <c>opens:</c>, the mode from its
/// <c>opens-as</c>, and the reason and the note are what the executor writes.
/// </para>
/// <para>
/// <b><c>environment</c> and <c>repository</c> are deliberately absent</b>,
/// although the bound carries a <c>may-select</c>. Nothing on this path
/// selects — there is no classifier in front of a watch — so a mapping that
/// could propose one would be a selection nobody made. A watch that needs to
/// select wants a fourth field with its own argument, not a quiet fourth key.
/// </para>
/// </remarks>
[PinnedId("7b1c4d82-e396-4a05-9f6d-3c28e5b70a41")]
public sealed record WatchMapping
{
    /// <summary>What it is about: the identity the shape reports.</summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Which version of it, and this is what makes the dedupe work.
    /// </summary>
    /// <remarks>
    /// <c>(watch, subject@version)</c> is first-writer-wins, so a field that
    /// moved on every read would be a nomination per sweep, and one that never
    /// moved would be a subject nobody could nominate twice after it changed.
    /// </remarks>
    public required string Version { get; init; }

    /// <summary>What names it outside gg, which is what the budget groups by.</summary>
    public required string IntentKey { get; init; }
}

/// <summary>What a watch's sweeps may spend.</summary>
/// <remarks>
/// <para>
/// <b>Rate and budget are one number, not two that can disagree.</b> ADR-0022
/// § 5 lists <i>"rate, active hours, a cap per pass, and the budget"</i> — and
/// a budget is already flights-per-window, which is a rate. Declaring both
/// would be two ceilings on one act, and the first time they differed nobody
/// could say which held.
/// </para>
/// <para>
/// <b>Every member is optional and absent means unbounded</b>, on
/// <c>StrategyBounds</c>' shape: managing happens inside whatever was declared,
/// and a watch that declared nothing is one nobody has bounded yet.
/// </para>
/// </remarks>
[PinnedId("c58f30a6-1d47-4b92-8e03-6a9f2d41b7e5")]
public sealed record WatchBounds
{
    /// <summary>When it may sweep at all, as <c>HH:MM-HH:MMZ</c>, or null for always.</summary>
    public string? ActiveHours { get; init; }

    /// <summary>The most subjects one sweep may report, or null for all of them.</summary>
    public int? CapPerPass { get; init; }

    /// <summary>What its nominations draw on, or null for unbounded.</summary>
    public NominationBudget? Budget { get; init; }
}

/// <summary>
/// A watch: what to sweep, how often, under what bound, and who performs it.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0022 § 5's document.</b> A tenant-scoped, named, versioned airspace
/// object — to nominations what an environment strategy is to pools. It names a
/// shape, a host and credential <i>reference</i>, a filter, a skill at a pinned
/// ref, a mapping, its bound and its bounds.
/// </para>
/// <para>
/// <b>The document never contains a procedure.</b> ADR-0015 § 1, narrowed to the
/// document: <see cref="Skill"/> is a path and <see cref="Ref"/> is where to
/// read it, so git holds the words and <c>CODEOWNERS</c> review them. There is
/// no member a script can travel in, and <c>AWatchDeclaresReferencesTests</c>
/// asserts that structurally rather than trusting it.
/// </para>
/// <para>
/// <b>And <see cref="Nominates"/> is required, where a repository's bound is
/// not.</b> The one place the two nominators' doors differ, and deliberately: a
/// repository existed before the member did, so absent had to keep meaning
/// today's behaviour. A watch does not exist without its bound — nothing else
/// tells a sweep what kind to nominate, so one without it would sweep and leave
/// rows nothing could open.
/// </para>
/// </remarks>
[PinnedId("f4d8a17c-5b92-4e63-9a0f-2c7d1e8b6034")]
public sealed record WatchDocument
{
    /// <summary>One of <see cref="WatchShapes"/>.</summary>
    public required string Shape { get; init; }

    public required WatchTrigger Trigger { get; init; }

    /// <summary>Which system of record, by reference — never a credential.</summary>
    public required string Host { get; init; }

    /// <summary>
    /// Where the credential is, and never what it is.
    /// </summary>
    /// <remarks>
    /// The boundary this product is built on: the runner resolves the secret
    /// into the tool server's environment, so neither this document nor either
    /// executor ever holds it.
    /// </remarks>
    public required string Credential { get; init; }

    /// <summary>What to ask the shape for. Blank is refused, never read as everything.</summary>
    public required string Filter { get; init; }

    /// <summary>
    /// The registered repository <see cref="Skill"/> is a path in, by its
    /// registry name.
    /// </summary>
    /// <remarks>
    /// <b>A reference like the others, and the one the skill could not do
    /// without.</b> The control plane fetches the skill at <see cref="Ref"/>
    /// (ADR-0018 § 5, one noun over), so it has to know whose tree to read -
    /// and a registered repository is one somebody declared through a gate,
    /// with a forge binding to reach it by.
    /// </remarks>
    public required string Repository { get; init; }

    /// <summary>The skill the instructions executor follows, as a path in <see cref="Repository"/>.</summary>
    public required string Skill { get; init; }

    /// <summary>The ref that path is read at, so what ran is what somebody reviewed.</summary>
    public required string Ref { get; init; }

    public required WatchMapping Mapping { get; init; }

    /// <summary>One of <see cref="PullPoints"/>, and all three are legal here.</summary>
    public required string PullPoint { get; init; }

    /// <summary>The `flight` destination its nominations stand under. Required.</summary>
    public Destination? Nominates { get; init; }

    /// <summary>What those nominations may spend, or null for unbounded.</summary>
    public WatchBounds? Bounds { get; init; }

    /// <summary>
    /// The schema's own rule, shared so gg and the control plane cannot
    /// disagree about what a valid watch is. Null means valid; anything else is
    /// the refusal, Article XI-shaped.
    /// </summary>
    public static string? Validate(WatchDocument watch)
    {
        ArgumentNullException.ThrowIfNull(watch);

        if (!WatchShapes.All.Contains(watch.Shape, StringComparer.Ordinal))
        {
            return $"'{watch.Shape}' is not a shape this version can sweep. Expected one of: "
                 + $"{string.Join(", ", WatchShapes.All)}.";
        }

        if (!EnvelopeDurations.TryParse(watch.Trigger.Every, out var every)
            || every <= TimeSpan.Zero)
        {
            return $"This watch sweeps every '{watch.Trigger.Every}', which is not a duration "
                 + "this reads. Whole seconds, minutes or hours - 15m, 1h. A period that "
                 + "parses into nothing is a watch that reads as scheduled and sweeps never.";
        }

        // EMPTY AND ALL MUST NOT BE THE SAME VALUE. A blank filter reading as
        // "every subject the shape has" is the loop this slice's budget exists
        // to bound, arriving by omission rather than by anybody asking for it.
        if (string.IsNullOrWhiteSpace(watch.Filter))
        {
            return "This watch declares no filter. A blank one would read as every subject the "
                 + "shape has, which is a sweep nobody asked for - say what to look for.";
        }

        if (string.IsNullOrWhiteSpace(watch.Host) || string.IsNullOrWhiteSpace(watch.Credential))
        {
            return "This watch names no host or no credential to reach it with. Both are "
                 + "references - the runner resolves the secret, and this document never "
                 + "holds it - but a reference to nothing reaches nothing.";
        }

        if (string.IsNullOrWhiteSpace(watch.Repository))
        {
            return "This watch names no repository. Its skill is a path, and a path in no "
                 + "repository is a path in nothing - name the registered repository the "
                 + "skill is in.";
        }

        if (string.IsNullOrWhiteSpace(watch.Skill) || string.IsNullOrWhiteSpace(watch.Ref))
        {
            return "This watch names no skill, or no ref to read it at. The instructions "
                 + "executor follows a skill in the repository, and a path without a ref is a "
                 + "procedure whose words can change after somebody reviewed them.";
        }

        if (string.IsNullOrWhiteSpace(watch.Mapping.Subject)
            || string.IsNullOrWhiteSpace(watch.Mapping.Version)
            || string.IsNullOrWhiteSpace(watch.Mapping.IntentKey))
        {
            return "This watch's mapping leaves one of subject, version or intent-key unsaid. "
                 + "A nomination without a subject is not a nomination, and finding that out "
                 + "at sweep time means a watch that validated, applied, ran and produced "
                 + "nothing anybody can act on.";
        }

        if (string.IsNullOrWhiteSpace(watch.PullPoint)
            || !PullPoints.All.Contains(watch.PullPoint, StringComparer.Ordinal))
        {
            return $"This watch is performed by '{watch.PullPoint}', which is not a pull point "
                 + $"this version knows. Expected one of: {string.Join(", ", PullPoints.All)}. "
                 + "A watch nobody can perform is a control that reads as running.";
        }

        // REQUIRED, and the sentence says what makes it different from a
        // repository's. `required` on the member would have been the obvious
        // spelling and is the wrong one: a document read from JSON that omits
        // the key gets null whatever the initializer says, so the refusal has
        // to be here to be a refusal at all.
        if (watch.Nominates is not { } bound)
        {
            return "This watch declares nothing to nominate. A repository with no bound "
                 + "nominates what it opens today; a watch has no today - nothing else tells "
                 + "a sweep what kind to open, so one without a bound would sweep and leave "
                 + "rows nothing can open.";
        }

        if (NominationBounds.Invalid(bound) is { } wrong)
        {
            return wrong;
        }

        if (watch.Bounds?.Budget is { } budget
            && NominationBounds.Invalid(budget) is { } unspendable)
        {
            return unspendable;
        }

        return watch.Bounds?.ActiveHours is { } hours
            && EnvironmentStrategy.ParseActiveHours(hours) is null
            ? $"This watch's active hours are '{hours}', which is not a window this reads. "
              + "HH:MM-HH:MMZ, in UTC."
            : null;
    }
}

/// <summary>A watch in force: the document, and which version of it.</summary>
/// <remarks>
/// <b><c>EnvironmentStrategyState</c>'s shape with one member renamed</b>, so a
/// reader that holds one already knows how to hold the other. The version is
/// what a <c>based-on</c> precondition names, which is the reason a watch rides
/// the envelope stream rather than a table of its own.
/// </remarks>
[PinnedId("9e2b7d41-6c08-4f35-a1d9-3b54e87f0c62")]
public sealed record WatchState
{
    /// <summary>The topology name the watch was applied to.</summary>
    public required string Name { get; init; }

    /// <summary>The per-name version in force, e.g. v2.</summary>
    public required string Version { get; init; }

    public required DateTimeOffset AppliedAt { get; init; }

    public required WatchDocument Watch { get; init; }
}

/// <summary>
/// How a watch is doing: what would run it, what it last said, and what it has
/// spent.
/// </summary>
/// <remarks>
/// <para>
/// <b>A separate read from <see cref="WatchState"/>, and the estate is why.</b>
/// <c>gg airspace pull</c> writes a working copy from the watches in force, and
/// a working copy exists to be diffed against what somebody wrote. Liveness
/// members on that type would put timestamps in those files and change them on
/// every pull - so what is in force and how it is going are two reads.
/// </para>
/// <para>
/// <b>Every member is nullable or counted, because a watch that has never
/// swept is an ordinary state.</b> A watch applied a minute ago has no
/// executor in force, said nothing, and spent nothing; a reader has to be able
/// to tell that from a watch that has gone quiet, which is what
/// <see cref="QuietSince"/> is for.
/// </para>
/// </remarks>
[PinnedId("2a6f91c4-7b3e-4d58-9c02-e85d1a43f6b7")]
public sealed record WatchStanding
{
    /// <summary>The topology name.</summary>
    public required string Name { get; init; }

    /// <summary>The watch version in force.</summary>
    public required string Version { get; init; }

    /// <summary>
    /// What would run this watch's next sweep, as its newest decided sweep
    /// declared - or null for a watch that has never had one.
    /// </summary>
    /// <remarks>
    /// <b>Reported rather than defaulted.</b> Every sweep today carries
    /// <c>instructions</c> and ADR-0023's script executor arrives later; a
    /// member that answered the shipped word by construction would be a
    /// surface performing a value instead of reading one, and would be wrong
    /// the day the second executor lands without anything changing.
    /// </remarks>
    public string? Executor { get; init; }

    /// <summary>When this watch last reported, on the control plane's clock.</summary>
    public DateTimeOffset? LastHeardAt { get; init; }

    /// <summary>The newest report's outcome, one of <see cref="WatchOutcomes"/>, or null.</summary>
    public string? Outcome { get; init; }

    /// <summary>How many nominations the newest report carried.</summary>
    public int Nominated { get; init; }

    /// <summary>Why the newest sweep could not do its job, when it could not.</summary>
    public string? Diagnosis { get; init; }

    /// <summary>
    /// When this watch's next sweep is due, or null when none is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Said here because nobody else can say it.</b> The schedule is timed
    /// between two DECISIONS - so a runner's pace never stretches it and its
    /// clock never shortens it - and it is held back by a latch while a sweep
    /// has not reported, and by the watch's active hours. A reader holding only
    /// <see cref="LastHeardAt"/> would compute a time that disagrees with the
    /// planner exactly when somebody is asking why nothing has run.
    /// </para>
    /// <para>
    /// <b>In the past means due and waiting to be pulled</b>, which is a real
    /// state rather than a stale number: the control plane has decided nothing
    /// is stopping it and no runner has taken it yet.
    /// </para>
    /// </remarks>
    public DateTimeOffset? NextSweepAt { get; init; }

    /// <summary>
    /// Why there is no next sweep, when there is none, in the planner's words.
    /// </summary>
    /// <remarks>
    /// <b>A time and a reason are different answers, and a reader needs to tell
    /// them apart.</b> Null with a <see cref="NextSweepAt"/> is the ordinary
    /// case; a reason with no time is a watch nothing will ever pull - an
    /// unperformed pull point, a period that does not read as a duration - and
    /// showing a blank there would read as "soon".
    /// </remarks>
    public string? NextSweepSaid { get; init; }

    /// <summary>
    /// The instant this watch has said nothing since, when that is longer than
    /// twice its period.
    /// </summary>
    /// <remarks>
    /// <b>Null is the healthy answer, and it is not the same as
    /// <see cref="LastHeardAt"/> being recent.</b> A watch that has never swept
    /// at all has no last-heard and can still be quiet - measured from when it
    /// was applied, because that is when the board started expecting to hear
    /// from it.
    /// </remarks>
    public DateTimeOffset? QuietSince { get; init; }

    /// <summary>
    /// How many flights this watch's nominations have opened inside its budget
    /// window - or inside <see cref="DefaultCostWindow"/> when it has no
    /// budget.
    /// </summary>
    public int Opened { get; init; }

    /// <summary>
    /// The window <see cref="Opened"/> was counted over, as a duration.
    /// </summary>
    /// <remarks>
    /// Carried so a reader sees the scale rather than a bare number: "3 in 24h"
    /// says something and "3" does not.
    /// </remarks>
    public required string Window { get; init; }

    /// <summary>
    /// What the watch budgeted for that window, or null for unbounded.
    /// </summary>
    /// <remarks>
    /// <b>Unbounded is a state, not an absence</b>, and it is what every watch
    /// written before a budget existed says - so the count is reported either
    /// way and only the bound goes missing.
    /// </remarks>
    public int? Budgeted { get; init; }

    /// <summary>
    /// The window a watch with no budget's cost is counted over.
    /// </summary>
    /// <remarks>
    /// <b>A day, because the number is for a person rather than for a rule.</b>
    /// Nothing decides anything from it: the budget's own window is used
    /// wherever there is one, and this is only so a watch without one still
    /// answers "how much has this been costing".
    /// </remarks>
    public const string DefaultCostWindow = "24h";
}

/// <summary>How every watch in force is doing.</summary>
/// <remarks>
/// An envelope rather than a bare array, for <see cref="WatchList"/>'s reason.
/// </remarks>
[PinnedId("58c3e07a-9d41-4b26-8f95-71a0d6c2e438")]
public sealed record WatchStandingList
{
    public required IReadOnlyList<WatchStanding> Standings { get; init; }
}

/// <summary>Every watch in force for the tenant.</summary>
/// <remarks>
/// An envelope rather than a bare array, for the reason <c>StrategyList</c> is
/// one: a bare array has nowhere to put the paging this will grow. Empty is a
/// tenant watching nothing, which is a state and not an error.
/// </remarks>
[PinnedId("4f71a8c3-2e59-4d06-b8a7-c10d95e36b24")]
public sealed record WatchList
{
    public required IReadOnlyList<WatchState> Watches { get; init; }
}
