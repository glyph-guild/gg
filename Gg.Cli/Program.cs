using System.Diagnostics;
using System.Reflection;
using Gg.Cli;
using Gg.Client;
using Gg.Console;

return CliArgs.Parse(args) switch
{
    CliAction.LaunchConsole => LaunchConsole(),
    // Taking a flight over runs in the DEVELOPER role like every other verb here,
    // and deliberately not in the console: a headless machine has no terminal, and
    // that is the whole point of it being a verb.
    CliAction.Take take => await TakeAsync(take.Json, commands => take.Return is { } outcome
        ? commands.ReturnAsync(take.Reference, outcome, take.Note)
        : commands.TakeAsync(take.Reference)),
    CliAction.PrintVersion => PrintVersion(),
    CliAction.Login => await AuthAsync(commands => commands.LoginAsync(Environment.MachineName)),
    CliAction.Logout => await AuthAsync(commands => commands.LogoutAsync()),
    CliAction.WhoAmI => await AuthAsync(commands => commands.WhoAmIAsync()),
    CliAction.RunnerUp or CliAction.RunnerServe => await RunnerUpAsync(),
    CliAction.RunnerMaintain maintain => await RunnerMaintainAsync(maintain.Pool),

    CliAction.Fly fly => await EmitAsync(
        fly.Json, c => c.FlyAsync(fly.Text, fly.Uri, provider: fly.Provider, id: fly.Id)),
    CliAction.Flights flights => await EmitAsync(
        flights.Json, c => c.ListAsync(flights.All, provider: flights.Provider, id: flights.Id)),
    CliAction.Show show => await EmitAsync(show.Json, c => c.ShowAsync(show.Reference)),
    CliAction.Log log => await EmitAsync(log.Json, c => c.LogAsync(log.Reference)),
    CliAction.Runners runners => await EmitAsync(runners.Json, c => c.RunnersAsync()),
    CliAction.Plan plan => await EmitAsync(plan.Json, c => c.PlanAsync(plan.Flight)),
    CliAction.AirspaceShow airspace => await EmitAsync(airspace.Json, c => c.AirspaceAsync()),
    // THE WORKING COPY IS WHERE YOU ARE. Nothing configurable, because a flag
    // naming the tree would be a second place the estate's location is written
    // down - and the ADR is explicit that the repository is just a repository.
    CliAction.AirspacePull pull => await EmitAsync(
        pull.Json, c => c.AirspacePullAsync(Directory.GetCurrentDirectory())),
    CliAction.AirspaceDiff diff => await EmitAsync(
        diff.Json, c => c.AirspaceDiffAsync(Directory.GetCurrentDirectory())),
    CliAction.AirspaceApply apply => await EmitAsync(
        apply.Json, c => c.AirspaceApplyAsync(Directory.GetCurrentDirectory())),
    CliAction.RunnerLabels labels => await EmitAsync(labels.Json, c => c.RunnerLabelsAsync()),
    CliAction.Invite invite => await EmitAsync(invite.Json, c => c.InviteAsync()),
    CliAction.Why why => await EmitAsync(why.Json, c => c.WhyAsync(why.Flight, why.Obligation)),
    CliAction.Gates gates => await EmitAsync(gates.Json, c => c.GatesAsync()),
    CliAction.Decide decide => await EmitAsync(decide.Json, c => c.DecideAsync(
        decide.Flight, decide.Obligation, decide.Outcome, Observed(decide.Json), decide.Reason)),
    CliAction.Doctor doctor => await DoctorAsync(doctor.Json),
    CliAction.Bundle bundle => await BundleAsync(bundle.Json),

    CliAction.EnvelopeShow show => await EnvelopeAsync(show.Json, c => c.ShowAsync()),
    CliAction.EnvelopeApply apply =>
        await EnvelopeAsync(apply.Json, c => c.ApplyAsync(ReadEnvelope(apply.Source))),
    CliAction.StrategyApply strategy =>
        await StrategyAsync(strategy.Json,
            c => c.ApplyAsync(strategy.Name, ReadEnvelope(strategy.Source))),
    // No client and no session: validate contacts nothing, so a syntax error
    // costs no round trip and works with no network at all.
    CliAction.EnvelopeValidate check => EmitLocal(check.Json, () =>
        EnvelopeCommands.Validate(ReadEnvelope(check.Source))),

    CliAction.CredentialAdd add =>
        await CredentialAsync(add.Json, c => c.AddAsync(add.Repo, add.Scopes, add.Identity)),
    CliAction.CredentialList list => await CredentialAsync(list.Json, c => c.ListCredentialsAsync()),
    CliAction.CredentialRemove remove =>
        await CredentialAsync(remove.Json, c => c.RemoveCredentialAsync(remove.CredentialId)),

    CliAction.Unknown unknown => Fail(unknown.Message),
    _ => Fail("unhandled action"),
};

