namespace Gg.Console;

/// <summary>
/// What the runner on this machine has said, read from its log.
/// </summary>
/// <remarks>
/// <para>
/// <b>A local file, which is the one thing a UI session may read.</b> The
/// exception is already written down for the live pane and is scoped to exactly
/// this: a file whose path the console holds. This reads that file and does
/// nothing else - no socket, no credential, no child. Starting and stopping the
/// runner are the loop's, between sessions, like every other write.
/// </para>
/// <para>
/// <b>The tail, not the file.</b> A runner that has been up for a day has a log
/// nobody wants delivered into a modal, and the question this answers is "what
/// is it doing now" - so it reads the last few kilobytes and keeps the last
/// handful of lines.
/// </para>
/// <para>
/// <b>It never throws.</b> A view that fails must not fail anything, and on
/// this side that means it must not take a UI session down mid-render: the
/// terminal would be left in a state nothing is holding. A log that cannot be
/// read is a log with nothing in it and a line saying so.
/// </para>
/// </remarks>
public sealed class RunnerLog(string path) : IRunnerLog
{
    /// <summary>How far back a first look goes.</summary>
    private const int Tail = 16 * 1024;

    /// <summary>How many lines survive into the model.</summary>
    private const int Lines = 200;

    public string Path => path;

    /// <summary>
    /// Copy what a runner is saying into its log, as it says it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Flushed every chunk, which is the whole of this method.</b> The
    /// obvious spelling - <c>CopyToAsync</c> into a <c>StreamWriter</c>'s base
    /// stream - writes into a <c>FileStream</c> buffer that nothing empties
    /// until the process exits, so the log of a runner that has been up for an
    /// hour is zero bytes and the log of one you just killed is complete. That
    /// is exactly backwards from what a log is for.
    /// </para>
    /// <para>
    /// <b>Here rather than in the composition root</b> for the reason the
    /// colours and the modal titles moved: it was four lines of plumbing in a
    /// place no test can reach, and it was wrong.
    /// </para>
    /// <para>
    /// It appends, so a restart adds to the story rather than erasing it, and
    /// it never throws - a log that fails must not take the runner's output
    /// with it.
    /// </para>
    /// </remarks>
    public static async Task CaptureAsync(
        Stream source, string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);

            await using var file = new FileStream(
                path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);

            var chunk = new byte[4096];

            while (true)
            {
                var got = await source.ReadAsync(chunk, cancellationToken);

                if (got == 0)
                {
                    return;
                }

                await file.WriteAsync(chunk.AsMemory(0, got), cancellationToken);
                await file.FlushAsync(cancellationToken);
            }
        }
        catch (Exception failure) when (failure is IOException
                                            or UnauthorizedAccessException
                                            or ObjectDisposedException
                                            or OperationCanceledException)
        {
            // Nothing to do and nowhere to say it: this runs behind a runner
            // whose output is the thing being captured.
        }
    }

    public IReadOnlyList<string> Read()
    {
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            using var file = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            if (file.Length > Tail)
            {
                file.Seek(-Tail, SeekOrigin.End);
            }

            using var reader = new StreamReader(file);

            // THE FIRST LINE AFTER A SEEK IS HALF A LINE, and dropping it is
            // cheaper than pretending a fragment is a sentence the runner said.
            var all = reader.ReadToEnd().Split('\n');
            var whole = file.Length > Tail ? all.Skip(1) : all;

            return
            [
                .. whole
                    .Select(line => line.TrimEnd('\r'))
                    .Where(line => line.Length > 0)
                    .TakeLast(Lines),
            ];
        }
        catch (Exception failure) when (failure is IOException
                                            or UnauthorizedAccessException
                                            or NotSupportedException)
        {
            return [$"(the log could not be read: {failure.Message})"];
        }
    }
}
