using System.Diagnostics;
using System.Reflection;
using Gg.Cli;
using Gg.Client;
using Gg.Console;

return CliArgs.Parse(args) switch
{
    CliAction.LaunchConsole => LaunchConsole(),
    CliAction.PrintVersion => PrintVersion(),
    CliAction.Login => await AuthAsync(commands => commands.LoginAsync(Environment.MachineName)),
    CliAction.Logout => await AuthAsync(commands => commands.LogoutAsync()),
    CliAction.WhoAmI => await AuthAsync(commands => commands.WhoAmIAsync()),
    CliAction.RunnerUp or CliAction.RunnerServe => await RunnerUpAsync(),

    CliAction.Fly fly => await EmitAsync(fly.Json, c => c.FlyAsync(fly.Text, fly.Uri)),
    CliAction.Flights flights => await EmitAsync(flights.Json, c => c.ListAsync()),
    CliAction.Show show => await EmitAsync(show.Json, c => c.ShowAsync(show.Reference)),
    CliAction.Log log => await EmitAsync(log.Json, c => c.LogAsync(log.Reference)),
    CliAction.Runners runners => await EmitAsync(runners.Json, c => c.RunnersAsync()),
    CliAction.Why why => await EmitAsync(why.Json, c => c.WhyAsync(why.Flight, why.Obligation)),
    CliAction.Gates gates => await EmitAsync(gates.Json, c => c.GatesAsync()),
    CliAction.Doctor doctor => await DoctorAsync(doctor.Json),
    CliAction.Bundle bundle => await BundleAsync(bundle.Json),

    CliAction.EnvelopeShow show => await EnvelopeAsync(show.Json, c => c.ShowAsync()),
    CliAction.EnvelopeApply apply =>
        await EnvelopeAsync(apply.Json, c => c.ApplyAsync(ReadEnvelope(apply.Source))),
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
        return 0;
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
        return 69;
    }
    catch (HttpRequestException failure)
    {
        Console.Error.WriteLine(
            $"Could not reach the control plane at {baseAddress}: {failure.Message}. Try gg doctor.");
        return 69;
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
        return 69;
    }
    catch (HttpRequestException failure)
    {
        Console.Error.WriteLine(
            $"Could not reach the control plane at {baseAddress}: {failure.Message}. Try gg doctor.");
        return 69;
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
    var data = new ConsoleData(
        new FlightCommands(client, sessions),
        // The console can read the credential references and forget one. It
        // cannot add one: that needs a secret typed at a prompt, and a prompt
        // inside a Terminal.Gui modal is a keyboard path with its own
        // escape-hatch rules. Registering stays a command-line act.
        new CredentialCommands(client, sessions, new FileCredentialStore(), new ConsoleSecretPrompt()));

    var initial = ConsoleStart.LoadAsync(data).GetAwaiter().GetResult();

    var final = new ConsoleLoop(new TerminalGuiSession(), new EditorSession()).Run(initial);

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
        return 69;
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

    return await Gg.Runner.RunnerHost.RunAsync(
        new Uri(baseAddress), registered.RunnerId, registered.RunnerToken, labels, holdFor,
        new LocalCredentialResolver(new FileCredentialStore()), workspace, stopping.Token,
        destinations: destinations);
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 64;
}
