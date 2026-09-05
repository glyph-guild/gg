namespace Gg.Runner.Execution;

/// <summary>
/// Where transcripts live: durable, and outside every ephemeral tree.
/// </summary>
/// <remarks>
/// <para>
/// <b>Outside is the whole point.</b> The tree is deleted when a flight ends -
/// that is what the observational tier IS - so a transcript written inside it
/// would be a reference to something already gone by the time anybody follows
/// it. A reference that cannot be followed on the machine that made it would
/// not be a capability gap; it would be a bug.
/// </para>
/// <para>
/// <b>And it is a declared gap even so.</b> ADR-0006 puts transcripts in blob
/// storage and there is no storage port, so this resolves here and nowhere
/// else. The artifact says that on itself - a gate that cannot follow the
/// locator finds out from the reference rather than from an empty fetch.
/// </para>
/// <para>
/// Nothing sweeps this. Transcripts are the record of what an agent did and
/// deleting evidence is a one-way change; retention is a decision somebody
/// makes rather than a directory that quietly empties.
/// </para>
/// </remarks>
public sealed class TranscriptStore
{
    private readonly string _path;

    public TranscriptStore(string? path = null) => _path = path ?? DefaultPath();

    /// <summary>Where transcripts live when nobody overrides it.</summary>
    /// <remarks>
    /// Under state rather than cache. A cache directory is somewhere an
    /// operating system is entitled to empty without asking, and this holds the
    /// only copy of what an agent did.
    /// </remarks>
    public static string DefaultPath() => Gg.Local.LocalPaths.Transcripts();


    /// <summary>Where this flight's loop transcript goes.</summary>
    public string For(string flightId, string loopId) =>
        Path.Combine(_path, Safe(flightId), Safe(loopId) + ".ndjson");

    /// <summary>
    /// A component safe to put in a path.
    /// </summary>
    /// <remarks>
    /// Ids come from the control plane and loop ids come from an envelope a
    /// customer wrote. Neither is allowed to name a directory above this one.
    /// </remarks>
    private static string Safe(string component) =>
        new([.. component.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '-')]);
}
