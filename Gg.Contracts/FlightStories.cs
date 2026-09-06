namespace Gg.Contracts;

/// <summary>One thing that happened to a flight.</summary>
/// <remarks>
/// <b>A kind, its params, and prose kept apart from both.</b> The kind fixes what
/// the params mean and <see cref="FlightStory.Sentence"/> is the only place they
/// are worded. Prose a person or an agent actually wrote goes in
/// <see cref="Said"/> instead: interpolating a diagnosis into a grammar is how a
/// diagnosis becomes a guess.
/// </remarks>
[PinnedId("9418db18-da80-49d1-b1e3-5373426f9307")]
public sealed record StoryEntry
{
    /// <summary>When, by the clock of whatever recorded it.</summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>One of <see cref="StoryKinds"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>
    /// The stage this belongs to, or null when it belongs to none.
    /// </summary>
    /// <remarks>
    /// Derivable through <see cref="FlightStages.Of"/> and carried for a reader
    /// that groups by it. Null is a real answer: a takeover interrupts at any
    /// stage.
    /// </remarks>
    public string? Stage { get; init; }

    /// <summary>The facts the sentence names, in the order the grammar reads them.</summary>
    /// <remarks>
    /// <b>The accessor delivers that, and the initializer does not.</b> This
    /// member is init-only, so System.Text.Json builds the object through a
    /// creator that assigns every member from an argument array — this one as
    /// null when the key is absent, overwriting the <c>= []</c>.
    /// <c>AbsentCollectionsSurviveTheWireTests</c> holds it for the whole
    /// contract.
    /// </remarks>
    public IReadOnlyList<string> Params
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>Which attempt this belongs to, where the record says.</summary>
    /// <remarks>
    /// Null and never zero: a flight sent back gets a second pass at a new
    /// generation, and an entry from a record that never carried one is absent
    /// rather than first.
    /// </remarks>
    public int? Attempt { get; init; }

    /// <summary>Who did it.</summary>
    public Actor? Actor { get; init; }

    /// <summary>
    /// Prose somebody actually wrote, rendered whole and never interpolated.
    /// </summary>
    /// <remarks>
    /// A halt's diagnosis, a takeover note, a rejection's reason, a runner's own
    /// account of how its turn ended. It is not a parameter and must not become
    /// one: a grammar that swallowed it would be this platform paraphrasing a
    /// person.
    /// </remarks>
    public string? Said { get; init; }
}

/// <summary>
/// A flight's whole story: how far it got, how it stands, and everything that
/// happened to it.
/// </summary>
/// <remarks>
/// <para>
/// <b>One read, because the question is one question.</b> "What happened to this
/// flight, and where is it now" was spread across five routes — the summary, the
/// log, the attribution, the checklist and the seed — and a person had to know
/// which held which half.
/// </para>
/// <para>
/// <b>Composed by folding, never stored.</b> Rule 5: <i>"Folding what happened is
/// event sourcing working."</i> A story is a history, so there is no read model
/// missing here; and a person reads one BECAUSE the flight just stopped, which is
/// exactly when a lagging perspective would answer about the flight before the
/// thing they are reading about.
/// </para>
/// </remarks>
[PinnedId("b4069e6c-8210-4f49-9395-7cc730453d7f")]
public sealed record FlightStory
{
    public required string FlightId { get; init; }

    /// <summary>What a person types. <c>GG-42</c>.</summary>
    public required string FlightNumber { get; init; }

    /// <summary>What sort of work this is, when the record says.</summary>
    public string? WorkKind { get; init; }

    /// <summary>
    /// How far it got: one of <see cref="FlightStages"/>.
    /// </summary>
    /// <remarks>
    /// Derived from <see cref="Entries"/> and carried anyway, so a reader does
    /// not need the kind table. <see cref="Validate"/> refuses a disagreement.
    /// </remarks>
    public required string Stage { get; init; }

    /// <summary>
    /// What became of it: one of <see cref="FlightStates"/>, or the reading
    /// <c>open</c>.
    /// </summary>
    /// <remarks>
    /// <b>A different axis from <see cref="Stage"/>, and collapsing them is the
    /// mistake this shape exists to prevent.</b> A halted flight stopped at
    /// <c>evaluated</c> and is <c>open</c>: stopping is not the same as being
    /// over.
    /// </remarks>
    public required string State { get; init; }

    /// <summary>Why it cannot start, when it cannot. Null means it is not waiting.</summary>
    public Reason? Waiting { get; init; }

    /// <summary>Who has it right now — a person holding it, or a runner leasing it.</summary>
    /// <remarks>
    /// Null means nobody. Without this the story can say a person took the flight
    /// over an hour ago and cannot say whether they still have it, which is the
    /// ambiguity the takeover routes exist to remove.
    /// </remarks>
    public Actor? HeldBy { get; init; }

    /// <summary>Until when, where a hold or a lease has an expiry.</summary>
    public DateTimeOffset? HeldUntil { get; init; }

