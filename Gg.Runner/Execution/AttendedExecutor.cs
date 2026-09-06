using System.Diagnostics;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Runner.Execution;

/// <summary>
/// Hands the terminal to a person, in the flight's own tree, under the
/// flight's own envelope.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same runner, a different executor.</b> Everything on either side of
/// this call is unchanged: credentials resolve locally, the pinned trees
/// materialize, the change manifest is extracted from the tree, facts ship,
/// the landing decision is awaited and the destination is written. A person
/// editing a tree measures identically to an agent editing it, because
/// <c>ChangeExtractor</c> reads the tree and not the actor.
/// </para>
/// <para>
/// <b>The terminal is inherited, not redirected.</b> This is the opposite of
/// <see cref="ClaudeCodeExecutor"/>, which redirects all three streams
/// precisely so no terminal is involved. Here a person is at the keyboard and
/// the child owns the screen until it exits. The two <c>ProcessStartInfo</c>
/// blocks look alike and are opposites; <c>AttendedExecutorTests</c> asserts
/// this one is not tidied toward the other.
/// </para>
/// <para>
/// <b>Which is why it measures nothing.</b> There is no stream to read, so
/// attempts, moves used and the outcome are all unavailable — and every one of
/// them is required on <see cref="ExecutorRun"/>. It answers null, which
/// <see cref="IExecutorPort.ExecuteAsync"/> defines as *nothing measured a loop
/// at all*, and what an attended session could not measure is declared on a
/// fact rather than guessed at here.
/// </para>
/// <para>
/// <b>Nothing types into the child.</b> No flag pre-fills Claude Code's
/// composer without submitting, and screen-scraping another program's terminal
/// breaks whenever that program is improved. The person is told what the flight
/// is about and starts it themselves.
/// </para>
/// <para>
/// <b>And the operator's own settings are cleared.</b> Measured at CLI 2.1.261:
/// with <c>--setting-sources ""</c> and <c>--strict-mcp-config</c> the session
/// reports permission mode <c>default</c> and no tool servers; without them,
/// the same allow-list, and it reports the operator's own mode with the
/// operator's own servers attached. <c>--allowedTools</c> shrinks the tool
/// surface in neither case — 29 tools either way. So the bound on an attended
/// session is these two flags and not the allow-list, and a machine whose
/// settings say <c>skipAutoPermissionPrompt</c> would otherwise not even
/// prompt. What that costs the person — their plugins, their servers, their
/// permission mode — is said out loud before the child starts, because a
/// person whose skills vanished without explanation concludes the tool is
/// broken.
/// </para>
/// </remarks>
/// <param name="binary">
/// The agent binary. The same one <see cref="ExecutorConfiguration"/> resolves,
/// because a hand-flight and a fleet flight run the same agent.
/// </param>
/// <param name="announce">
/// Where the person is told what is about to happen. Null is the console, and
/// a test passes <see cref="TextWriter.Null"/> — the announcement is part of
/// the behaviour rather than decoration, so it is injected rather than
/// suppressed by a flag.
/// </param>
/// <param name="spawn">
/// Starts the child and answers its exit code, or null when it could not be
/// started. Injected so the launch is testable without a terminal; production
/// passes nothing.
/// <para>
/// <b>Asynchronous, and a test found out why.</b> A synchronous wait completes
/// the whole invocation before <c>RunnerLoop</c>'s renewal loop ever looks at
/// it, so the lease is never renewed under a child that is still running — and
/// a person holding a terminal past the lease's expiry is the ORDINARY case
/// here, not the exceptional one. A lapsed lease hands their flight to the
/// fleet mid-edit.
/// </para>
/// </param>
public sealed class AttendedExecutor(
    string binary,
    IReadOnlyList<IntentReader> readers,
    Func<string, string?>? secretFor = null,
    SelfInvocation? self = null,
    TextWriter? announce = null,
    Func<ProcessStartInfo, CancellationToken, Task<int?>>? spawn = null,
    Func<string, CancellationToken, Task<string>>? versionOf = null) : IExecutorPort
{
    private readonly string _binary = binary;
    private readonly IReadOnlyList<IntentReader> _readers = readers;
    private readonly Func<string, string?> _secretFor = secretFor ?? (_ => null);
    private readonly SelfInvocation? _self = self;
    private readonly TextWriter _announce = announce ?? System.Console.Out;
    private readonly Func<ProcessStartInfo, CancellationToken, Task<int?>> _spawn =
        spawn ?? InheritAsync;

    private readonly Func<string, CancellationToken, Task<string>> _versionOf =
        versionOf ?? ReportedVersionAsync;

    /// <summary>
    /// Running this executor cannot measure its own bound.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Neither way round works, which is why this is a skip rather than a
    /// substitution.</b> <see cref="MoveBoundProbe"/> INVOKES the port it is
    /// handed — its own temporary tree, an <c>ISSUE.md</c> asking for two
    /// writes, and then a look at the disk. Through this executor that hands a
    /// person the probe's canary task and waits for them to do it. Through the
    /// headless executor it measures a different session.
    /// </para>
    /// <para>
    /// <b>And the second is the one that looks safe.</b> <c>RunnerLoop</c> moved
    /// the probe to per-session deliberately, because "a measurement taken at
    /// startup measures the machine as it was before this session existed… what
    /// it buys is the product's only claim: that the measurement measures the
    /// session it governs." A headless reading stamped onto
    /// <c>environment.identity</c> as this flight's <c>moveEnforcement</c> would
    /// break exactly that claim, with the thing that exists to make it.
    /// </para>
    /// <para>
    /// So an attended flight's bound is <b>unmeasured</b>, and saying so on a
    /// fact is what rule 3 is for. Quietly measuring something else is what this
    /// stops.
    /// </para>
    /// </remarks>
    public bool BoundIsMeasurable => false;

    /// <summary>
    /// The same declaration the headless executor makes, deliberately.
    /// </summary>
    /// <remarks>
    /// <b>Nothing is added here.</b> Seven members were deleted at slice twenty
    /// because nothing ever degraded against any of them, and
    /// <c>IExecutorPort.Capabilities</c> has never been called by production at
    /// all. A second adapter is exactly the moment somebody starts declaring
    /// things about it again — so what this executor could not measure is said
    /// on a fact, where a person reads it, and not in a record nothing consults.
    /// </remarks>
    public static ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

    ExecutorCapabilities IExecutorPort.Capabilities => Capabilities;

    /// <summary>How the child is started, for a test that has no terminal.</summary>
    /// <remarks>
    /// <b>Separate from <c>ExecuteAsync</c> so the launch can be asserted.</b> A
    /// flag this runner meant to pass and did not is invisible to a test that
    /// stops at the configuration, and on this path the flag that matters most
    /// is the one clearing the operator's settings.
    /// </remarks>
    public static ProcessStartInfo StartInfoFor(
        ExecutorRequest request,
        IReadOnlyList<IntentReader> readers,
        string? secret = null,
        SelfInvocation? self = null,
        string binary = "claude")
    {
        ArgumentNullException.ThrowIfNull(request);

        var info = new ProcessStartInfo
        {
            FileName = binary,

            // The tree, so whatever they run is already looking at the work.
            WorkingDirectory = request.WorkingDirectory,

            // NOTHING IS REDIRECTED. The child owns the screen, and a redirect
            // here would leave a person typing into a pipe this process is not
            // reading. The headless executor's block is twenty lines away and
            // sets all three the other way.
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,

            // No shell, the same as the headless path: a shell decides for
            // itself what a child inherits, and this one's inheritance is the
            // whole point.
            UseShellExecute = false,
        };

        foreach (var argument in ClaudeCodeExecutor.BoundingArgumentsFor(
                     request, readers, secret, self))
        {
            info.ArgumentList.Add(argument);
        }

        return info;
    }

    /// <summary>
    /// Says what is about to happen, hands over the terminal, and measures
    /// nothing.
    /// </summary>
    public async Task<ExecutorRun?> ExecuteAsync(
        ExecutorRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // RESOLVED BEFORE ANYTHING STARTS, the same as the headless path. A
        // tool server launched without the credential its declaration named
        // fails at the tracker, with an authentication error nobody can trace
        // back to a missing file on this host.
        string? secret = null;
        if (ReaderFor(request) is { } declared)
        {
            secret = declared.Locator is { Length: > 0 } locator ? _secretFor(locator) : null;

            if (IntentConfiguration.Unresolvable(declared, secret) is { } unresolvable)
            {
                return ExecutorRun.Failed(
                    request.LoopId, unresolvable, attempts: 0, took: TimeSpan.Zero, movesUsed: []);
            }
        }

        var info = StartInfoFor(request, _readers, secret, _self, _binary);

        // SAID BEFORE THE CHILD STARTS, because once it starts the screen is
        // its own and nothing of ours will be read again until it exits.
        Announce(request);

        int? exit;
        try
        {
            exit = await _spawn(info, cancellationToken);
        }
        catch (Exception failure) when (failure is System.ComponentModel.Win32Exception
                                            or InvalidOperationException
                                            or FileNotFoundException)
        {
            return Unstartable(request, failure.Message);
        }

        if (exit is null)
        {
            return Unstartable(request, "the process could not be started");
        }

        // NOTHING MEASURED, AND THAT IS THE ANSWER. A person held the terminal;
        // this process watched a child it could not see inside. Attempts, moves
        // used and an outcome are all required on ExecutorRun and all three
        // would be invented here.
        return null;
    }

    /// <summary>
    /// What this session could not measure, and what it was measured against.
    /// </summary>
    /// <remarks>
    /// <b>Every one of the three is unavailable for the same reason and none of
    /// them is inferred.</b> There was no stream: nothing counted a turn,
    /// nothing saw a move, and the bound was not probed because probing means
    /// invoking the port, which here means handing a person the canary task and
    /// waiting for them.
    /// </remarks>
    public async Task<AttendedSession?> AttendedAsync(
        ExecutorRequest request, TimeSpan held, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new AttendedSession
        {
            Binary = _binary,
            BinaryVersion = await _versionOf(_binary, cancellationToken),
            Held = held,
            Unmeasured = [.. Gg.Contracts.AttendedGaps.All],
            SettingsCleared = Cleared(StartInfoFor(request, _readers, null, _self, _binary)),
        };
    }

    /// <summary>
    /// Which of the operator's settings sources this launch actually cleared.
    /// </summary>
    /// <remarks>
    /// <b>READ FROM THE ARGUMENTS, not from what this file believes it passes.</b>
    /// The two flags are what makes the envelope's bound mean anything on an
    /// attended session, and a fact that named them from a constant would go on
    /// asserting a bound after a refactor dropped one. Measured at CLI 2.1.261:
    /// with these the session reports permission mode <c>default</c> and no tool
    /// servers; without them, the operator's own mode with their own servers
    /// attached.
    /// </remarks>
    private static IReadOnlyList<string> Cleared(ProcessStartInfo info)
    {
        var cleared = new List<string>();

        if (info.ArgumentList.Contains("--setting-sources", StringComparer.Ordinal))
        {
            cleared.Add("setting-sources");
        }

        if (info.ArgumentList.Contains("--strict-mcp-config", StringComparer.Ordinal))
        {
            cleared.Add("mcp-servers");
        }

        return cleared;
    }

    /// <summary>What the binary says it is, or why it could not be asked.</summary>
    /// <remarks>
    /// <b>Unavailable rather than omitted</b>, on <c>EnvironmentSurvey</c>'s own
    /// pattern for git: a missing entry reads as "nobody looked" and this reads
    /// as "it was not there". Never empty, so the fact stays constructible on a
    /// machine where the binary has gone.
    /// </remarks>
    private static async Task<string> ReportedVersionAsync(
        string binary, CancellationToken cancellationToken)
    {
        try
        {
            using var asking = Process.Start(new ProcessStartInfo
            {
                FileName = binary,
                ArgumentList = { "--version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            if (asking is null)
            {
                return "unavailable: the process could not be started";
            }

            var reported = await asking.StandardOutput.ReadToEndAsync(cancellationToken);
            await asking.WaitForExitAsync(cancellationToken);

            return reported.Trim() is { Length: > 0 } said
                ? said
                : "unavailable: it printed nothing";
        }
        catch (Exception failure) when (failure is System.ComponentModel.Win32Exception
                                            or InvalidOperationException
                                            or FileNotFoundException)
        {
            return "unavailable: " + failure.Message;
        }
    }

    /// <summary>A child that would not start, named.</summary>
    /// <remarks>
    /// <b>A run rather than a null</b>, and the distinction is the one the port
    /// rests on: a session that ran and measured nothing is not the same fact as
    /// a session that never began. This one this runner DID measure.
    /// </remarks>
    private ExecutorRun Unstartable(ExecutorRequest request, string why) =>
        ExecutorRun.Failed(
            request.LoopId,
            $"'{_binary}' could not be started, so nobody was handed this flight: {why}",
            attempts: 0, took: TimeSpan.Zero, movesUsed: []);

    private void Announce(ExecutorRequest request)
    {
        _announce.WriteLine();
        _announce.WriteLine($"Flying {request.LoopId} by hand in {request.WorkingDirectory}");

        // WHAT WAS TAKEN AWAY, said rather than discovered. This session runs
        // with the operator's setting sources cleared and their tool servers
        // withheld - which is what makes the envelope's bound mean anything
        // here - and a person whose own skills and servers have silently
        // vanished concludes the tool is broken rather than that it is
        // governed.
        _announce.WriteLine(
            "Your own Claude Code settings, plugins and tool servers are not loaded for this "
          + "session: the flight's envelope decides what may run, not this machine's "
          + "configuration.");

        if (request.Moves is [_, ..])
        {
            _announce.WriteLine(
                "This flight declares: " + string.Join(", ", request.Moves) + ".");
        }

        _announce.WriteLine();
    }

    /// <summary>The reader for this flight's tracker, when it has one.</summary>
    private IntentReader? ReaderFor(ExecutorRequest request) =>
        request.IntentProvider is { Length: > 0 } provider
        && _readers.FirstOrDefault(
            r => string.Equals(r.Key, provider, StringComparison.Ordinal))
            is { Key.Length: > 0 } found
            ? found
            : null;

    /// <summary>Starts the child with this process's terminal, and waits.</summary>
    /// <remarks>
    /// <b>WaitForExitAsync, not WaitForExit.</b> The blocking one holds the
    /// thread that <c>RunnerLoop</c> renews the lease on, so a person at a
    /// terminal would watch their own lease lapse.
    /// </remarks>
    private static async Task<int?> InheritAsync(
        ProcessStartInfo info, CancellationToken cancellationToken)
    {
        using var child = Process.Start(info);
        if (child is null)
        {
            return null;
        }

        await child.WaitForExitAsync(cancellationToken);
        return child.ExitCode;
    }
}