/// <summary>
/// The envelope text, from a file or from stdin.
/// </summary>
/// <remarks>
/// "-" reads stdin, which is what makes this compose with an editor and with
/// the sync a customer keeping envelopes in git will want. Their review
/// process, our authority.
/// </remarks>
static string ReadEnvelope(string source) =>
    source == "-" ? Console.In.ReadToEnd() : File.ReadAllText(source);

/// <summary>
/// A verb that needs nothing but the text it was handed.
/// </summary>
/// <remarks>
/// <b>An invalid envelope exits non-zero</b>, while still printing the whole
/// structured result. A validator that reports success on a document it just
/// refused is one nobody can put in a pipeline, and the pipeline is where this
/// verb earns its keep - a customer keeping envelopes in git wants their
/// review process and our authority.
/// </remarks>
static int EmitLocal(bool json, Func<VerbResult> run)
{
    try
    {
        var result = run();
        Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));
        return result is VerbResult.EnvelopeValidated { Value.Valid: false } ? 1 : 0;
    }
    catch (IOException unreadable)
    {
        return Fail(unreadable.Message);
    }
    catch (UnauthorizedAccessException unreadable)
    {
        return Fail(unreadable.Message);
    }
}

/// <summary>The envelope verbs, which need a session and the control plane.</summary>
/// <summary>
/// What this process can observe about how a decision was made.
/// </summary>
/// <remarks>
/// <para>
/// <b>Observations, never a conclusion.</b> There is no `attended` here on purpose:
/// connection is a transport fact and attendance is a decision record, and this process
/// cannot tell them apart. A person can pipe input; a script can allocate a terminal.
/// gg says what it saw and the control plane decides what that means.
/// </para>
/// <para>
/// <b>Nothing rendered, in this version.</b> `gg decide` takes the outcome on the
/// command line, so no evidence was shown and the honest answer to "was it read" is no.
/// That will change when the console gets a modal; the field exists now because a
/// decision recorded before it existed is unclassifiable afterwards.
/// </para>
/// </remarks>
static Gg.Contracts.DecisionObservations Observed(bool json) => new()
{
    // Both ends, because either being redirected means something other than a person at
    // a terminal is driving this.
    Interactive = !Console.IsInputRedirected && !Console.IsOutputRedirected && !json,
    EvidenceRendered = false,
    SecondsToDecide = null,
};

static async Task<int> EnvelopeAsync(bool json, Func<EnvelopeCommands, Task<VerbResult>> run)
{
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
    var commands = new EnvelopeCommands(new ControlPlaneClient(http), new FileSessionStore());

    try
    {
        var result = await run(commands);
        Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));
        return 0;
    }
    catch (NotSignedInException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (DirtyWorkingCopyException refusal)
    {
        // The list is the actionable part, so it reaches the person whole
        // rather than as "the tree is dirty".
        return Fail(refusal.Message);
    }
    catch (NoEnvelopeException missing)
    {
        return Fail(missing.Message);
    }
    catch (EnvelopeUnreadableException unreadable)
    {
        return Fail(unreadable.Message);
    }
    catch (EnvelopeRefusedException refused)
    {
        return Fail(refused.Message);
    }
    catch (IOException unreadable)
    {
        return Fail(unreadable.Message);
    }
}

static async Task<int> StrategyAsync(bool json, Func<StrategyCommands, Task<VerbResult>> run)
{
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
    var commands = new StrategyCommands(new ControlPlaneClient(http), new FileSessionStore());

    try
    {
        var result = await run(commands);
        Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));
        return 0;
    }
    catch (NotSignedInException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (StrategyUnreadableException unreadable)
    {
        return Fail(unreadable.Message);
    }
    catch (StrategyRefusedException refused)
    {
        return Fail(refused.Message);
    }
    catch (IOException unreadable)
    {
        return Fail(unreadable.Message);
    }
}

