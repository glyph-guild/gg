using System.Diagnostics;
using System.Reflection;
using Gg.Cli;
using Gg.Client;
using Gg.Console;
using Gg.Contracts;

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
    // BEFORE ANYTHING THAT PRINTS, and that placement is the whole contract.
    // Stdout IS the protocol here: one line of narration and the agent sees a
    // server that never initialized rather than a tool that failed.
    CliAction.RunnerTools => await PlatformToolServer.RunAsync(
        System.Console.In, System.Console.Out),
    // THE SAME CONTRACT, one server over. Stdout is the protocol here too, so
    // nothing on this path may print - including the credential resolution,
    // which fails as a tool error the agent can read rather than as a line.
    CliAction.RunnerRead read => await RunnerReadAsync(read),
    CliAction.RunnerUp or CliAction.RunnerServe => await RunnerUpAsync(),
    CliAction.RunnerMaintain maintain => await RunnerMaintainAsync(maintain.Pool),

    // BEFORE THE ORDINARY ARM, because a pattern that matched both would take
    // whichever came first - and it was the ordinary one, which is how
    // `--hand` parsed for a whole slice and did nothing.
    CliAction.Fly { ByHand: true } hand => await HandAsync(hand),
    CliAction.Fly fly => await EmitAsync(
        fly.Json, c => c.FlyAsync(
            fly.Text, fly.Uri, provider: fly.Provider, id: fly.Id, repository: fly.Repository)),
    CliAction.Flights flights => await EmitAsync(
        flights.Json, c => c.ListAsync(flights.All, intent: flights.Intent)),
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
    CliAction.Update update => await UpdateReportAsync(update.Json),
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

    // WHAT THIS MACHINE IS, read here because this is where the environment
    // belongs. Gg.Client references only Gg.Contracts, so the doctor is handed
    // facts rather than going looking for variables.
    var executor = Environment.GetEnvironmentVariable(
        Gg.Runner.Execution.ExecutorConfiguration.BinaryVariable);

    var role = new MachineRole
    {
        ExecutorBinary = executor,
        ExecutorPresent = executor is { Length: > 0 } && File.Exists(executor),
        ForgeHosts = Environment.GetEnvironmentVariable("GG_VCS_HOSTS"),
        DestinationApis = Environment.GetEnvironmentVariable("GG_DESTINATION_APIS"),
        PoolEndpoint = Environment.GetEnvironmentVariable("GG_POOL_ENDPOINT"),
    };

    var report = await new Doctor(
        new ControlPlaneClient(http), new FileSessionStore(), new FileCredentialStore(),
        new Uri(baseAddress),
        addressConfigured: Environment.GetEnvironmentVariable("GG_CONTROL_PLANE") is { Length: > 0 })
        .RunAsync(role: role);

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

    // SIGNING IN, WHICH THIS CONSOLE MAY DO BEFORE IT CAN DO ANYTHING ELSE.
    // The same commands `gg login` uses and the same session file, so a person
    // who signs in here is signed in at the command line and the other way
    // round - two ways in, one credential, and no second notion of who you are.
    //
    // The writer is the real one: the shell runs with the terminal provably
    // free, which is the same reason the secret prompt below is allowed to be.
    var auth = new AuthCommands(
        client, sessions, new StandardConsoleWriter(), new SystemClock(),
        (span, token) => Task.Delay(span, token));

    var data = new ConsoleData(
        new FlightCommands(client, sessions),
        // The console can read the credential references and forget one. It
        // cannot add one: that needs a secret typed at a prompt, and a prompt
        // inside a Terminal.Gui modal is a keyboard path with its own
        // escape-hatch rules. Registering stays a command-line act.
        new CredentialCommands(client, sessions, new FileCredentialStore(), new ConsoleSecretPrompt()),
        takes,
        // WHAT THIS TENANT SHOULD KNOW. The notices row above the queue was
        // drawn by PaneText from the first slice and assigned by nothing, so a
        // degradation the control plane reported on every call reached nobody.
        new IdentityCommands(client, sessions),
        // THE RULES IN FORCE, readable at last. Every flight the console shows
        // names this document's version and nothing could show the document.
        new EnvelopeCommands(client, sessions));

    var initial = ConsoleStart.LoadAsync(data, takes.Principal()).GetAwaiter().GetResult()
        // WHAT THIS MACHINE IS CONFIGURED TO DO, read once and handed over.
        // ExecutorConfiguration states the rule this follows: one place reads
        // the environment, and nothing downstream reads it again and reaches a
        // different answer. The console renders what it is given.
        //
        // DECLARED, NEVER SWEPT. Walking the process environment would put
        // whatever else a person exports - cloud keys, tokens - on a screen
        // they may be sharing and into the state dump. These are the variables
        // gg itself reads, and every one is named where it is read.
        with { Settings = ConsoleEnvironment.Read() };

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
    // ONE SET OF TAILS, SHARED. The loop advances the pane between sessions and
    // the screen advances it on a timer during one; both resume from the same
    // offset or the same lines arrive twice. It is owned here, outside every UI
    // lifetime, which is what keeps "a session retains nothing" true.
    var tails = new LiveTails(flightId => new LiveTail(Gg.Local.LocalPaths.LiveView(flightId)));

    var final = new ConsoleLoop(
        new TerminalGuiSession(tails),
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
        actions: new VerbConsoleActions(data, new ConsoleSecretPrompt()),
        tails: tails,
        // THE READ PATH, AND IT IS THE SAME ONE THE BOOT TOOK. Passing the boot
        // itself is what makes a refresh mean "as if you had just opened it"
        // rather than "as much of it as somebody remembered to re-read".
        //
        // The bridge at the edge, like the write path two lines up: async verbs,
        // a synchronous shell, and the loop owns the terminal while this runs.
        // GIVEN THE MODEL, not ignoring it. `_ =>` here meant every refresh was
        // a boot, so everything the loader does not read - the browse pane, the
        // receipts, and on a failure the entire queue - reset to a default.
        reload: current => ConsoleStart
            .LoadAsync(data, takes.Principal(), current)
            .GetAwaiter()
            .GetResult(),
        // THE CHECKLIST IS READ WHEN THE PANE IS OPENED, not at boot: it is off
        // by default, and a request for a pane nobody opened is a request
        // nobody wanted.
        // THE ONE WRITE THAT WORKS BEFORE THERE IS A SESSION, and the reason
        // this console is worth drawing on a machine that has none. The bridge
        // at the edge again: async verbs, a synchronous shell, and the terminal
        // is provably free while these run.
        //
        // The two halves of `gg login` rather than the verb, because the verb
        // fetches the code and blocks on it in one breath - the code would only
        // ever appear in what was printed before Terminal.Gui painted over it.
        // The device code stays inside SignInSession; what comes back to the
        // model is what a person reads off the screen.
        signIn: new SignInSession(
            () => auth.StartAsync(Environment.MachineName).GetAwaiter().GetResult(),
            started => auth.AwaitApprovalAsync(started).GetAwaiter().GetResult()),
        checklist: current => ConsoleChecklist.Read(data, current),
        repositories: current => ConsoleRepositories.Read(data, current),
        envelope: current => ConsoleEnvelope.Read(data, current))
        .Run(initial);

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

