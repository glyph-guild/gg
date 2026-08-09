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
    CliAction.RunnerUp => RunnerUp(),
    CliAction.RunnerServe => Gg.Runner.RunnerHost.Run(),
    CliAction.Unknown unknown => Fail(unknown.Message),
    _ => Fail("unhandled action"),
};

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
    var baseAddress = Environment.GetEnvironmentVariable("GG_CONTROL_PLANE")
        ?? "http://localhost:5199";
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

static int RunnerUp()
{
    var executable = Environment.ProcessPath
        ?? throw new InvalidOperationException("cannot resolve own executable path");

    // Deliberately a separate OS process: the console acts as the developer,
    // the runner is treated as hostile, and the OS keeps them apart.
    var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
    startInfo.ArgumentList.Add("runner");
    startInfo.ArgumentList.Add("serve");

    var child = Process.Start(startInfo)
        ?? throw new InvalidOperationException("failed to start runner process");
    Console.WriteLine($"gg-runner started as pid {child.Id} (separate process — check: ps -p {child.Id})");
    return 0;
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 64;
}