static string ControlPlaneAddress() =>
    Environment.GetEnvironmentVariable("GG_CONTROL_PLANE") ?? "http://localhost:5199";

/// <summary>
/// Runs a verb and prints its result - one way or the other, never both.
/// </summary>
/// <remarks>
/// The single place a result becomes characters on a screen. Every verb hands
/// back a VerbResult and nothing else, so the JSON and the human rendering are
/// two views of one document rather than two implementations that agree today.
/// The console at step 4b renders the same value through the same code.
/// </remarks>
static async Task<int> EmitAsync(bool json, Func<FlightCommands, Task<VerbResult>> run)
{
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
    var commands = new FlightCommands(new ControlPlaneClient(http), new FileSessionStore());

    try
    {
        var result = await run(commands);
        Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));

        // THREE OUTCOMES, THREE CODES. A script has to tell "you were told no"
        // from "we do not know yet", and one non-zero cannot carry both.
        return ExitCodes.For(result);
    }
    catch (DecisionRefusedException refused)
    {
        // AN ANSWER, AND IT USED TO BE A CRASH. Every refusal on this path left as
        // an unhandled InvalidOperationException - a stack trace and exit 134,
        // which is SIGABRT and is what gg looks like when it breaks.
        return Fail(refused.Message);
    }
    catch (NotSignedInException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (FlightReferenceException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (FlightNotFoundException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (FlightIntentException refusal)
    {
        // Article XI reaching a person: the diagnosis is the actionable part
        // and collapsing it into "bad request" would throw that away.
        return Fail(refusal.Message);
    }
    catch (ProtocolTooOldException refusal)
    {
        Console.Error.WriteLine(refusal.Message);
        return ExitCodes.Unavailable;
    }
    catch (HttpRequestException failure)
    {
        Console.Error.WriteLine(
            $"Could not reach the control plane at {baseAddress}: {failure.Message}. Try gg doctor.");
        return ExitCodes.Unavailable;
    }
}

/// <summary>
/// Runs a takeover verb and prints what came back.
/// </summary>
/// <remarks>
/// <b>Its own emitter for one reason: the refusal.</b> Somebody else holding the
/// flight is the ordinary case this verb exists for, not a fault - so it is caught
/// and printed as a sentence with a non-zero code, rather than reaching the
/// unhandled path as a stack trace and exit 134, which is what gg looks like when
/// it breaks.
/// </remarks>
static async Task<int> TakeAsync(bool json, Func<TakeCommands, Task<VerbResult>> run)
{
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
    var commands = new TakeCommands(new ControlPlaneClient(http), new FileSessionStore());

    try
    {
        var result = await run(commands);
        Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));
        return ExitCodes.For(result);
    }
    catch (TakeoverRefusedException refused)
    {
        // REFUSED, not unavailable. A script can tell "somebody has it" from "the
        // control plane is down", and one non-zero cannot carry both.
        return Fail(refused.Message);
    }
    catch (ArgumentException refused)
    {
        return Fail(refused.Message);
    }
    catch (NotSignedInException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (ProtocolTooOldException refusal)
    {
        Console.Error.WriteLine(refusal.Message);
        return ExitCodes.Unavailable;
    }
    catch (HttpRequestException failure)
    {
        Console.Error.WriteLine(
            $"Could not reach the control plane at {baseAddress}: {failure.Message}. Try gg doctor.");
        return ExitCodes.Unavailable;
    }
}

/// <summary>
/// Runs a credential verb, in the credential-broker role.
/// </summary>
/// <remarks>
/// Separate from <c>EmitAsync</c> only because the refusals are different
/// ones. The result path is the same: a VerbResult, printed one way or the
/// other, and never both.
/// </remarks>
static async Task<int> CredentialAsync(bool json, Func<CredentialCommands, Task<VerbResult>> run)
{
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };

    var commands = new CredentialCommands(
        new ControlPlaneClient(http),
        new FileSessionStore(),
        new FileCredentialStore(),
        // The only way a secret enters this process, and it is a terminal.
        new ConsoleSecretPrompt());

    try
    {
        var result = await run(commands);
        Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));
        return 0;
    }
    catch (NotSignedInException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (CredentialScopeException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (CredentialRefusedException refusal)
    {
        // Article XI reaching a person: the control plane refused with a
        // diagnosis and the diagnosis is the part they can act on.
        return Fail(refusal.Message);
    }
    catch (CredentialNotFoundException refusal)
    {
        return Fail(refusal.Message);
    }
    catch (ProtocolTooOldException refusal)
    {
        Console.Error.WriteLine(refusal.Message);
        return ExitCodes.Unavailable;
    }
    catch (HttpRequestException failure)
    {
        Console.Error.WriteLine(
            $"Could not reach the control plane at {baseAddress}: {failure.Message}. Try gg doctor.");
        return ExitCodes.Unavailable;
    }
}

/// <summary>
/// Runs doctor, which reports rather than throws.
/// </summary>
/// <remarks>
/// Separate from the verbs above because it is the one that must survive
/// everything they refuse on: an unreachable control plane is a finding here,
/// not a failure, or the command would be useless in exactly the case somebody
/// runs it.
/// </remarks>
static async Task<int> DoctorAsync(bool json)
{
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };

    var report = await new Doctor(
        new ControlPlaneClient(http), new FileSessionStore(), new FileCredentialStore(),
        new Uri(baseAddress)).RunAsync();

    var result = new VerbResult.Diagnosis(report);
    Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));

    return report.ExitCode;
}

