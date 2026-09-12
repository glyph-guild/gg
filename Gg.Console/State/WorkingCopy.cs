namespace Gg.Console;

/// <summary>
/// One document in the working copy, as much of it as may be held.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHY THIS EXISTS RATHER THAN <c>Gg.Client.TreeDocument</c>.</b> That type
/// carries the PARSED DOCUMENT — <c>Envelope</c>, <c>EnvelopeNarrowing</c> or
/// <c>EnvironmentStrategy</c> — and putting it on <c>AppState</c> would put a
/// tenant's governance rules into <c>GG_STATE_DUMP</c> and the diagnostics
/// bundle. Not the text, but every approver, rule and glob in it, which is the
/// same information and the same problem.
/// </para>
/// <para>
/// <b>Found by the assertion written for it.</b> The first version held
/// <c>TreeRead</c> directly and the test looking for an approver in the
/// serialised state failed, which is the only reason this type is here rather
/// than that rule being broken quietly.
/// </para>
/// <para>
/// <b>So: the same class of fact the topology and the diff carry.</b> A name,
/// a role, a path and a version. Everything a row needs and nothing a document
/// says.
/// </para>
/// </remarks>
/// <param name="Role">One of <c>Roles</c>, taken from where the file sits.</param>
/// <param name="Name">The name in the topology this document is for.</param>
/// <param name="Path">Slash-separated, relative to the working copy.</param>
/// <param name="BasedOn">
/// What the file says it was rendered from, or null — which means it has never
/// been applied rather than that it is unchanged.
/// </param>
public sealed record AirspaceFile(string Role, string Name, string Path, string? BasedOn)
{
    /// <summary>
    /// What the file actually says, for the pane that shows it.
    /// </summary>
    /// <remarks>
    /// <b>The on-disk tab's content, and the only one of the three a session
    /// could have fetched itself.</b> Reading a local file whose path the
    /// console already holds is the stated exception to the session rule — but
    /// <c>PaneText</c> is pure and cannot read one, so the text rides on the
    /// model like everything else the views draw.
    /// <para>
    /// Null when the walk did not keep it, which is not the same as an empty
    /// file.
    /// </para>
    /// </remarks>
    public string? Text { get; init; }
}

/// <summary>
/// A file that sits where a document goes and does not read as one.
/// </summary>
/// <remarks>
/// <b>Carried because one of these stops every apply.</b> The diagnosis is the
/// parser's complaint about the shape, not the document's content — a missing
/// key or a line that will not parse — so it is a fact about the file rather
/// than a quotation from it.
/// </remarks>
public sealed record UnreadableFile(string Path, string Diagnosis);

/// <summary>
/// What the airspace working copy holds, read from disk and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE ONE THING THIS TAB CAN ANSWER WITH NO SESSION.</b> The names and the
/// diff both come off the control plane. This is a walk of a directory, so it
/// is true on a machine with no session, no network and no applied envelope —
/// and until it existed the tab showed nothing on exactly that machine.
/// </para>
/// <para>
/// <b><see cref="Present"/> is load-bearing and not the same as empty.</b>
/// <c>AirspaceTree.Read</c> keeps the distinction because reading an absent
/// tree as "retire everything" would make apply a verb nobody could safely
/// run; the pane keeps it because somebody standing in the wrong directory
/// wants a different sentence from somebody who has not pulled yet.
/// </para>
/// </remarks>
public sealed record WorkingCopy
{
    /// <summary>Whether <c>airspace/</c> is there at all.</summary>
    public required bool Present { get; init; }

    /// <summary>The documents, ordered by path as the walk found them.</summary>
    public required IReadOnlyList<AirspaceFile> Documents { get; init; }

    /// <summary>Files that sit where a document goes and do not read as one.</summary>
    public required IReadOnlyList<UnreadableFile> Unreadable { get; init; }
}