/// <summary>
/// `gg fly --hand`: open the flight and hand this terminal to the person.
/// </summary>
/// <remarks>
/// <b>Wiring only.</b> The order, the refusal and the three outcomes live in
/// <see cref="FlyByHandCommand"/>, where a test can reach them. What is here is
/// what only this project can supply: the control plane's address, this
/// machine's session, its own runner slot, and the attended executor.
/// </remarks>
static async Task<int> HandAsync(CliAction.Fly fly)
{
    var session = new FileSessionStore().Read();
    if (session is null)
    {
        // THE SAME WORDS `gg runner up` USES, because this does the same thing:
        // a hand-flight registers a runner on this machine, and registering a
        // runner is a person's action.
        return Fail("not signed in — run `gg login` first. Registering a runner is a person's action.");
    }

    var baseAddress = ControlPlaneAddress();
    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
    var client = new ControlPlaneClient(http);
    var commands = new FlightCommands(client, new FileSessionStore());

    var labels = (Environment.GetEnvironmentVariable("GG_RUNNER_LABELS") ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    return await FlyByHandCommand.RunAsync(
        fly,
        plan: async token => await client.GetPlanAsync(session.SessionToken, token)
            ?? throw new InvalidOperationException(
                "the control plane served no plan, so what this flight would need is unknown"),
        // WHAT THIS MACHINE ADVERTISES. The plan prices against the fleet, and a
        // label some other runner has is useless to a person at this keyboard.
        advertised: labels,
        open: _ => commands.FlyAsync(
            fly.Text, fly.Uri, provider: fly.Provider, id: fly.Id, repository: fly.Repository),
        hold: (flightId, token) => HoldAsync(baseAddress, http, session, labels, flightId, token),
        say: Console.WriteLine,
        // THE PERSON'S SESSION, ON A DOOR THAT ANSWERS TO ONE. Rule 8: the
        // launcher answers gates and the attended runner never does - and the
        // reason it never does is here rather than in a check, because this is
        // the process holding the session and the runner holds a runner token
        // it was handed. Neither is interchangeable with the other.
        gates: async flightNumber =>
        [
            .. (await client.GatesAsync(session.SessionToken)).Gates
                .Where(gate => string.Equals(
                    gate.FlightNumber, flightNumber, StringComparison.Ordinal)),
        ],
        answer: new ConsoleGateAnswer(),
        decide: async (flightNumber, obligation, outcome, reason) =>
        {
            // MEASURED, NOT CLAIMED. The observations say a person was asked
            // interactively and shown the evidence, and both are true HERE in a
            // way they are not on a scripted call - the gate was rendered to a
            // terminal a person was sitting at, seconds ago.
            var recorded = await commands.DecideAsync(
                flightNumber, obligation, outcome,
                new DecisionObservations { Interactive = true, EvidenceRendered = true },
                reason);

            return recorded is not null;
        });
}

/// <summary>
/// Runs the attended runner for one named flight, and goes home after it.
/// </summary>
/// <remarks>
/// <b>Its own credential slot, under the machine's name plus a suffix.</b>
/// <c>FileRunnerStore.PathFor</c> keys a runner's identity by name, so a
/// hand-flight sharing the fleet runner's slot would have the two overwrite
/// each other's token - and read-or-register keeps one host from appearing as
/// eleven runners, which is the defect that mechanism exists for.
/// </remarks>
static async Task<int> HoldAsync(
    string baseAddress, HttpClient http, StoredSession session,
    IReadOnlyList<string> labels, string flightId, CancellationToken cancellationToken)
{
    // NAMED, NOT SILENT. FromEnvironment answers null for an unconfigured
    // machine and the fleet runner treats that as "this host has no agent" - on
    // a hand-flight there is a person waiting at a terminal for one, so it is
    // said rather than discovered as a session that never starts.
    if (Environment.GetEnvironmentVariable(
            Gg.Runner.Execution.ExecutorConfiguration.BinaryVariable) is not { Length: > 0 } binary)
    {
        return Fail(
            $"this machine declares no agent — set {Gg.Runner.Execution.ExecutorConfiguration.BinaryVariable} "
          + "to the binary you want handed the flight.");
    }

    var name = Gg.Client.AttendedRunner.NameFor(Environment.MachineName);

    var registered = await RunnerIdentity.EnsureAsync(
        new FileRunnerStore(FileRunnerStore.PathFor(name)),
        async () =>
        {
            var fresh = await new ControlPlaneClient(http)
                .RegisterRunnerAsync(session.SessionToken, name);

            return new StoredRunner
            {
                RunnerId = fresh.RunnerId,
                RunnerToken = fresh.RunnerToken,
                // THE SAME THIRTY DAYS the fleet runner's slot gets. A shorter
                // life here would make a person re-register on a machine they
                // hand-fly from weekly, which is the friction read-or-register
                // exists to remove.
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            };
        },
        DateTimeOffset.UtcNow);

    return await Gg.Runner.RunnerHost.RunAsync(
        new Uri(baseAddress), registered.RunnerId, registered.RunnerToken, labels,
        TimeSpan.Zero,
        new LocalCredentialResolver(new FileCredentialStore()),
        new Gg.Runner.Workspace(
            Gg.Runner.Vcs.VcsConfiguration.FromEnvironment(), new Gg.Runner.Vcs.WorkingTreeRoot()),
        cancellationToken,
        destinations: Gg.Runner.Vcs.DestinationConfiguration.FromEnvironment(
            api => new HttpClient { BaseAddress = new Uri(api) }),
        // THE OTHER EXECUTOR, and the only line that decides a person rather
        // than an agent does the work. The SAME binary the fleet runs, from the
        // same variable: a hand-flight and a fleet flight run the same agent,
        // and a second way to name it would be a second answer to which one
        // this machine has.
        executor: new Gg.Runner.Execution.AttendedExecutor(
            binary,
            Gg.Local.IntentConfiguration.FromEnvironment(),
            secretFor: locator => new FileCredentialStore().Read(locator),
            self: Gg.Local.SelfInvocation.Current),
        flightId: flightId,
        // THE ONE READER, HANDED ACROSS. Gg.Runner cannot see Gg.Client - the
        // runner is treated as hostile and the reference graph keeps them
        // apart - so this project, which is the only one that sees both, passes
        // it. A second implementation of "what did the person decide" would be
        // a second size bound and a second set of three diagnoses to drift from
        // these.
        returns: (tree, flight) => TakeoverReturnReader.Read(
            TakeoverReturnReader.PathIn(tree), flight));
}

static async Task<int> RunnerUpAsync()
{
    var baseAddress = ControlPlaneAddress();

    using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };

    // A POOL MEMBER STARTS HERE, and it has no session because nobody is
    // present when a container is warmed. It finds a single-use nonce in its
    // environment, put there by the resident runner that created it, and
    // exchanges that for an identity of its own.
    //
    // This is the branch that lets a member run a flight at all. Before it,
    // `gg runner up` refused without a session, so the only way to warm a
    // working member was to bake a developer's session into an image - which
    // lasts twelve hours and carries their whole surface.
    if (Environment.GetEnvironmentVariable(
            Gg.Runner.Pools.MemberBootstrap.NonceVariable) is { Length: > 0 } nonce)
    {
        return await MemberUpAsync(http, baseAddress, nonce);
    }

    var session = new FileSessionStore().Read();
    if (session is null)
    {
        return Fail("not signed in — run `gg login` first. Registering a runner is a person's action.");
    }

    // A person registers the runner; the runner then holds only the credential
    // that comes back. The developer session never reaches the runner process.
    //
    // READ-OR-REGISTER, the same decision `gg runner maintain` makes. This used
    // to register unconditionally on every start, so one host showed as eleven
    // runners in `gg runners` with ten of them permanently offline - one per
    // restart - and a machine could not come back from a reboot without
    // somebody signed in.
    var registered = await RunnerIdentity.EnsureAsync(
        new FileRunnerStore(FileRunnerStore.PathFor(Environment.MachineName)),
        async () =>
        {
            var fresh = await new ControlPlaneClient(http)
                .RegisterRunnerAsync(session.SessionToken, Environment.MachineName);

            return new StoredRunner
            {
                RunnerId = fresh.RunnerId,
                RunnerToken = fresh.RunnerToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            };
        },
        DateTimeOffset.UtcNow);

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
    // WHERE A TOOL SERVER'S CREDENTIAL COMES FROM, and the only place this
    // process hands one over. The same store `gg credential add` writes; the
    // secret goes into the server's own environment and never into the agent's.
    var executor = Gg.Runner.Execution.ExecutorConfiguration.FromEnvironment(
        secretFor: locator => new FileCredentialStore().Read(locator));

    return await Gg.Runner.RunnerHost.RunAsync(
        new Uri(baseAddress), registered.RunnerId, registered.RunnerToken, labels, holdFor,
        new LocalCredentialResolver(new FileCredentialStore()), workspace, stopping.Token,
        destinations: destinations, executor: executor);
}