/// <summary>
/// `gg bundle`. Everything doctor asked, plus what a person would otherwise
/// be asked for, minus anything a runner printed.
/// </summary>
/// <remarks>
/// The flight log is fetched only when the control plane answered. Asking for
/// it anyway would turn one clear "could not connect" into two, and the second
/// one would be the one people report.
/// </remarks>
static async Task<int> BundleAsync(bool json)
{
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };

    var sessions = new FileSessionStore();
    var client = new ControlPlaneClient(http);
    var report = await new Doctor(client, sessions, new FileCredentialStore(), new Uri(baseAddress))
        .RunAsync();

    // Observed with no tree: a bundle is taken from wherever somebody happens
    // to be standing, and the locks and the tree belong to a flight rather
    // than to this machine. The fingerprint is the same one the runner
    // records, which is what lets a bundle be matched to a flight's facts.
    var environment = Gg.Runner.Facts.EnvironmentSurvey.Observe(treePath: null, Gg.Contracts.EnvironmentProvenance.Reused);

    var result = new VerbResult.Bundle(
        Gg.Client.Bundle.Build(DateTimeOffset.UtcNow, environment, report, flightLog: null));

    Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));

    // Zero. A bundle is a report, not a verdict: exiting non-zero because the
    // machine it describes has a problem would make `gg bundle` unusable in
    // the script somebody writes to collect one.
    return 0;
}

