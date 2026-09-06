namespace Gg.Contracts;

/// <summary>
/// Everything that can happen to a flight, as a closed vocabulary.
/// </summary>
/// <remarks>
/// <para>
/// <b>Closed, because the alternative is what the flight log does.</b> Its
/// <c>Detail</c> is a serialized dictionary with different keys for each kind,
/// shipped in a member whose own doc says a per-kind shape <i>"would make both of
/// those parse a union"</i>. A closed kind plus flat positional params plus one
/// <see cref="FlightStory.Sentence"/> leaves nothing to branch on.
/// </para>
/// <para>
/// <b>Fourteen of these are the log's own kinds, by name.</b> The story is
/// composed from the same records; keeping the spelling means a person moving
/// between the two surfaces is not learning a second vocabulary, and a walk
/// asserting on one can be pointed at the other.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class StoryKinds
{
    // ---- the flight exists ----

    /// <summary>Somebody opened it.</summary>
    public const string Created = "created";

    /// <summary>Admission opened it, on the strength of another flight's work.</summary>
    public const string OpenedByAdmission = "opened-by-admission";

    // ---- it is up for grabs ----

    /// <summary>A claim was refused.</summary>
    public const string LeaseRefused = "lease-refused";

    /// <summary>A credential this flight needs could not be resolved.</summary>
    public const string CredentialUnresolved = "credential-unresolved";

    // ---- one machine holds it ----

    public const string LeaseGranted = "lease-granted";
    public const string LeaseRenewed = "lease-renewed";
    public const string LeaseReleased = "lease-released";
    public const string LeaseExpired = "lease-expired";
    public const string LeaseAbandoned = "lease-abandoned";

    // ---- the work ----

    /// <summary>A loop ran and reported what became of its own turn.</summary>
    public const string LoopRan = "loop-ran";

    /// <summary>An agent asked for something it is not allowed to decide.</summary>
    public const string LoopAsked = "loop-asked";

    // ---- the rules are checked ----

    /// <summary>One evaluation pass, with what it found.</summary>
    public const string Evaluated = "evaluated";

    /// <summary>Something shipped was refused at the door.</summary>
    public const string EvidenceRejected = "evidence-rejected";

    /// <summary>A rule the platform genuinely could not answer.</summary>
    public const string ObligationHalted = "obligation-halted";

    /// <summary>The platform decided this flight now needs a person.</summary>
    public const string LoopWaitingForPerson = "loop-waiting-for-person";

    /// <summary>A gate opened and named who decides.</summary>
    public const string DecisionAsked = "decision-asked";

    /// <summary>Somebody answered it.</summary>
    public const string DecisionMade = "decision-made";

    // ---- something became of it ----

    /// <summary>The flight ended, and this says how.</summary>
    /// <remarks>
    /// The entry the log has never had: a landed flight's log ends with a lease
    /// release, which is a story that stops one stage early.
    /// </remarks>
    public const string Ended = "ended";

    // ---- and three that interrupt at any stage ----

    /// <summary>A person took it over.</summary>
    public const string TakenOver = "taken-over";

    /// <summary>Their hold lapsed without them saying anything.</summary>
    public const string HoldExpired = "hold-expired";

    /// <summary>The pool this flight's machine came from had a bad time.</summary>
    public const string PoolIncident = "pool-incident";

    public static IReadOnlyList<string> All { get; } =
    [
        Created, OpenedByAdmission,
        LeaseRefused, CredentialUnresolved,
        LeaseGranted, LeaseRenewed, LeaseReleased, LeaseExpired, LeaseAbandoned,
        LoopRan, LoopAsked,
        Evaluated, EvidenceRejected, ObligationHalted, LoopWaitingForPerson,
        DecisionAsked, DecisionMade,
        Ended,
        TakenOver, HoldExpired, PoolIncident,
    ];

    /// <summary>
    /// Whether an entry of this kind says the flight is over.
    /// </summary>
    /// <remarks>
    /// <b>Exactly one kind does, and that is the point.</b> A runner reports on
    /// its turn and never on the flight — two of the four endings are not a
    /// machine's to report at all — so `StoryActorBoundTests` sweeps the
    /// vocabulary and refuses a runner-actor entry that carries this.
    /// </remarks>
    public static bool Ends(string kind) =>
        string.Equals(kind, Ended, StringComparison.Ordinal);
}
