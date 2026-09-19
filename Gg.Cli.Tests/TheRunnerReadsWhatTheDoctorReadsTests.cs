using System.Text.RegularExpressions;
using Gg.Runner.Execution;

namespace Gg.Cli.Tests;

/// <summary>
/// The agent, the destinations and the trackers a runner uses are the ones the
/// doctor reports - from the environment, and then from the machine's own file.
/// </summary>
/// <remarks>
/// <para>
/// <b>The doctor said green, and every flight ran with no agent.</b> The doctor
/// resolves <c>executor-binary</c> through <c>Settings</c>, so a person who
/// followed its own advice - <i>"Run `gg config set executor-binary &lt;path&gt;`"</i>
/// - was told the machine was ready. <c>gg runner up</c> read
/// <c>GG_EXECUTOR_BINARY</c> from its process environment and nothing else, so
/// the value in the file reached the doctor and never reached a flight. The same
/// was true of <c>destination-apis</c>, on the runner and on a hand-flight.
/// </para>
/// <para>
/// <b>And the guard beside this one could not see it.</b>
/// <c>AConfigurableValueReachesTheDoctorTests</c> refuses a helper called with
/// NO arguments, because argumentless is how it falls back to the environment.
/// These calls passed <c>secretFor:</c> and fell back all the same. What matters
/// is whether the declaration is passed, not whether anything is - so that is
/// what this checks, per helper, by the parameter that carries it.
/// </para>
/// </remarks>
public class TheRunnerReadsWhatTheDoctorReadsTests
{
    private static string ProgramText()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);

        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "Gg.Cli")))
        {
            here = here.Parent;
        }

        return File.ReadAllText(Path.Combine(here!.FullName, "Gg.Cli", "Program.cs"));
    }

    /// <summary>Each helper that falls back to the environment, and the argument that stops it.</summary>
    private static readonly (string Call, string Declaration)[] Helpers =
    [
        ("ExecutorConfiguration.FromEnvironment", "declaration:"),
        ("ExecutorConfiguration.AgentFromEnvironment", "declaration:"),
        ("DestinationConfiguration.FromEnvironment", "apis:"),
    ];

    [Test]
    public async Task Every_runner_the_root_composes_is_handed_what_the_file_says()
    {
        var text = ProgramText();
        var bypassing = new List<string>();

        foreach (var (call, declaration) in Helpers)
        {
            // BALANCED, because the arguments hold lambdas: a first-`)` match
            // stops inside `Read(locator)` and would miss a declaration passed
            // after it.
            var calls = Regex.Matches(
                text,
                Regex.Escape(call) + @"\((?<args>(?>[^()]+|\((?<depth>)|\)(?<-depth>))*(?(depth)(?!)))\)",
                RegexOptions.Singleline);

            await Assert.That(calls.Count).IsGreaterThan(0)
                .Because($"the scan must find {call} at all, or it proves nothing.");

            bypassing.AddRange(calls
                .Where(m => !m.Groups["args"].Value.Contains(declaration, StringComparison.Ordinal))
                .Select(m => $"{call}({Regex.Replace(m.Groups["args"].Value, @"\s+", " ").Trim()})"));
        }

        await Assert.That(bypassing).IsEmpty()
            .Because("a helper not handed its declaration reads the environment and never the "
                   + "file, while the doctor reads both - so the doctor reports a machine the "
                   + "runner is not. Found: " + string.Join(" | ", bypassing));
    }

    [Test]
    public async Task An_agent_named_only_in_the_file_is_the_agent_a_flight_runs()
    {
        // THE BEHAVIOUR THE SCAN STANDS FOR: a declaration handed in is the one
        // used, with nothing in this process's environment.
        var executor = ExecutorConfiguration.FromEnvironment(declaration: "/opt/agents/claude");
        var agent = ExecutorConfiguration.AgentFromEnvironment(declaration: "/opt/agents/claude");

        await Assert.That(executor).IsNotNull()
            .Because("an executor-binary written in config.json is the agent this machine has.");
        await Assert.That(agent).IsNotNull();
    }

    /// <summary>The variables whose file value the doctor reports, spelled as the root names them.</summary>
    private static readonly string[] Declarations =
    [
        "ExecutorConfiguration.BinaryVariable",
        "ExecutorDeclaration.Variable",
        "DestinationConfiguration.ApisVariable",
    ];

    [Test]
    public async Task No_runner_path_reads_the_agent_from_its_environment_alone()
    {
        // THE SAME DEFECT WITHOUT A HELPER. A hand-flight decided whether this
        // machine had an agent at all, and runner up whether a console could
        // start its login, by reading the variable straight from the process -
        // so a machine the doctor calls ready told a person "this machine
        // declares no agent".
        var text = ProgramText();

        var direct = Declarations
            .SelectMany(variable => Regex.Matches(
                    text,
                    @"Environment\.GetEnvironmentVariable\(\s*(?:[\w.]+\.)?" + Regex.Escape(variable) + @"\s*\)",
                    RegexOptions.Singleline)
                .Select(m => Regex.Replace(m.Value, @"\s+", " ")))
            .ToList();

        await Assert.That(direct).IsEmpty()
            .Because("Settings.Value reads the environment and then the file, which is what the "
                   + "doctor reports; the environment alone is half of it. Found: "
                   + string.Join(" | ", direct));
    }
}