    /// <summary>What is unanswered.</summary>
    /// <remarks>
    /// <b>Selected from <see cref="Entries"/>, never derived a second time.</b>
    /// Two answers to "what needs a person" that could disagree is what this
    /// slice is about.
    /// </remarks>
    public IReadOnlyList<StoryEntry> Outstanding
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>Everything that happened, oldest first.</summary>
    public IReadOnlyList<StoryEntry> Entries
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>The canonical prose for a kind — one grammar, contract-side.</summary>
    /// <remarks>
    /// <b>An unknown kind throws.</b> A renderer that shrugs at a kind it does not
    /// know turns a governed record into silence, and silence reads as health.
    /// Missing params do NOT throw: a record written by an older writer is an
    /// older record, not a broken one.
    /// </remarks>
    public static string Sentence(string kind, IReadOnlyList<string> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return kind switch
        {
            StoryKinds.Created => $"opened by {At(parameters, 0, "somebody")}",
            StoryKinds.OpenedByAdmission =>
                $"opened by admission as {At(parameters, 0, "work")}, on another flight's "
              + "classification",

            StoryKinds.LeaseRefused =>
                $"a claim was refused: {At(parameters, 0, "no reason recorded")}",
            StoryKinds.CredentialUnresolved =>
                $"a credential could not be resolved: {At(parameters, 0, "unnamed")}",

            StoryKinds.LeaseGranted => $"handed to runner {At(parameters, 0, "unnamed")}",
            StoryKinds.LeaseRenewed => "the runner renewed its claim",
            StoryKinds.LeaseReleased =>
                $"the runner let it go, reporting {At(parameters, 0, "nothing")}",
            StoryKinds.LeaseExpired =>
                $"runner {At(parameters, 0, "unnamed")} stopped talking to us and its claim ran out",
            StoryKinds.LeaseAbandoned =>
                $"granted to runner {At(parameters, 0, "unnamed")} and never collected",

            StoryKinds.LoopRan =>
                $"the {At(parameters, 1, "loop")} loop ran and ended {At(parameters, 0, "somehow")}",
            StoryKinds.LoopAsked => "the agent asked for a decision it is not allowed to make",

            StoryKinds.Evaluated =>
                $"its rules were checked: {At(parameters, 0, "0")} applied, "
              + $"{At(parameters, 1, "0")} satisfied",
            StoryKinds.EvidenceRejected =>
                $"something it shipped was refused: {At(parameters, 0, "unnamed")}",
            StoryKinds.ObligationHalted =>
                $"stopped on {At(parameters, 0, "a rule")}, which could not be answered either way",
            StoryKinds.LoopWaitingForPerson => "it now needs a person",

            StoryKinds.DecisionAsked =>
                $"{At(parameters, 0, "a rule")} needs {At(parameters, 1, "somebody")}",
            StoryKinds.DecisionMade =>
                $"{At(parameters, 0, "a rule")} was answered {At(parameters, 1, "somehow")}",

            StoryKinds.Ended => $"it ended {At(parameters, 0, "somehow")}",

            StoryKinds.TakenOver => $"{At(parameters, 0, "somebody")} took it over",
            StoryKinds.HoldExpired => $"{At(parameters, 0, "somebody")}'s hold lapsed",
            StoryKinds.PoolIncident =>
                $"the pool it came from had trouble: {At(parameters, 0, "unnamed")}",

            _ => throw new InvalidOperationException(
                $"'{kind}' is not a story kind this build knows. A sentence cannot be derived "
              + "for it, and deriving silence instead would read as health."),
        };

        // AN OLDER RECORD IS NOT A BROKEN ONE. A writer that shipped fewer params
        // than this build reads is a flight from before somebody added one, and
        // throwing would make one absent value cost the whole story.
        static string At(IReadOnlyList<string> parameters, int index, string absent) =>
            index < parameters.Count && parameters[index] is { Length: > 0 } value
                ? value
                : absent;
    }

    /// <summary>The diagnosis, or null when nothing is wrong.</summary>
    public static string? Validate(FlightStory story)
    {
        ArgumentNullException.ThrowIfNull(story);

        if (!FlightStages.All.Contains(story.Stage, StringComparer.Ordinal))
        {
            return $"'{story.Stage}' is not a stage. Expected one of: "
                 + string.Join(", ", FlightStages.All) + ".";
        }

        // ONE OF THEM IS LYING AND THIS DOES NOT GUESS WHICH. Carrying a
        // derivable value spares a reader the kind table; carrying a wrong one is
        // worse than making them derive it.
        var reached = FlightStoryStages.Reached(story.Entries);
        if (!string.Equals(story.Stage, reached, StringComparison.Ordinal))
        {
            return $"this story says it reached '{story.Stage}' and its entries reach "
                 + $"'{reached}'. A carried stage that disagrees with the narrative under it "
                 + "is a lie one of them must be telling.";
        }

        foreach (var entry in story.Entries)
        {
            if (!StoryKinds.All.Contains(entry.Kind, StringComparer.Ordinal))
            {
                return $"'{entry.Kind}' is not a story kind. Expected one of: "
                     + string.Join(", ", StoryKinds.All) + ".";
            }

            // A RUNNER REPORTS ON ITS TURN AND NEVER ON THE FLIGHT. Two of the
            // four endings are not a machine's to report at all.
            if (StoryKinds.Ends(entry.Kind)
                && entry.Actor is { Kind: ActorKinds.Runner })
            {
                return "a runner reported how this flight ended. A machine says what became "
                     + "of its own turn; what became of the flight is the platform's to "
                     + "decide, and two of the four endings are not a machine's at all.";
            }
        }

        return null;
    }
}
