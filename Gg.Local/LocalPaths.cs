namespace Gg.Local;

/// <summary>
/// Where this machine keeps what gg leaves on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>One implementation, because two computations of one filename is a console
/// that tails nothing and reports no error.</b> The runner writes a flight's
/// live view and the console reads it, and until this existed the convention
/// lived in <c>Gg.Runner</c> where the console could not reach it — so the
/// console could not name the file it was built to read.
/// </para>
/// <para>
/// <b>State rather than cache</b>, for the reason <c>TranscriptStore</c> gives:
/// a cache directory is somewhere an operating system is entitled to empty
/// without asking, and a transcript is the only copy of what an agent did.
/// Live views ARE disposable, and they still live here — under their own
/// directory, so that clearing them is a different act from clearing evidence.
/// </para>
/// </remarks>
public static class LocalPaths
{
    /// <summary>Everything gg keeps on this machine, under one root.</summary>
    /// <remarks>
    /// <c>XDG_STATE_HOME</c> first, so a test or an operator can move all of it
    /// at once. The per-platform fallbacks are the ones
    /// <c>TranscriptStore.DefaultPath</c> established and are unchanged.
    /// </remarks>
    /// <param name="stateHome">
    /// Overrides the environment. Production passes nothing; a test passes its
    /// own directory, because <c>XDG_STATE_HOME</c> is process-global and a
    /// suite that runs four-wide cannot have one test setting it while another
    /// reads it.
    /// </param>
    public static string StateRoot(string? stateHome = null)
    {
        stateHome ??= Environment.GetEnvironmentVariable("XDG_STATE_HOME");

        var root = !string.IsNullOrWhiteSpace(stateHome)
            ? stateHome
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                OperatingSystem.IsWindows() ? "AppData/Local"
                : OperatingSystem.IsMacOS() ? "Library/Application Support"
                : ".local/state");

        return Path.Combine(root, "good-grief");
    }

    /// <summary>What an agent did. Durable, and never swept.</summary>
    public static string Transcripts(string? stateHome = null) =>
        Path.Combine(StateRoot(stateHome), "transcripts");

    /// <summary>What a runner has read of sweeps' skills, by commit.</summary>
    /// <remarks>
    /// <b>Disposable, and kept under state with the live views for their
    /// reason</b>: a commit's bytes never change, so clearing this costs one
    /// fetch per skill and loses nothing - which is also why it is a directory of
    /// its own, so clearing it is never clearing evidence.
    /// </remarks>
    public static string Skills(string? stateHome = null) =>
        Path.Combine(StateRoot(stateHome), "skills");

    /// <summary>
    /// Where a sweep's agent starts: one empty directory per sweep, deleted
    /// when it ends.
    /// </summary>
    /// <remarks>
    /// <b>A sweep materializes nothing, and still needs somewhere to be.</b> An
    /// agent started in the runner's own working directory would be an agent
    /// standing in whatever that machine happens to hold, with
    /// <c>--setting-sources project</c> reading a repository nobody chose. So a
    /// sweep gets a directory with nothing in it, which is also the cheapest
    /// way to make "it changed nothing" checkable.
    /// </remarks>
    public static string Sweeps(string? stateHome = null) =>
        Path.Combine(StateRoot(stateHome), "sweeps");

    /// <summary>
    /// What an agent is doing. Deletable, and swept.
    /// </summary>
    /// <remarks>
    /// <b>A sibling of the transcripts rather than a child, and the difference is
    /// the point.</b> Somebody clearing live views must not have to be careful
    /// about which files they are; a directory under the transcripts would make
    /// one <c>rm -rf</c> take the evidence with it.
    /// </remarks>
    public static string LiveViews(string? stateHome = null) =>
        Path.Combine(StateRoot(stateHome), "live");

    /// <summary>This flight's live view, by the name both halves compute.</summary>
    /// <param name="root">
    /// Overrides <see cref="LiveViews"/>, for a test that wants its own
    /// directory. Production passes nothing.
    /// </param>
    /// <remarks>
    /// <b>Returned normalised.</b> The first version of this composed the
    /// directory as <c>transcripts/../live</c>, which names the right place and
    /// spells it as a path containing a parent segment — so two halves comparing
    /// strings could disagree about one directory. <see cref="Path.GetFullPath"/>
    /// makes the answer the same on both sides.
    /// </remarks>
    public static string LiveView(string flightId, string? root = null, string? stateHome = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flightId);

        return Path.GetFullPath(
            Path.Combine(root ?? LiveViews(stateHome), Safe(flightId) + ".ndjson"));
    }

    /// <summary>
    /// A flight id as a filename, with anything else replaced.
    /// </summary>
    /// <remarks>
    /// A flight id is a uuid today and this does not depend on that: an id that
    /// arrived with a slash in it would otherwise name a directory somewhere
    /// else entirely.
    /// </remarks>
    public static string Safe(string component) =>
        new([.. component.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '-')]);
}
