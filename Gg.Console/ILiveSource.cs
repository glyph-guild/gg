namespace Gg.Console;

/// <summary>
/// Whatever a watched flight has said since the last look.
/// </summary>
/// <remarks>
/// <para>
/// <b>An interface so the loop can be tested without a filesystem</b>, and so
/// the one implementation that reads a file stays the only thing that does.
/// <see cref="LiveTail"/> is that implementation; it holds the offset, restarts
/// when the file gets shorter, refuses partial lines, and re-strips because
/// anything on this machine can write to the file it reads.
/// </para>
/// <para>
/// It answers with what is new and never with an error. A missing file is an
/// empty read, which is the correct answer for a flight that has not started
/// writing - and the reason the pane has to say WHICH silence it is showing
/// rather than showing an empty box.
/// </para>
/// </remarks>
public interface ILiveSource
{
    /// <summary>Lines since the last call. Empty when there are none.</summary>
    IReadOnlyList<StreamLine> Read();

    /// <summary>
    /// Whether this flight has a live view at all.
    /// </summary>
    /// <remarks>
    /// <b>The difference between two silences.</b> No file means the runner has
    /// not started writing - a flight not yet claimed, or one that ran before
    /// the runner always wrote. A file with nothing new means the agent is
    /// working and has not spoken. A pane that shows an empty box for both is a
    /// pane that cannot tell a person which one they are looking at.
    /// </remarks>
    bool Exists { get; }
}
