namespace Gg.Contracts;

/// <summary>Who did a thing: a person, a machine, or this platform.</summary>
/// <remarks>
/// <para>
/// <b>The second spine of the reference document, made a field.</b> A runner
/// says what became of its own turn; the platform decides what that means for
/// the flight — <i>"a machine reporting on its own work should not be the thing
/// that decides the work was accepted."</i> Today that distinction is only
/// recoverable by knowing which of five stores a row came from. Here it is on
/// the entry, so <c>StoryActorBoundTests</c> can hold it rather than a reader
/// remembering it.
/// </para>
/// <para>
/// <b>And it is what lets the story begin where the flight began.</b> The flight
/// log omits <c>FlightCreated</c> unless admission opened it, because <i>"an
/// entry for all of them would tell a person who opened a flight themselves that
/// the control plane had - a false record rather than a missing one."</i> That is
/// true of a surface with no actor. With one, a person opening a flight and
/// admission opening one are two different entries.
/// </para>
/// </remarks>
[PinnedId("8ddd4c78-8b82-4be1-885c-6f08367976a6")]
public sealed record Actor
{
    /// <summary>One of <see cref="ActorKinds"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Who, as a reader would recognise them.
    /// </summary>
    /// <remarks>
    /// A display name for a person, a runner id for a machine, and the platform's
    /// own name for the platform. Never an opaque id alone: an actor a reader
    /// cannot recognise is an attribution that does not attribute.
    /// </remarks>
    public required string Name { get; init; }
}

/// <summary>The kinds of thing that can act on a flight.</summary>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class ActorKinds
{
    /// <summary>Somebody signed in.</summary>
    public const string Person = "person";

    /// <summary>A runner, acting on its own turn and never beyond it.</summary>
    public const string Runner = "runner";

    /// <summary>The control plane itself — a sweep, a timer, admission.</summary>
    public const string Platform = "platform";

    public static IReadOnlyList<string> All { get; } = [Person, Runner, Platform];
}

/// <summary>
/// The six stages a flight moves through.
/// </summary>
/// <remarks>
/// <b>The boxes are stages, not the flight's state.</b> How far a flight got and
/// what became of it are different axes: a halted flight stopped at
/// <see cref="Evaluated"/> and is <c>open</c>, and a flight can be `open` at
/// every one of these. <see cref="FlightStates"/> is the other axis and the two
/// must never be collapsed — that collapse is how a stopped flight comes to read
/// as a finished one.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class FlightStages
{
    /// <summary>It exists, and its rules were fixed.</summary>
    public const string Created = "created";

    /// <summary>It is up for grabs.</summary>
    public const string Ready = "ready";

    /// <summary>Exactly one machine holds it.</summary>
    public const string Leased = "leased";

    /// <summary>An agent, or a person, worked on it.</summary>
    public const string Worked = "worked";

    /// <summary>Its rules were checked against what happened.</summary>
    public const string Evaluated = "evaluated";

    /// <summary>Something became of it.</summary>
    public const string Ended = "ended";

    /// <summary>In order, because "furthest reached" is a comparison.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Created, Ready, Leased, Worked, Evaluated, Ended];

    /// <summary>
    /// The stage an entry of this kind belongs to, or null when it belongs to
    /// none.
    /// </summary>
    /// <remarks>
    /// <b>Null is a real answer and the important one.</b> A takeover, a lapsed
    /// hold and a pool's maintenance storm interrupt at any stage; assigning one
    /// of the six would invent a reading, and reading a stage off one would read
    /// it off the wrong thing. Every other kind must decide, and
    /// <c>StoryStageTests</c> sweeps the vocabulary so a kind added later cannot
    /// default quietly.
    /// </remarks>
    public static string? Of(string kind) => kind switch
    {
        StoryKinds.Created or StoryKinds.OpenedByAdmission => Created,

        StoryKinds.LeaseRefused or StoryKinds.CredentialUnresolved => Ready,

        StoryKinds.LeaseGranted or StoryKinds.LeaseRenewed or StoryKinds.LeaseReleased
            or StoryKinds.LeaseExpired or StoryKinds.LeaseAbandoned => Leased,

        StoryKinds.LoopRan or StoryKinds.LoopAsked => Worked,

        StoryKinds.Evaluated or StoryKinds.EvidenceRejected or StoryKinds.ObligationHalted
            or StoryKinds.LoopWaitingForPerson or StoryKinds.DecisionAsked
            or StoryKinds.DecisionMade => Evaluated,

        StoryKinds.Ended => Ended,

        // NOT A GAP. These interrupt at any stage and belong to none of the six.
        StoryKinds.TakenOver or StoryKinds.HoldExpired or StoryKinds.PoolIncident => null,

        _ => throw new InvalidOperationException(
            $"'{kind}' is not a story kind this build knows, so the stage it belongs to "
          + "cannot be decided - and picking one would put an entry in a box nobody chose."),
    };
}

/// <summary>Reading the stage a flight reached off its own narrative.</summary>
public static class FlightStoryStages
{
    /// <summary>
    /// The furthest stage any entry belongs to.
    /// </summary>
    /// <remarks>
    /// <b>Pure, so both repositories compute one answer.</b> The control plane
    /// calls this and puts the result on the wire; a client reads the member
    /// rather than recomputing it — and <see cref="FlightStory.Validate"/>
    /// refuses a story where the two disagree, because a carried value that
    /// contradicts what it was derived from is a lie one of them must be telling.
    /// </remarks>
    public static string Reached(IReadOnlyList<StoryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var furthest = -1;
        foreach (var entry in entries)
        {
            if (FlightStages.Of(entry.Kind) is { } stage)
            {
                furthest = Math.Max(furthest, IndexOf(stage));
            }
        }

        // A STORY WITH NO PLACEABLE ENTRY IS STILL A FLIGHT THAT EXISTS. It was
        // created, whatever else its record does or does not hold.
        return furthest < 0 ? FlightStages.Created : FlightStages.All[furthest];
    }

    private static int IndexOf(string stage)
    {
        for (var i = 0; i < FlightStages.All.Count; i++)
        {
            if (string.Equals(FlightStages.All[i], stage, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