/// <summary>
/// Serve one tracker's work items to the agent that started this process.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE CREDENTIAL IS RESOLVED HERE, WHICH IS THE WHOLE POINT.</b> An
/// external tool server can only be handed a secret through the config that
/// launches it, and that config is an argument to the agent - readable by every
/// <c>ps</c> on the host. This process is handed a NAME instead and reads the
/// store itself, so the secret exists only in this address space and only after
/// the agent has already started us.
/// </para>
/// <para>
/// <b>An unresolvable credential is a tool that says so, not a process that
/// dies.</b> A server that exits before its first line is a server the agent
/// reports as never initialized, and the reason - an expired credential on this
/// host - would reach nobody. So the source is built with no secret, the
/// tracker answers 401, and the agent is told a sentence it can stop on.
/// </para>
/// <para>
/// <b>Nothing here prints.</b> Stdout is the protocol; the resolution failure
/// above is exactly the kind of thing that wants a log line, and must not have
/// one.
/// </para>
/// </remarks>
static async Task<int> RunnerReadAsync(CliAction.RunnerRead read)
{
    string? secret = null;

    if (read.Credential is { Length: > 0 } locator)
    {
        var resolved = await new LocalCredentialResolver(new FileCredentialStore())
            .ResolveAsync(
                new Gg.Contracts.CredentialReference
                {
                    Kind = "local",
                    Locator = locator,
                    Identity = read.Provider,
                    Scopes = ["read"],
                });

        secret = resolved is Gg.Runner.CredentialResolution.Resolved granted ? granted.Secret : null;
    }

    using var client = new HttpClient();

    return await WorkItemToolServer.RunAsync(
        System.Console.In,
        System.Console.Out,
        new Gg.Runner.Intent.WiqlWorkItemSource(read.Host, secret, client));
}

