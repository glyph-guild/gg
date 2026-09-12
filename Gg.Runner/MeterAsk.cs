using System.Diagnostics;

namespace Gg.Runner;

/// <summary>
/// Asks the executor to refresh the provider's meter.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one place gg runs another program to learn a number</b>, and it is
/// here rather than in <c>Gg.Local</c> because that is where the rule about
/// spawning lives. A UI session may not spawn a process; the runner does it
/// every flight, and the command line is explicit by construction.
/// </para>
/// <para>
/// <b>It passes no credential and receives none.</b> The executor already
/// holds the subscription's, which is the entire reason gg may have this
/// number at all — the alternative was reading another tool's
/// <c>.credentials.json</c> and calling an API with it.
/// </para>
/// <para>
/// <b>Measured:</b> <c>claude -p "/usage"</c> rewrote
/// <c>cachedUsageUtilization</c> on a machine whose reading was eleven hours
/// old, and created one on a host that had never had the key at all. It prints
/// a templated report and appears to cost no inference, which is a reason to
/// be unhurried about the cadence rather than a licence to ask on every beat.
/// </para>
/// </remarks>
public static class MeterAsk
{
    /// <summary>The prompt that refreshes it. A slash command, not a question.</summary>
    private const string Usage = "/usage";

    /// <summary>How long to wait before giving up and keeping the old reading.</summary>
    /// <remarks>
    /// <b>Bounded because a hung executor must not become gg's problem.</b>
    /// Nothing downstream of a refresh is load-bearing: the reading that was
    /// already there is still true, only older.
    /// </remarks>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Runs the configured executor's usage command. False when there is none.
    /// </summary>
    public static async Task<bool> RefreshAsync(
        string? binary, CancellationToken cancellationToken = default)
    {
        if (binary is not { Length: > 0 } || !File.Exists(binary))
        {
            // NOT AN ERROR. Most machines run no executor, and a machine that
            // cannot refresh still measures its own transcripts correctly.
            return false;
        }

        using var running = new Process
        {
            StartInfo = new ProcessStartInfo(binary)
            {
                // THE PROMPT AS ONE ARGUMENT, never a shell string. gg builds
                // argument lists everywhere for the reason a shell would
                // reintroduce: quoting is somebody else's parser.
                ArgumentList = { "-p", Usage },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };

        running.Start();

        // READ BOTH PIPES, or a chatty executor fills one and blocks forever
        // holding a lock on the thing we are waiting for.
        var out_ = running.StandardOutput.ReadToEndAsync(cancellationToken);
        var err = running.StandardError.ReadToEndAsync(cancellationToken);

        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(Patience);

        try
        {
            await running.WaitForExitAsync(patience.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try { running.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            return false;
        }

        // NOTHING IS READ OUT OF THE OUTPUT. What the command prints is a
        // report for a person; what gg uses is the cache the command WROTE,
        // read back through the same parser as always. Parsing the prose would
        // be a second reader of the same fact, and the brittler one.
        await Task.WhenAll(out_, err);

        return running.ExitCode == 0;
    }
}
