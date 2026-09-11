namespace Gg.Console;

/// <summary>
/// The estate as this machine can see it: every name the tenant has, and what
/// the working copy says about them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two answers held as the types the verbs return, joined only at the
/// render.</b> <see cref="Names"/> is the topology read and <see cref="Working"/>
/// is the diff verb's own result. Flattening them into one list of rows here
/// would mean deciding, in the console, which document is in sync and which way
/// a changed one moves — and direction is the computation ADR-0016 § 6 made the
/// control of who may apply. A second opinion about it is exactly what a
/// permission model was refused for, and the copy that drifts is the one nobody
/// is looking at.
/// </para>
/// <para>
/// <b>A summary, never the documents.</b> <c>AppState</c> is written to
/// <c>GG_STATE_DUMP</c> and handed to <c>ConsoleData.BundleFrom</c>, so a member
/// able to carry envelope text would put a tenant's governance documents in a
/// file they send us. The topology carries names, roles and versions; the diff
/// carries names, paths and directions. Neither carries a body, and a test holds
/// this type to that.
/// </para>
/// <para>
/// <b>Null <see cref="Working"/> is a state rather than a failure.</b> A tenant
/// can have a whole topology and nothing pulled — which is every tenant before
/// their first pull — and that is a different thing from a working copy that
/// could not be read. The first wants the name of a verb; the second wants a
/// diagnosis.
/// </para>
/// </remarks>
public sealed record EstateOnThisMachine
{
    /// <summary>Where the working copy is, or null when nobody has said.</summary>
    /// <remarks>
    /// Worth carrying even though the read already used it: the verbs fall back
    /// to whatever directory gg was run from, so <i>which tree did that write
    /// to</i> is a real question and a pane that shows documents without saying
    /// where they came from invites the wrong answer to it.
    /// </remarks>
    public string? Root { get; init; }

    /// <summary>Whether that path is inside a git working tree.</summary>
    public bool IsRepository { get; init; }

    /// <summary>
    /// What the working copy holds, or null when nobody has said where it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE ONE MEMBER THAT NEEDS NO SESSION, which is why it is first.</b>
    /// The names and the diff both come off the control plane; this is a walk
    /// of a directory. A tab whose rows are files can be drawn on a machine
    /// with no session, no network and no applied envelope, and until this
    /// existed it could not.
    /// </para>
    /// <para>
    /// <b>A SUMMARY, AND THE FIRST VERSION OF THIS WAS NOT.</b> It held
    /// <c>Gg.Client.TreeRead</c>, whose <c>TreeDocument</c> carries the PARSED
    /// DOCUMENT — so every approver, rule and glob in a tenant's governance
    /// went into <c>GG_STATE_DUMP</c> and the diagnostics bundle. Not the
    /// text, and the same information. The assertion written for this rule is
    /// what caught it. <see cref="WorkingCopy"/> carries a name, a role, a
    /// path and a version: the same class of fact the topology and the diff
    /// already carry.
    /// </para>
    /// <para>
    /// <b>Null is "nobody has said where", not "empty".</b> An airspace with
    /// nothing in it is a <c>TreeRead</c> whose <c>Present</c> is false, which
    /// is a different sentence again — somebody standing in the wrong
    /// directory.
    /// </para>
    /// </remarks>
    public WorkingCopy? Tree { get; init; }

    /// <summary>
    /// The documents git says have changed since the pull that wrote them.
    /// </summary>
    /// <remarks>
    /// <b>The local proxy for "you changed this", and the only one there is
    /// without a control plane to compare against.</b> It is NOT a direction
    /// and must not be shown as one: git knows a file moved, and nothing
    /// local knows whether that tightens or widens. Empty is an answer — a
    /// clean tree — where null would be indistinguishable from not having
    /// looked, so it is <c>required</c> rather than left to default.
    /// </remarks>
    public required IReadOnlyList<string> Uncommitted { get; init; } = [];

    /// <summary>Every name this tenant has, root first.</summary>
    public Gg.Contracts.EnvelopeTopology? Names { get; init; }

    /// <summary>What the working copy differs from the estate by, or null.</summary>
    public Gg.Client.EstateDiff? Working { get; init; }

    /// <summary>Why the working copy could not be read, when it could not.</summary>
    /// <remarks>
    /// Separate from a null <see cref="Working"/>, because "nothing is pulled
    /// here" and "something went wrong reading it" are opposite facts and only
    /// one of them is a thing to go and fix.
    /// </remarks>
    public string? Diagnosis { get; init; }
}