/// <summary>
/// A pool member coming up: redeem the nonce, then run as an ordinary runner.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read-or-redeem, the same decision every runner makes.</b> A container
/// restarts, and a member that redeemed on every start would find its nonce
/// spent and never come up again — so a stored credential answers whenever
/// there is one, and the nonce is only for the first breath.
/// </para>
/// <para>
/// <b>The labels come from the credential, not from the environment.</b> What a
/// member may advertise was decided control-plane-side from the strategy; a
/// variable saying the same thing would be a second source of truth for the one
/// value that must not be a runner's to choose.
/// </para>
/// </remarks>
static async Task<int> MemberUpAsync(HttpClient http, string baseAddress, string nonce)
{
    // KEYED ON THE CONTAINER, so a restart finds what the first start stored.
    var store = new FileRunnerStore(FileRunnerStore.PathFor(Environment.MachineName));

    StoredRunner identity;
    try
    {
        identity = await RunnerIdentity.EnsureAsync(
            store,
            async () =>
            {
                var issued = await new ControlPlaneClient(http).RedeemMemberAsync(nonce)
                    ?? throw new InvalidOperationException(
                        "this member's nonce buys nothing: it was never minted, it has expired, "
                      + "or it has already been redeemed. A nonce is spent exactly once, and a "
                      + "member cannot mint itself another - the pool has to warm a new one.");

                return new StoredRunner
                {
                    RunnerId = issued.RunnerId,
                    RunnerToken = issued.RunnerToken,
                    Labels = issued.Labels,
                    ExpiresAt = issued.ExpiresAt,
                };
            },
            DateTimeOffset.UtcNow);
    }
    catch (InvalidOperationException refusal)
    {
        // A CLEAN REFUSAL rather than a stack trace. This lands in a
        // container's log, which is where somebody looks when a member is warm
        // and claims nothing.
        return Fail(refusal.Message);
    }

    var holdFor = int.TryParse(
        Environment.GetEnvironmentVariable("GG_RUNNER_HOLD_SECONDS"), out var seconds)
        ? TimeSpan.FromSeconds(seconds)
        : TimeSpan.FromSeconds(10);

    using var stopping = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };

    var workspace = new Gg.Runner.Workspace(
        Gg.Runner.Vcs.VcsConfiguration.FromEnvironment(), new Gg.Runner.Vcs.WorkingTreeRoot());

    var destinations = Gg.Runner.Vcs.DestinationConfiguration.FromEnvironment(
        api => new HttpClient { BaseAddress = new Uri(api) });

    // WHERE A TOOL SERVER'S CREDENTIAL COMES FROM, and the only place this
    // process hands one over. The same store `gg credential add` writes; the
    // secret goes into the server's own environment and never into the agent's.
    var executor = Gg.Runner.Execution.ExecutorConfiguration.FromEnvironment(
        secretFor: locator => new FileCredentialStore().Read(locator));

    return await Gg.Runner.RunnerHost.RunAsync(
        new Uri(baseAddress), identity.RunnerId, identity.RunnerToken, identity.Labels, holdFor,
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

    // THE SAME DECISION `gg runner up` makes, and it is shared now rather than
    // written twice. These two had drifted: this one read-or-registered and
    // that one registered every start, which is how one host became eleven
    // rows in `gg runners`.
    //
    // ON ITS HISTORICAL PATH. This is the only file any version of gg has
    // written, and on a live pool host it holds this service's thirty-day
    // token. Moving it would make the next start find nothing and refuse,
    // because a maintain start without a credential needs a person signed in -
    // and on a pool host nobody is.
    StoredRunner identity;
    try
    {
        identity = await RunnerIdentity.EnsureAsync(
        runners,
        async () =>
        {
            var signedIn = new FileSessionStore().Read();
            if (signedIn is null)
            {
                // NAMES THE CADENCE. "Not signed in" on a host that ran
                // yesterday reads like a broken machine rather than a
                // credential reaching the end of its life. Nothing renews a
                // runner token - the protocol's renew is for a LEASE - so this
                // is a person's action every thirty days, by design.
                throw new InvalidOperationException(
                    "no usable runner credential, and not signed in. A pool host runs on its own "
                  + "runner token, which lasts thirty days and cannot be renewed - so a person signs "
                  + "in once to mint a new one: run `gg login`, then start this again. Registering a "
                  + "runner is a person's action.");
            }

            var fresh = await new ControlPlaneClient(http)
                .RegisterRunnerAsync(signedIn.SessionToken, Environment.MachineName + ":maintain");

            return new StoredRunner
            {
                RunnerId = fresh.RunnerId,
                RunnerToken = fresh.RunnerToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            };
        },
            DateTimeOffset.UtcNow);
    }
    catch (InvalidOperationException refusal)
    {
        // A CLEAN REFUSAL, not a stack trace. This is the every-thirty-days
        // path and it lands on somebody's console; the sentence is the whole
        // point of it.
        return Fail(refusal.Message);
    }

    runnerToken = identity.RunnerToken;

    using var stopping = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };

    var protocol = new Gg.Runner.RunnerProtocolClient(http, runnerToken);
    var adapter = new Gg.Runner.Pools.DockerPoolAdapter(
        new HttpClient { BaseAddress = new Uri(configuration.Endpoint) });

    // NARRATED, because this loop reported nothing at all. A pull point that
    // crash-looped for hours looked exactly like one quietly doing its job, and
    // the pool it manages grew to 196 dead members with nobody told.
    var loop = new Gg.Runner.Pools.MaintainLoop(
        protocol, adapter, new Gg.Runner.SystemClock(), Task.Delay,
        narrate: Console.Error.WriteLine,

        // WHERE A MEMBER ANSWERS TO, which is usually this host's own control
        // plane and is not always: a container's 127.0.0.1 is the container.
        // What it may ADVERTISE is not here - that comes back with the
        // credential it redeems, decided control-plane-side from the strategy.
        controlPlane: Gg.Runner.Pools.MemberBootstrap.ControlPlaneFor(
            baseAddress,
            Environment.GetEnvironmentVariable(
                Gg.Runner.Pools.MemberBootstrap.ReachableAsVariable)));

    return await loop.RunAsync(pool, stopping.Token);
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return ExitCodes.Refused;
}