static int LaunchConsole()
{
    // The queue is loaded through the VERBS, so what the console shows is what
    // `gg flights --json` would print. There is no other route to the data.
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
    var client = new ControlPlaneClient(http);
    var sessions = new FileSessionStore();
    var takes = new TakeCommands(client, sessions);
    var data = new ConsoleData(
        new FlightCommands(client, sessions),
        // The console can read the credential references and forget one. It
        // cannot add one: that needs a secret typed at a prompt, and a prompt
        // inside a Terminal.Gui modal is a keyboard path with its own
        // escape-hatch rules. Registering stays a command-line act.
        new CredentialCommands(client, sessions, new FileCredentialStore(), new ConsoleSecretPrompt()),
        takes);

    var initial = ConsoleStart.LoadAsync(data, takes.Principal()).GetAwaiter().GetResult();

    // TAKE AND HAND, PASSED FOR THE FIRST TIME. Both were optional constructor
    // arguments that only tests ever supplied, so the console's takeover key
    // answered "this console is not configured to take flights over" for the whole
    // of slices five and six while every piece of the machinery underneath was
    // written and tested.
    //
    // TakeSession is given a CLAIM rather than only a command to spawn. A console
    // that handed over a terminal without claiming would reintroduce the failure
    // slice seven exists to remove: two people on two machines both working one
    // flight, each believing they hold it.
    //
    // HAND IS STILL NOT PASSED, and that is a stated gap rather than an oversight.
    // HandSession needs two ports the product does not have: an `infer` that spawns
    // an agent to propose what appears to have been done, and an `ask` that reads a
    // confirmation from the terminal. Building the first means invoking an executor
    // from the console, which is a boundary slice seven does not touch. So the
    // hand-back key still answers "this console is not configured to hand flights
    // back", and gg:ConsoleTakeWiringTests says so out loud rather than asserting a
    // wiring that would have to be faked to pass.
    var final = new ConsoleLoop(
        new TerminalGuiSession(),
        new EditorSession(),
        new TakeSession(claim: reference =>
            takes.ClaimAsync(reference).GetAwaiter().GetResult()),
        hand: null,
        // THE WRITE PATH. Async verbs, a synchronous shell, and the bridge at the
        // edge - the same one ConsoleStart.LoadAsync uses two lines up. Without
        // this the gate keys resolved, reached the reducer and did nothing.
        // The prompt is the one the credential verb already uses on the command
        // line. It runs here with the UI torn down and the terminal free, which is
        // what answers the old objection to registering from a console: the
        // escape-hatch rules a modal would need do not apply to a process that owns
        // the screen.
        actions: new VerbConsoleActions(data, new ConsoleSecretPrompt())).Run(initial);

    // Demo/verification hook: prove the surviving model is the whole truth.
    var dumpPath = Environment.GetEnvironmentVariable("GG_STATE_DUMP");
    if (!string.IsNullOrEmpty(dumpPath))
    {
        File.WriteAllText(dumpPath, AppStateJson.Serialize(final));
    }
    return 0;
}

static int PrintVersion()
{
    // All three, not just the binary. A runner evaluating facts against a
    // vocabulary the control plane has moved past gives a silently wrong
    // answer, so the version that reveals it is printed alongside the others.
    Console.WriteLine($"gg                {GgVersions.Binary}");
    Console.WriteLine($"protocol          {GgVersions.Protocol}");
    Console.WriteLine($"fact vocabulary   {GgVersions.FactVocabulary}");
    return 0;
}

static async Task<int> AuthAsync(Func<AuthCommands, Task<int>> run)
{
    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };

    var commands = new AuthCommands(
        new ControlPlaneClient(http),
        new FileSessionStore(),
        new StandardConsoleWriter(),
        new SystemClock(),
        (span, token) => Task.Delay(span, token));

    try
    {
        return await run(commands);
    }
    catch (ProtocolTooOldException refusal)
    {
        Console.Error.WriteLine(refusal.Message);
        return 69;   // EX_UNAVAILABLE: the service will not serve this version
    }
    catch (HttpRequestException failure)
    {
        Console.Error.WriteLine($"Could not reach the control plane at {baseAddress}: {failure.Message}");
        return ExitCodes.Unavailable;
    }
}

