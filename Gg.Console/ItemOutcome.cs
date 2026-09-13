namespace Gg.Console;

/// <summary>
/// How asking a reader about one work item ended.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two endings, where <see cref="BrowseOutcome"/> has five.</b> That is not
/// an oversight and it is not laziness: the browse pane distinguishes "no
/// tracker configured", "no browse tool", "an empty backlog", "refused" and
/// "not this protocol" because each one is a different thing for a person to go
/// and do. A detail that could not be read has ONE — read the sentence — so the
/// sentence is carried whole rather than sorted into a shape nobody branches on.
/// </para>
/// <para>
/// <b>The reader's own words, in both cases.</b> What comes back on success is
/// the rendering the server already does for an agent, and what comes back on
/// failure is whatever the reader said about it. Reassembling either here would
/// be a second opinion about the same bytes.
/// </para>
/// <para>
/// <b>It never throws, for <see cref="BrowseOutcome"/>'s reason.</b> The caller
/// is a redraw, and a redraw that has to catch is a console that dies because a
/// tracker did.
/// </para>
/// </remarks>
public abstract record ItemOutcome
{
    /// <summary>The reader answered, in its own words.</summary>
    public sealed record Read(string Said) : ItemOutcome;

    /// <summary>It did not, and this is why.</summary>
    public sealed record Nothing(string Why) : ItemOutcome;
}
