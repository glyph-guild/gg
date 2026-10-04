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

    /// <summary>
    /// Why this point did not work, or null when it did.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A pure decision, so the loop has nothing to get wrong.</b> Before slice
    /// fifty-eight the loop performed a bring-up, recorded the result as a
    /// <c>stack.performed</c> fact and carried on — the only reader of the
    /// outcome collected it for shipping. A stack that failed to come up handed
    /// an agent an environment that was not there, and the agent spent its budget
    /// finding out. The judgement lives here because putting it in the loop is
    /// what made it possible to ignore an exit code at all.
    /// </para>
    /// <para>
    /// <b>It names the point.</b> Five can fail and they fail differently: a
    /// <c>prepare</c> that could not build is a tree or a registry problem, an
    /// <c>attach</c> that exited non-zero is the stack itself, a <c>detach</c>
    /// that failed left something running for the next flight to trip over. "A
    /// hook failed" sends a reader to read all five.
    /// </para>
    /// <para>
    /// <b>And it is not a retry.</b> <see cref="PerformAsync"/> already retries
    /// the START of a process, because a fork can lose a race with the
    /// filesystem — a different failure from a script that ran and said no.
    /// Running a bring-up again because it failed is how one half-built stack
    /// becomes two.
    /// </para>
    /// </remarks>
    public static string? Refusal(string point, Performance performance) =>
        performance.Outcome switch
        {
            StackOutcomes.Exited when performance.Exit is 0 => null,

            // THE CODE, because it is the first thing the script's author will
            // look up. Exit is nullable and only this outcome has one.
            StackOutcomes.Exited =>
                $"the '{point}' point of this environment's hooks exited {performance.Exit}. "
              + "The stack is not up, so nothing is gained by handing this flight to an agent "
              + "that would spend its budget discovering that. Fix the script, or take the "
              + "hooks off the environment and let an agent work the bring-up out from advice.",

            // NO CODE EXISTS HERE, which `Exit`'s own summary says: "the exit
            // code, for the one outcome that has one". Reporting a zero would
            // make a hang read as a success.
            StackOutcomes.Timeout =>
                $"the '{point}' point of this environment's hooks was still running after "
              + $"{Patience.TotalMinutes:0} minutes and was abandoned. A point that cannot "
              + "finish is the environment being short rather than a script to wait longer "
              + "for - attach is meant to reach health and return, not to hold it.",

            _ =>
                $"the '{point}' point of this environment's hooks would not start. The file is "
              + "there and this host would not run it, which is a missing interpreter line or "
              + "a lost executable bit rather than anything the stack did.",
        };

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
        string Outcome, int? Exit, TimeSpan Took, bool Survived, string? Said = null);

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
    /// <summary>
    /// What the <c>ready</c> point said about the stack.
    /// </summary>
    /// <remarks>
    /// <b>Three, because two would lose a measurement.</b> <see cref="No"/> is a
    /// stack the hook looked at and found not answering. <see cref="Unanswered"/>
    /// is a hook that did not say — it failed, it hung, or it exited cleanly
    /// without implementing the contract. Collapsing them makes a broken hook
    /// indistinguishable from a slow stack for ever, which is Article XI one
    /// layer out of the Engine.
    /// </remarks>
    public enum Readiness
    {
        /// <summary>The hook did not say. NOT the same as saying no.</summary>
        Unanswered,

        /// <summary>It looked, and the stack was not answering.</summary>
        No,

        /// <summary>It looked, and the stack was answering.</summary>
        Yes,
    }

    /// <summary>
    /// The answer, and whatever the hook chose to report alongside it.
    /// </summary>
    /// <param name="Readiness">One of three answers.</param>
    /// <param name="Values">
    /// Named values, by key. <c>url</c> is the only one anything interprets —
    /// the control plane stamps <c>preview.url</c> from it — and the rest is
    /// carried for a person to read. An unknown key is KEPT, because a reader
    /// debugging a queue consumer wants <c>depth=0</c> in front of them rather
    /// than discarded by a parser that did not recognise it.
    /// </param>
    public readonly record struct ReadyReport(
        Readiness Readiness, IReadOnlyDictionary<string, string> Values);

    /// <summary>The key whose value becomes an address.</summary>
    /// <remarks>
    /// The only interpreted name. ADR-0033 Decision 8: the preview address is
    /// read from the environment rather than declared — which is what makes a
    /// stack with no address a working environment rather than a broken one,
    /// something <c>PREVIEW_PORT</c> could not express.
    /// </remarks>
    public const string UrlKey = "url";

    private const string ReadyKey = "ready";

    /// <summary>
    /// Reads what <c>ready</c> reported.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A performance that did not exit zero is <see cref="Readiness.Unanswered"/>
    /// however it printed.</b> A hook that failed is not a stack that is down,
    /// and a flight told "not ready" when the truth is "nothing asked" waits for
    /// something that will never happen.
    /// </para>
    /// <para>
    /// <b>Silence is not readiness</b>, and it is the easiest mistake here
    /// because exit zero usually means yes. A hook that exits zero and says
    /// nothing has not answered; reading that as ready hands a flight an
    /// environment nobody checked.
    /// </para>
    /// <para>
    /// <b>The last answer wins.</b> <see cref="Attach"/> is told to poll until
    /// the stack answers and a <c>ready</c> written the same way prints as it
    /// goes, so what is true is what it said last — the same rule the runner uses
    /// reading the LAST <c>document.proposal</c> of a flight.
    /// </para>
    /// <para>
    /// <b>Noise is ignored rather than refused.</b> A hook is a script and
    /// scripts print: compose announces its networks, docker reports pull
    /// progress. A parser that refused a line it did not understand would make
    /// every working hook look broken, so only lines shaped like a report are
    /// read.
    /// </para>
    /// </remarks>
    public static ReadyReport ReadReady(Performance performance, string? said)
    {
        if (performance.Outcome != StackOutcomes.Exited || performance.Exit is not 0)
        {
            return new ReadyReport(Readiness.Unanswered, new Dictionary<string, string>());
        }

        var answer = Readiness.Unanswered;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in (said ?? string.Empty).Split('\n'))
        {
            // THE FIRST SEPARATOR ONLY. A connection string and a query string
            // both carry `=`, and splitting on every one would hand a person
            // half an address.
            var at = line.IndexOf('=', StringComparison.Ordinal);

            if (at <= 0)
            {
                continue;
            }

            var key = line[..at].Trim();
            var value = line[(at + 1)..].Trim();

            if (key.Length == 0)
            {
                continue;
            }

            if (string.Equals(key, ReadyKey, StringComparison.Ordinal))
            {
                // THE ANSWER, and not one of the values: carrying it in both
                // places would give two sources for one fact.
                answer = value switch
                {
                    "yes" => Readiness.Yes,
                    "no" => Readiness.No,

                    // A WORD NEITHER OF THOSE is a hook that tried to answer and
                    // said something this version cannot read, which is not an
                    // answer. Taken as unanswered rather than guessed at.
                    _ => Readiness.Unanswered,
                };

                continue;
            }

            values[key] = value;
        }

        return new ReadyReport(answer, values);
    }

    /// <summary>
    /// Points a hook at the daemon this flight was granted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The environment, because that is where a Docker client looks.</b> The
    /// CLI, an AppHost and a compose file all read <c>DOCKER_HOST</c>; handing
    /// the address any other way would mean teaching each of them separately.
    /// <c>ClaudeCodeExecutor.PlaceInstance</c> says the same thing for the
    /// agent, and this is the hook's half of it.
    /// </para>
    /// <para>
    /// <b>Without this a hook reaches the machine's own daemon</b>, which is the
    /// hazard <see cref="DockerInstanceDaemon"/> exists to avoid: an unscoped
    /// client risks <i>"quietly emptying whatever an ambient DOCKER_HOST pointed
    /// at — which on a pool host would be every member on the machine."</i> A
    /// bring-up hook is a Docker client like any other.
    /// </para>
    /// <para>
    /// <b>The same derivation the agent gets, not a second one.</b>
    /// <c>EnvironmentNaming.SocketFor</c> is the one place this convention lives
    /// on this side; an address derived anywhere else could point a hook at a
    /// different daemon from the agent working beside it in the same flight.
    /// </para>
    /// <para>
    /// <b>Placed, so an inherited value cannot win.</b> The platform granted this
    /// instance, and a <c>DOCKER_HOST</c> arriving from the runner's own
    /// environment would point the hook at the host's daemon — the one thing the
    /// instance exists to keep it away from.
    /// </para>
    /// </remarks>
    public static void PlaceInstance(System.Diagnostics.ProcessStartInfo info, string? instance)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (Pools.EnvironmentNaming.SocketFor(instance) is { } socket)
        {
            info.Environment["DOCKER_HOST"] = socket;
        }
    }

    /// <summary>
    /// Why this environment's declared hook cannot be run, or null when it can.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Because an absent file is otherwise a silence.</b> <see cref="Within"/>
    /// returns null for a path that does not resolve, and the invoker reads null
    /// as <i>"this environment has no hooks"</i> — which is every environment in
    /// the field, so it cannot be an error there. The consequence is an
    /// environment that declared <c>hooks:</c> and whose repository lacks the
    /// file running with NO STACK, and an agent finding an empty environment.
    /// </para>
    /// <para>
    /// <b>ONE file, because the point is an argument.</b> There is nothing to
    /// check per point: either the executable is there or it is not.
    /// </para>
    /// <para>
    /// <b>Two mistakes told apart.</b> A path that is missing is one somebody has
    /// not written; a path that climbs out of the checkout is one aimed at the
    /// pool host. <see cref="Within"/> refuses both by returning null, and
    /// somebody told "not found" about the second would go looking for a file
    /// that is exactly where they put it.
    /// </para>
    /// <para>
    /// <b>Named as the author wrote it.</b> The resolved form is absolute and
    /// describes a tree under <c>/srv/env</c> — a pool host's layout the author
    /// never sees and the control plane holds none of.
    /// </para>
    /// </remarks>
    public static string? Missing(string tree, string? hooks)
    {
        if (!Runs(hooks))
        {
            return null;
        }

        if (Within(tree, hooks) is not { } script)
        {
            return $"this environment's hooks are declared at '{hooks}', which resolves "
                 + "outside the checkout. A hook is a file in the repository this flight "
                 + "checked out; an absolute path, or one that climbs out of it, names a file "
                 + "on the pool host instead - which is not the tenant's to run from here.";
        }

        return File.Exists(script)
            ? null
            : $"this environment's hooks are declared at '{hooks}' and there is no file there. "
            + "Until slice fifty-eight that read as an environment with no hooks at all, so "
            + "the flight ran with no stack and an agent found an empty environment. Write "
            + "the executable - it is invoked once per point, with the point as its argument "
            + $"({string.Join(", ", EnvironmentPoints.All)}) - or take `hooks:` off the environment and let an "
            + "agent work the bring-up out from advice.";
    }

    /// <summary>
    /// Tells a hook which port the connector forwards to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Because the address is fixed and the stack is what moves.</b> The
    /// connector is brought up on the beat, once per machine, at
    /// <c>http://localhost:&lt;port&gt;</c> from the exposure document — and
    /// re-dialling it at whatever a flight reported is forbidden, since two
    /// connectors on one token are replicas of one tunnel. So gg says where to
    /// serve and the hook obeys.
    /// </para>
    /// <para>
    /// <b>What <c>PREVIEW_PORT</c> was reaching for.</b> That is an untyped
    /// string in a tenant document plus a sentence of prose, and nothing in any
    /// schema knows the name means a port. This is the platform's, placed rather
    /// than declared, with the <c>GG_</c> prefix <c>GG_IMAGE_DIGEST</c> and
    /// <c>GG_POOL_ENDPOINT</c> already carry.
    /// </para>
    /// <para>
    /// <b>Nothing is placed without an exposure</b> — every flight that serves
    /// nobody — because a port with no tunnel behind it is a number a stack would
    /// bind for no reason.
    /// </para>
    /// </remarks>
    public static void PlacePreview(System.Diagnostics.ProcessStartInfo info, int? port)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (port is { } serving)
        {
            info.Environment[PreviewPortVariable] =
                serving.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Where a hook reads the port it must serve on.</summary>
    public const string PreviewPortVariable = "GG_PREVIEW_PORT";

    public static async Task<Performance> PerformAsync(
        string script, string verb, string workingDirectory, TimeSpan patience,
        CancellationToken cancellationToken = default,
        string? instance = null,
        bool capture = false,
        int? previewPort = null)
    {
        var start = new System.Diagnostics.ProcessStartInfo
        {
            // CAPTURED ONLY WHERE SOMETHING READS IT. `ready` answers on stdout;
            // the other four points say what they did with an exit code, and
            // redirecting a pipe nobody drains is how a chatty bring-up hangs.
            RedirectStandardOutput = capture,
            FileName = script.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
                ? "pwsh"
                : script,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };

        // BEFORE ANYTHING ELSE TOUCHES THE ENVIRONMENT, so the granted socket is
        // what a Docker client in this hook finds, and the port it must serve on
        // is the one the connector forwards to.
        PlaceInstance(start, instance);
        PlacePreview(start, previewPort);

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
                // READ BEFORE THE WAIT, and that order is the whole correctness
                // of capturing. A pipe has a buffer; a hook that prints more than
                // it holds blocks on the write while this blocks on the exit, and
                // neither moves again. Starting the read first means the drain is
                // already running by the time anything waits.
                Task<string>? reading = capture
                    ? process.StandardOutput.ReadToEndAsync(waiting.Token)
                    : null;

                await process.WaitForExitAsync(waiting.Token);

                return new Performance(
                    StackOutcomes.Exited, process.ExitCode, began.Elapsed, Survived: false,
                    Said: reading is null ? null : await reading);
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
