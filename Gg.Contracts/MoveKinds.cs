namespace Gg.Contracts;

/// <summary>
/// What granting a move means: the grant of a recallable capability, or the
/// act itself.
/// </summary>
/// <remarks>
/// <para>
/// ADR-0014's third resolution: <b>enforce a move whose use is itself the
/// outward act; record-only a move whose product a destination still
/// gates.</b> An edit is recallable - the manifest measures it, the gates ask
/// about it, and nothing has left the tree until a destination admits it. A
/// <c>send</c> is not: using it IS the outward act, and no gate downstream of
/// the act can unsend it. Article VI is the axis that will populate the
/// enforced set - <c>send</c>, and <c>power-on</c>/<c>power-off</c> when the
/// maintenance verbs arrive.
/// </para>
/// <para>
/// <b>The enforced set is correctly empty today</b>, and the refusal below is
/// what keeps that from being a mechanism waiting for a member: a move
/// classified as an outward act, whose enforcement nothing this product has
/// lets a probe confirm, is refused at authoring - the lock installed before
/// the door. The probe confirms WITHHOLDING of tools that were not granted;
/// an outward move would need its bound to hold WHILE GRANTED, which nothing
/// today can measure, so the confirmable-outward set is empty and the
/// classification alone drives the refusal. The commit that ships the first
/// confirmable enforcement is the commit that adds the second input.
/// </para>
/// <para>
/// Contract-fingerprinted rather than fact-fingerprinted, deliberately: the
/// kind never crosses in a fact - it changes what Validate accepts, which is
/// contract surface. <see cref="LoopMoves"/> itself stays on the fact
/// fingerprint, because its VALUES do cross.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class MoveKinds
{
    /// <summary>Using it is itself the outward act, and nothing can take it back.</summary>
    public const string OutwardAct = "outward-act";

    /// <summary>Its product is still gated at a destination before anything leaves.</summary>
    public const string RecordOnly = "record-only";

    public static IReadOnlyList<string> All { get; } = [OutwardAct, RecordOnly];

    /// <summary>
    /// Every declared move's kind. A dictionary rather than a switch, so
    /// totality is a testable property instead of a compiler default.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Table { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Each produces something a destination gate still faces: read and
            // search produce knowledge, edit and write produce tree state the
            // manifest measures, run-tests produces outcomes the verdicts read.
            [LoopMoves.Read] = RecordOnly,
            [LoopMoves.Edit] = RecordOnly,

            // RUN-TESTS IS THE ONE THIS IS NOT QUITE TRUE OF, AND IT IS SAID
            // HERE BECAUSE HERE IS WHERE SOMEBODY READS THE CLASSIFICATION.
            // ClaudeCodeExecutor grants it `Bash`, and states one consequence:
            // "run-tests maps onto Bash, which can also edit files." The
            // sharper one was measured from inside a pool member -
            //
            //     cloudflared tunnel --url http://localhost:PORT
            //
            // - which dials OUT, publishes a URL to anybody who has it, opens
            // no port on the host, asks nothing of a firewall, and consults no
            // envelope. That is an act nothing can recall, which is the
            // definition of the kind this is not.
            //
            // IT IS STILL RECORD-ONLY, AND ON PURPOSE. The refusal above means
            // an outward act nothing can probe is refused at authoring, so
            // reclassifying this would refuse every envelope that runs a test.
            // The honest answer does not exist yet: it needs a bound a probe
            // can confirm WHILE the tool is granted, the way ScopeProbe reaches
            // outside the pool prefix and requires a refusal. A network bound
            // with that property is a SANDBOX, and gg does not have one.
            //
            // So this is a declared gap rather than a decision pending - the
            // shape ArtifactScopes carries for its single member. When a
            // sandbox ships, this line is the second input the paragraph above
            // says the first confirmable enforcement adds.
            [LoopMoves.RunTests] = RecordOnly,
            [LoopMoves.Search] = RecordOnly,
            [LoopMoves.Write] = RecordOnly,

            // NOMINATING PRODUCES A FACT, and whether that fact becomes a
            // flight is a destination's answer - so nothing has left, nobody
            // has been messaged, and admission can refuse it. That is the whole
            // difference between declaring a value and acting on one.
            [LoopMoves.Propose] = RecordOnly,

            // AND NEITHER DOES PROPOSING A WORK ITEM. It writes a fact saying
            // what someone thinks a tracker should hold; the tracker is not
            // touched, no agent holds a credential for one, and the write - if
            // it happens at all - is the runner's, after a destination admits
            // it. An agent that proposes has still not acted.
            [LoopMoves.ProposeWorkItem] = RecordOnly,

            // RECORD-ONLY, and more plainly so than its neighbour. A proposal
            // asks that somebody else's backlog change; this asks what to call
            // a pull request that may never be opened. Nothing outside the
            // flight has moved when it is called, and admission can still
            // refuse the whole landing.
            [LoopMoves.ProposeLanding] = RecordOnly,

            // AND THE ONE THE GAP ABOVE IS ABOUT, AT ITS WIDEST. `anything`
            // grants every tool the agent binary has, with the permission check
            // off - so the cloudflared paragraph is true of it without needing
            // Bash as the route. Reclassifying it would be dishonest in the
            // other direction and useless in this one: an outward act nothing
            // can probe is REFUSED at authoring, so `outward-act` here would
            // make the value undeclarable, which is the machine-level switch
            // this move was minted to replace.
            //
            // WHAT IS DIFFERENT, AND WHY THAT MAKES IT ACCEPTABLE. Every other
            // row is a claim about a grant somebody might not have read. This
            // one IS the reading: the envelope says, in the one word that
            // cannot be mistaken for anything else, that it is not bounding
            // this agent. The classification is not what bounds it, and for
            // this value nothing is - which is the fact the envelope states
            // and every reader downstream is made to repeat.
            [LoopMoves.Anything] = RecordOnly,
        };

    /// <summary>The kind of a declared move. THROWS on one nobody classified.</summary>
    /// <remarks>
    /// Article XI's poison, the Reason.Sentence shape: a sixth move added
    /// without a classification fails a build or a validate, never an audit -
    /// because the unclassified default would otherwise be record-only, and
    /// record-only is the answer that lets an unrecallable act be granted.
    /// </remarks>
    public static string Of(string move) =>
        Table.TryGetValue(move, out var kind)
            ? kind
            : throw new InvalidOperationException(
                $"'{move}' is not a move anybody classified. Decide one of: "
              + $"{OutwardAct}, {RecordOnly} - whether using it is itself the outward act "
              + "is the decision, and defaulting it would grant what nothing can recall.");
}
