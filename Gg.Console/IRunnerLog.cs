namespace Gg.Console;

/// <summary>
/// The runner's log, as much of it as is worth showing.
/// </summary>
/// <remarks>
/// <b>Its own interface so the session can hold it and nothing else.</b> The
/// thing that starts and stops the runner spawns a child, which a UI session
/// may not do; this reads a file, which is precisely what the exception allows.
/// Two interfaces rather than one is what keeps that difference structural -
/// the same split <c>ILiveSource</c> has from the shell's ports.
/// </remarks>
public interface IRunnerLog
{
    /// <summary>Where it is, so the modal can say where to look.</summary>
    string Path { get; }

    /// <summary>The tail of it, newest last. Never throws.</summary>
    IReadOnlyList<string> Read();
}
