using Gg.Contracts;

namespace Gg.Runner.Environments;

/// <summary>
/// The script a work kind names for bringing its stack up and down.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0023's crystalize at a second subject, in the owner's words:</b>
/// <i>instructions should crystalize into scripts, if they can, that run on the
/// runners.</i> A stack bring-up is that shape — advice an agent follows until
/// the same advice has been followed enough times to be worth writing down.
/// </para>
/// <para>
/// <b>Performed on the same terms as the agent it replaces</b> — ADR-0023 § 4:
/// same tool servers, same tier, same grant. It is narrower in two ways, both in
/// its favour: it reads no text as instructions, so there is nothing to inject
/// into, and it is deterministic.
/// </para>
/// <para>
/// <b>The path is bounded here as well as at the document.</b> An envelope is
/// validated when it is applied; a lease arrives from a control plane this binary
/// does not control. The far check is a contract and this one is a boundary, and
/// a boundary that trusts the far side is not one.
/// </para>
/// </remarks>
public static class StackScript
{
    /// <summary>Make the project ready to run here, before anything starts.</summary>
    /// <remarks>
    /// The contract's, since the point crosses on <see cref="StackPerformed"/>
    /// — two constants spelling the same word is a thing to keep in sync. These
    /// five replaced <c>up</c> and <c>down</c> when ADR-0033's second amendment
    /// restored the abstraction; <c>StackVerbs</c> listed two and is gone.
    /// </remarks>
    public const string Prepare = EnvironmentPoints.Prepare;

    /// <summary>Bring the stack up, wait until it answers, and return.</summary>
    public const string Attach = EnvironmentPoints.Attach;

    /// <summary>Move the tree to where the stack will read it.</summary>
    public const string Sync = EnvironmentPoints.Sync;

    /// <summary>Answer whether the stack is answering.</summary>
    public const string Ready = EnvironmentPoints.Ready;

    /// <summary>Take the stack down, after the agent has finished.</summary>
    public const string Detach = EnvironmentPoints.Detach;

    /// <summary>Whether this environment names an executable at all.</summary>
    /// <remarks>
    /// False is every environment in the field: the agent works the bring-up out
    /// from advice, which is where one stays until that advice has been used
    /// enough to be worth writing down.
    /// </remarks>
    public static bool Runs(string? hooks) => !string.IsNullOrWhiteSpace(hooks);

    /// <summary>The argument this verb is performed with.</summary>
    public static string ArgumentFor(string verb) => verb;

    /// <summary>
    /// Where the script would be inside this tree, or null when the path leaves
    /// it.
    /// </summary>
    /// <remarks>
    /// <b>Resolved and compared, rather than inspected for <c>..</c>.</b> A
    /// segment check is a check of spelling; what matters is where the path
    /// lands, and only the filesystem's own resolution answers that. It says
    /// where the script WOULD be — whether it is there is the caller's to find
    /// out, because a document valid when it was applied describes a repository
    /// that has since moved.
    /// </remarks>
    public static string? Within(string tree, string? stack)
    {
        if (!Runs(stack) || string.IsNullOrWhiteSpace(tree))
        {
            return null;
        }

        var root = Path.GetFullPath(tree);
        var full = Path.GetFullPath(Path.Combine(root, stack!));

        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? full
            : null;
    }


    /// <summary>
    /// How many times a start is attempted before the script is called
    /// unstartable.
    /// </summary>
    /// <remarks>
    /// Three, because the window the retry exists for is a descriptor closing —
    /// measured in milliseconds, and gone the moment the child that inherited it
    /// does. A script that genuinely will not run here is reported three hundred
    /// milliseconds later, which nothing notices.
    /// </remarks>
    private const int StartAttempts = 3;

    /// <summary>How long to wait between those attempts.</summary>
    private static readonly TimeSpan StartBackoff = TimeSpan.FromMilliseconds(150);

    /// <summary>How long a killed process is given to actually die.</summary>
    /// <remarks>
    /// Not a policy so much as the difference between asking and knowing. If it
    /// outlasts this, the performance says it survived, and a host with a stack
    /// nobody owns is a thing a person can be told about rather than discover.
    /// </remarks>
    private static readonly TimeSpan Interment = TimeSpan.FromSeconds(5);

    /// <summary>How long a bring-up may take before it is abandoned.</summary>
    /// <remarks>
    /// A stack of thirteen containers pulling cold images is minutes, not
    /// seconds; a script that has hung is not worth a flight's whole budget. Ten
    /// minutes is the first number, and the walk is what moves it.
    /// </remarks>
    public static readonly TimeSpan Patience = TimeSpan.FromMinutes(10);

    /// <summary>
    /// What one performance of a script did, as the runner measured it.
    /// </summary>
    /// <remarks>
    /// <b>No path of its own, deliberately.</b> What <see cref="PerformAsync"/>
    /// is handed is resolved against a checkout under <c>/srv/env</c>, so it is
    /// absolute and describes a pool host's layout. The caller holds the path the
    /// KIND named — relative, and the one that means something to a reader — so
    /// carrying one here would offer a second source for the member that crosses,
    /// and the wrong one is the easier to reach.
    /// </remarks>
    /// <param name="Outcome">One of <see cref="StackOutcomes"/>.</param>
    /// <param name="Exit">The exit code, for the one outcome that has one.</param>
    /// <param name="Took">How long it ran.</param>
    /// <param name="Survived">
    /// Whether the process was still alive when this returned. False for every
    /// outcome the runner produces — a timeout kills what it abandons — and the
    /// member exists so a test can say so rather than assume it.
    /// </param>
    public readonly record struct Performance(
        string Outcome, int? Exit, TimeSpan Took, bool Survived);

