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
/// <b>IT CARRIES BODIES NOW, AND THE RULE THAT SAID OTHERWISE RESTED ON
/// SOMETHING UNTRUE.</b> This read: <i>"A summary, never the documents.
/// AppState is written to GG_STATE_DUMP and handed to
/// ConsoleData.BundleFrom, so a member able to carry envelope text would
/// put a tenant's governance documents in a file they send us."</i>
/// Measured before changing it: <c>BundleFrom</c> takes the state and
/// IGNORES it — it calls
/// <c>Bundle.Build(takenAt, environment, doctor, flightLog)</c> — and
/// <c>GG_STATE_DUMP</c> is an opt-in environment variable that
/// <c>Program.cs</c> calls a <i>"Demo/verification hook"</i>, written once
/// on exit. Governance text here reaches a debug dump somebody switched
/// on, not a bundle a customer sends.
/// </para>
/// <para>
/// <b>What forced it was the pane, not convenience.</b> The airspace tab
/// draws the selected document on disk, as applied, and as it composes —
/// and <c>PaneText</c> is pure, so all three must be on the model. The only
/// alternative was a request per arrow key, which <c>ConsoleStart</c>
/// refuses by name.
/// </para>
/// <para>
/// <b>The old rule's SHAPE still holds, so this carries what the tab draws
/// and nothing more:</b> the documents the airspace has applied, and the
/// text of the files on disk. Not logs, not evidence, not every read the
/// console makes — a member added here still has to argue for itself.
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

    /// <summary>
    /// Every document the airspace has applied, whole.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What the right-hand pane draws without asking anybody.</b> One read
    /// fills it — the same read that fills <see cref="Names"/> and
    /// <see cref="Working"/> — so moving the cursor costs nothing and the
    /// console keeps its rule that a session makes no network call.
    /// </para>
    /// <para>
    /// <b>Required rather than init-only</b>, because an init-only collection
    /// deserialises to null when the key is absent and the pane walks this one.
    /// That discriminator is recorded in
    /// <c>AbsentCollectionsSurviveTheWireTests</c>.
    /// </para>
    /// </remarks>
    public IReadOnlyList<Gg.Contracts.NamedEnvelopeState> Applied { get; init; } = [];
}