/// <summary>
/// `gg update`. What shape this install is, and the command that would move it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Synchronous, and that is the rule showing through the signature.</b>
/// There is nothing to await because there is nothing to fetch, write or spawn:
/// gg replaces no binary of its own. When the control plane can say what is
/// current - the one channel independent of the feed - that answer travels in
/// as a value, so this stays a function of facts rather than a thing that
/// reaches.
/// </para>
/// <para>
/// <b>Exit zero either way.</b> Being behind is reported, never blocking; the
/// protocol floor already refuses with a 426 and that stays the only thing that
/// does. A non-zero exit here turns "there is a newer gg" into a failed build
/// on somebody else's machine.
/// </para>
/// </remarks>
static async Task<int> UpdateReportAsync(bool json)
{
    // THE ONE CHANNEL THAT IS NOT THE FEED. Asking nuget.org what is current
    // would be asking the party a stolen key lets lie: `dotnet tool update`
    // with no version takes whatever was pushed last, and repository signing
    // proves the pipeline rather than the publisher.
    //
    // CurrentVersionAsync returns null for every way this can fail, on purpose,
    // and UpdateAdvice renders null as an absence rather than as currency. So
    // the control plane being down costs a person the ANSWER and never gives
    // them a wrong one.
    using var http = new HttpClient { BaseAddress = new Uri(ControlPlaneAddress()) };
    var current = await new ControlPlaneClient(http).CurrentVersionAsync();

    var advice = Gg.Local.UpdateAdvice.For(Gg.Local.InstallShape.Current, current);

    if (json)
    {
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
            new UpdateReportJson(
                advice.Shape.Kind.ToString(),
                advice.Shape.ToolPath,
                GgVersions.Binary,
                advice.Current,
                advice.Summary,
                [.. advice.Commands]),
            UpdateJsonContext.Default.UpdateReportJson));

        return ExitCodes.Ok;
    }

    Console.WriteLine(advice.Summary);

    foreach (var command in advice.Commands)
    {
        Console.WriteLine();
        Console.WriteLine("  " + command);
    }

    return ExitCodes.Ok;
}

/// <summary>What `gg update --json` emits.</summary>
/// <param name="Shape">Which install this is.</param>
/// <param name="ToolPath">Where the tool lives, when it is one.</param>
/// <param name="Installed">The version running now.</param>
/// <param name="Current">What is current, or null where that could not be established.</param>
/// <param name="Summary">The same sentence the text form prints.</param>
/// <param name="Commands">What to run, which may legitimately be empty.</param>
internal sealed record UpdateReportJson(
    string Shape,
    string? ToolPath,
    string Installed,
    string? Current,
    string Summary,
    IReadOnlyList<string> Commands);

/// <summary>
/// Source-generated, because everything here must stay AOT-publishable.
/// </summary>
[System.Text.Json.Serialization.JsonSourceGenerationOptions(
    PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[System.Text.Json.Serialization.JsonSerializable(typeof(UpdateReportJson))]
internal sealed partial class UpdateJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