    /// <summary>Performs one verb of this script, inside the tree, and measures it.</summary>
    /// <remarks>
    /// <para>
    /// <b>The exit code is read, which is the whole of what the owner asked
    /// for</b> — <i>the thing to measure is whether the script works</i>. This
    /// waited for the process and read nothing off it for one release, so
    /// <c>exit 3</c> and <c>exit 0</c> were the same event and a failed bring-up
    /// reached a person only as work failing against a stack that was not there.
    /// </para>
    /// <para>
    /// <b>Patience is the caller's.</b> It was a constant read in here, which
    /// made the timeout untestable without a ten-minute test — and the one thing
    /// a timeout has to be is tested, because the path it takes is the one
    /// nobody walks by hand. <see cref="Patience"/> is still the runner's policy
    /// and the runner still passes it.
    /// </para>
    /// <para>
    /// <b>A hung script is killed, not merely abandoned.</b> Cancelling the wait
    /// leaves the process running: it holds the ports the next flight needs, and
    /// the reclaim on the way in is a backstop rather than a licence to leave
    /// one. The whole tree goes, because a bring-up's children are the stack.
    /// </para>
    /// <para>
    /// <b>Nothing throws.</b> Every end a performance can have is one of three
    /// outcomes, because a caller that has to catch to find out what happened is
    /// one that will swallow — which is what the caller here did, for both of
    /// the ends it could not name.
    /// </para>
    /// </remarks>
    public static async Task<Performance> PerformAsync(
        string script, string verb, string workingDirectory, TimeSpan patience,
        CancellationToken cancellationToken = default)
    {
        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = script.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
                ? "pwsh"
                : script,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };

        if (start.FileName == "pwsh")
        {
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(script);
        }

        start.ArgumentList.Add(verb);

        var began = System.Diagnostics.Stopwatch.StartNew();

        System.Diagnostics.Process? process = null;

        for (var attempt = 1; attempt <= StartAttempts && process is null; attempt++)
        {
            try
            {
                process = System.Diagnostics.Process.Start(start);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException
                                            or System.ComponentModel.Win32Exception
                                            or InvalidOperationException)
            {
                // A FILE THE TREE HAS AND THE HOST WILL NOT RUN - a mode lost in
                // a checkout, a shebang naming an interpreter the image lacks, a
                // `pwsh` that is not installed. All of them are "this script does
                // not work here", which is the thing being measured.
                //
                // EXCEPT ONE, AND IT IS WHY THIS RETRIES. Linux refuses to exec a
                // file while any process holds it open for writing (ETXTBSY), and
                // a fork inherits the whole descriptor table - so a runner that
                // checks a tree out and then performs a script from it can be
                // refused by its own timing, on a thread that had nothing to do
                // with either. Recorded once and believed, that blames the script
                // for the runner. CI found it on the first run of this very
                // feature's tests, which fork constantly.
                //
                // NOT TOLD APART BY ERRNO. The transient one is the only one
                // worth retrying and the rest cost a few hundred milliseconds
                // once, in a method whose patience is ten minutes - and reading a
                // platform error number to decide would be this file growing an
                // opinion about two kernels to save that.
                if (attempt < StartAttempts)
                {
                    await Task.Delay(StartBackoff, cancellationToken);
                }
            }
        }

        if (process is null)
        {
            return new Performance(StackOutcomes.Unstartable, null, began.Elapsed, Survived: false);
        }

        using (process)
        {
            using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            waiting.CancelAfter(patience);

            try
            {
                await process.WaitForExitAsync(waiting.Token);

                return new Performance(
                    StackOutcomes.Exited, process.ExitCode, began.Elapsed, Survived: false);
            }
            catch (OperationCanceledException)
            {
                // THE WHOLE TREE, because a bring-up's children ARE the stack and
                // killing the script alone would leave what it started. Swallowed
                // because the process may have exited in the moment between the
                // cancellation and this line, and a race is not an outcome.
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception failure) when (failure is InvalidOperationException
                                                or NotSupportedException
                                                or System.ComponentModel.Win32Exception)
                {
                }

                // THE OUTER TOKEN IS THE FLIGHT GOING AWAY, and that is not a
                // measurement of this script. It is rethrown after the kill,
                // because a caller being cancelled should stop rather than
                // receive a verdict - and calling it `unstartable` would blame
                // the script for a flight somebody ended.
                cancellationToken.ThrowIfCancellationRequested();

                // KILLED, AND CONFIRMED DEAD. Kill only ASKS; a timeout that
                // reported a dead process without waiting for one would be the
                // prose-asserts-what-the-code-lacks shape, in the member whose
                // whole job is to say the host was left clean.
                using var interment = new CancellationTokenSource(Interment);

                try
                {
                    await process.WaitForExitAsync(interment.Token);
                }
                catch (OperationCanceledException)
                {
                }

                return new Performance(
                    StackOutcomes.Timeout, null, began.Elapsed, Survived: !process.HasExited);
            }
        }
    }
}
