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
    CliAction.Doctor doctor => await DoctorAsync(doctor.Json),

    CliAction.Unknown unknown => Fail(unknown.Message),
    _ => Fail("unhandled action"),
};

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
        new ControlPlaneClient(http), new FileSessionStore(), new Uri(baseAddress)).RunAsync();

    var result = new VerbResult.Diagnosis(report);
    Console.WriteLine(json ? VerbOutput.ToJson(result) : VerbOutput.ToText(result));

    return report.ExitCode;
}

static int LaunchConsole()
{
    var initial = new AppState
    {
        Flights = ["flight-001 · stub", "flight-002 · stub", "flight-003 · stub"],
        Notes = "Notes live in the model, not the view.\nPress e to edit them in $EDITOR.",
    };

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

    return await Gg.Runner.RunnerHost.RunAsync(
        new Uri(baseAddress), registered.RunnerId, registered.RunnerToken, labels, holdFor, stopping.Token);
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 64;
}
