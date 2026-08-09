using System.Diagnostics;
using System.Reflection;
using Gg.Cli;
using Gg.Console;

return CliArgs.Parse(args) switch
{
    CliAction.LaunchConsole => LaunchConsole(),
    CliAction.PrintVersion => PrintVersion(),
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
    var version = typeof(CliArgs).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
    Console.WriteLine($"gg {version}");
    return 0;
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
