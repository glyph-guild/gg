using Gg.Local;

namespace Gg.Console;

/// <summary>
/// How asking a reader for a page of work ended.
/// </summary>
/// <remarks>
/// <para>
/// <b>Five endings, because a pane has to say which one.</b>
/// <see cref="ILiveSource"/> makes the same argument one surface over: <i>"a
/// pane that shows an empty box for both is a pane that cannot tell a person
/// which one they are looking at"</i>. Browsing has more ways to end than the
/// live view does, and only ONE of them means there is no work.
/// </para>
/// <para>
/// <b>Why not just return a page and throw.</b> <see cref="IWorkItemSource"/>
/// is the interface a reader IMPLEMENTS, and throwing is right there - the
/// caller is a tool server that turns an exception into a sentence for an
/// agent. The console is not that caller. It has to draw the failure, next to
/// the reader's key, without stopping; an exception crossing into a redraw is
/// a console that dies because a tracker did.
/// </para>
/// <para>
/// <b>Each one is a different thing to do next</b>, which is the test of
/// whether the distinction earns its place: configure a browse tool, wait,
/// check the credential, restart the reader, or accept that the backlog is
/// empty.
/// </para>
/// </remarks>
public abstract record BrowseOutcome
{
    /// <summary>The reader answered. The page may be empty, and that is an answer.</summary>
    public sealed record Listed(WorkItemPage Page) : BrowseOutcome;

    /// <summary>
    /// The reader works and does not do this.
    /// </summary>
    /// <remarks>
    /// Not an error. A reader that reads one item by id is a useful reader, and
    /// this was the state of the only one deployed anywhere until gg served one
    /// itself. Reported as declared-and-not-browsable rather than probed.
    /// </remarks>
    public sealed record NotBrowsable(string Why) : BrowseOutcome;

    /// <summary>
    /// The reader answered, and the answer was that it could not.
    /// </summary>
    /// <remarks>
    /// An unreachable tracker, an expired credential. <b>Carried through in the
    /// reader's own words</b>: it already said why, and rewording it here would
    /// be a second answer to one question.
    /// </remarks>
    public sealed record Refused(string Why) : BrowseOutcome;

    /// <summary>
    /// Something came back that is not this protocol.
    /// </summary>
    /// <remarks>
    /// <b>STDOUT IS THE PROTOCOL, read from this side.</b> The server's own
    /// remark warns that one stray line makes it look like it never
    /// initialized; this is what noticing looks like. It must never be reported
    /// as an empty tracker, because the thing to go and look at is a log, not a
    /// backlog.
    /// </remarks>
    public sealed record Unintelligible(string Why) : BrowseOutcome;

    /// <summary>
    /// Nothing came back at all.
    /// </summary>
    /// <remarks>
    /// A child that died at startup writes nothing and closes. Distinct from
    /// <see cref="Unintelligible"/> because the diagnosis differs: one is a
    /// broken server, the other is a server that is not there.
    /// </remarks>
    public sealed record Silent(string Why) : BrowseOutcome;
}