static async Task<int> RunnerUpAsync()
{
    var baseAddress = ControlPlaneAddress();

    var session = new FileSessionStore().Read();
    if (session is null)
    {
        return Fail("not signed in — run `gg login` first. Registering a runner is a person's action.");
    }

    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };

    // A person registers the runner; the runner then holds only the credential
    // that comes back. The developer session never reaches the runner process.
    var registered = await new ControlPlaneClient(http)
        .RegisterRunnerAsync(session.SessionToken, Environment.MachineName);

    var labels = (Environment.GetEnvironmentVariable("GG_RUNNER_LABELS") ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var holdFor = int.TryParse(Environment.GetEnvironmentVariable("GG_RUNNER_HOLD_SECONDS"), out var seconds)
        ? TimeSpan.FromSeconds(seconds)
        : TimeSpan.FromSeconds(10);

    // Ctrl-C stops the loop. A KILL does not, and that is the interesting case:
    // the lease outlives the process and expires on the control plane's clock.
    using var stopping = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };

    // The runner resolves credentials from the SAME local store gg credential
    // add wrote to. The reference travels through the control plane; the value
    // never leaves this machine, and the two halves are joined here because
    // this is the only project that can see both.
    // Which providers this runner serves, and where they live. Deployment
    // knowledge, so it is configured rather than compiled in: gg is public and
    // distributed, and which forge a tenant uses is the control plane's
    // business. A provider nobody configured is a declared capability gap.
    var workspace = new Gg.Runner.Workspace(
        Gg.Runner.Vcs.VcsConfiguration.FromEnvironment(), new Gg.Runner.Vcs.WorkingTreeRoot());

    // Where this runner may LAND work, which is a second declaration on purpose.
    // A runner configured to read and not to write cannot write - there is no
    // adapter for it to reach. Absent is the ordinary state.
    var destinations = Gg.Runner.Vcs.DestinationConfiguration.FromEnvironment(
        api => new HttpClient { BaseAddress = new Uri(api) });

    // WHICH AGENT THIS MACHINE HAS, and none is a real answer. Until this line
    // existed the runner was handed no executor at all, so `gg runner serve`
    // built a loop that could not invoke anything and no flight in the product
    // ever ran an agent - registered is not invoked, on the verb the whole slice
    // is about. The host probes whatever comes back before it claims any work.
    var executor = Gg.Runner.Execution.ExecutorConfiguration.FromEnvironment();

    return await Gg.Runner.RunnerHost.RunAsync(
        new Uri(baseAddress), registered.RunnerId, registered.RunnerToken, labels, holdFor,
        new LocalCredentialResolver(new FileCredentialStore()), workspace, stopping.Token,
        destinations: destinations, executor: executor);
}

static async Task<int> RunnerMaintainAsync(string pool)
{
    var baseAddress = ControlPlaneAddress();

    // THE RUNNER'S OWN CREDENTIAL FIRST. A session lasts twelve hours and a
    // runner token thirty days, and this registers on every start - so a host
    // that could only present a session would fail to restart after half a day,
    // on a machine with nobody at it. RunnerRegistry designed the separation
    // for exactly this: "the runner's lifetime is its own."
    var runners = new FileRunnerStore();
    var held = runners.Usable(DateTimeOffset.UtcNow);

    // The scope-enforcing proxy, or nothing. A resident runner with no
    // endpoint is not a resident, and guessing a socket path here would be
    // the exact reach § 12 forbids - refused loudly, naming the variable.
    var configuration = Gg.Runner.Pools.PoolConfiguration.FromEnvironment();
    if (configuration is null)
    {
        return Fail("GG_POOL_ENDPOINT is not set. The resident runner acts only through the "
                  + "scope-enforcing proxy; point this at it (never at the raw socket).");
    }

    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
    string runnerToken;

    if (held is not null)
    {
        // Unattended, for as long as its own credential lasts.
        runnerToken = held.RunnerToken;
    }
    else
    {
        var signedIn = new FileSessionStore().Read();
        if (signedIn is null)
        {
            // NAMES THE CADENCE. "Not signed in" on a host that ran yesterday
            // reads like a broken machine rather than a credential reaching the
            // end of its life. Nothing renews a runner token - the protocol's
            // renew is for a LEASE - so this is a person's action every thirty
            // days, by design.
            return Fail(
                "no usable runner credential, and not signed in. A pool host runs on its own "
              + "runner token, which lasts thirty days and cannot be renewed - so a person signs "
              + "in once to mint a new one: run `gg login`, then start this again. Registering a "
              + "runner is a person's action.");
        }

        var registered = await new ControlPlaneClient(http)
            .RegisterRunnerAsync(signedIn.SessionToken, Environment.MachineName + ":maintain");

        // Kept so the next start needs nobody.
        runners.Write(new StoredRunner
        {
            RunnerId = registered.RunnerId,
            RunnerToken = registered.RunnerToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
        });

        runnerToken = registered.RunnerToken;
    }

    using var stopping = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };

    var protocol = new Gg.Runner.RunnerProtocolClient(http, runnerToken);
    var adapter = new Gg.Runner.Pools.DockerPoolAdapter(
        new HttpClient { BaseAddress = new Uri(configuration.Endpoint) });

    var loop = new Gg.Runner.Pools.MaintainLoop(
        protocol, adapter, new Gg.Runner.SystemClock(), Task.Delay);

    return await loop.RunAsync(pool, stopping.Token);
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return ExitCodes.Refused;
}
