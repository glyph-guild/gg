using Gg.Contracts;
using Gg.Runner.Execution;
using static Gg.Runner.Tests.McpLaunch;

namespace Gg.Runner.Tests;

/// <summary>
/// S72.5-01: the runner starts the MCP servers a loop names, with the
/// credentials their definitions reference resolved on this machine (ADR-0040).
/// </summary>
/// <remarks>
/// <para>
/// <b>Passed through as written.</b> The runner does not know what an external
/// server is, so the configuration it writes is the definition with exactly two
/// things done to it: each credential reference replaced by its secret, and each
/// allowed tool put on the allow-list.
/// </para>
/// <para>
/// <b>In a file, never on the command line.</b> The configuration carries
/// secrets, and an argument is readable by every user on the host.
/// </para>
/// </remarks>
public class TheRunnerStartsTheLoopsMcpServersTests
{
    [Test]
    public async Task A_remote_server_is_written_with_its_credential_resolved()
    {
        var arguments = ClaudeCodeExecutor.ArgumentsFor(
            Request([Hosted()], Uses("sonarqube")), readers: [], secretFor: Resolve);

        var server = Servers(arguments).GetProperty("sonarqube");

        await Assert.That(server.GetProperty("type").GetString()).IsEqualTo(McpTransports.Http);
        await Assert.That(server.GetProperty("url").GetString()).IsEqualTo("https://api.sonarcloud.io/mcp");
        await Assert.That(server.GetProperty("headers").GetProperty("Authorization").GetString())
            .IsEqualTo("Bearer " + Secret)
            .Because("the reference is replaced with what this machine's store resolves, and the "
                   + "rest of the value is kept as the author wrote it.");
        await Assert.That(server.GetProperty("headers").GetProperty("SONARQUBE_ORG").GetString())
            .IsEqualTo("jdx");
    }

    [Test]
    public async Task A_local_server_gets_its_secret_in_its_own_environment_block()
    {
        var arguments = ClaudeCodeExecutor.ArgumentsFor(
            Request([LocalServer()], Uses("sonar-local")), readers: [], secretFor: Resolve);

        var server = Servers(arguments).GetProperty("sonar-local");

        await Assert.That(server.GetProperty("command").GetString()).IsEqualTo("dnx");
        await Assert.That(server.GetProperty("args").EnumerateArray().Select(a => a.GetString()!))
            .IsEquivalentTo(["mcp-sonarqube@1.1.1", "--yes"]);
        await Assert.That(server.GetProperty("env").GetProperty("SONARQUBE_TOKEN").GetString())
            .IsEqualTo(Secret);
    }

    [Test]
    public async Task Each_allowed_tool_is_on_the_allow_list_by_the_agents_own_name()
    {
        var named = ClaudeCodeExecutor.ArgumentsFor(
            Request([Hosted()], Uses("sonarqube", "search_sonar_issues_in_projects", "show_rule")),
            readers: [], secretFor: Resolve);
        File.Delete(ConfigPath(named));

        await Assert.That(Allowed(named)).Contains("mcp__sonarqube__search_sonar_issues_in_projects");
        await Assert.That(Allowed(named)).Contains("mcp__sonarqube__show_rule");
        await Assert.That(Allowed(named)).DoesNotContain("mcp__sonarqube")
            .Because("the server's name alone allows every tool it has, which this loop did not.");

        var everything = ClaudeCodeExecutor.ArgumentsFor(
            Request([Hosted()], Uses("sonarqube")), readers: [], secretFor: Resolve);
        File.Delete(ConfigPath(everything));

        await Assert.That(Allowed(everything)).Contains("mcp__sonarqube");
    }

    [Test]
    public async Task The_configuration_is_a_file_only_the_runner_can_read()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var arguments = ClaudeCodeExecutor.ArgumentsFor(
            Request([Hosted()], Uses("sonarqube")), readers: [], secretFor: Resolve);
        var path = ConfigPath(arguments);

        try
        {
            await Assert.That(File.Exists(path)).IsTrue()
                .Because("a path, not the JSON: the configuration carries a secret.");
            await Assert.That(File.GetUnixFileMode(path))
                .IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task A_loop_that_names_no_server_starts_none()
    {
        var arguments = ClaudeCodeExecutor.ArgumentsFor(Request([], []), readers: [], secretFor: Resolve);

        await Assert.That(arguments).DoesNotContain("--mcp-config");
        await Assert.That(Allowed(arguments).Where(a => a.StartsWith("mcp__", StringComparison.Ordinal)))
            .IsEmpty();
    }

    [Test]
    public async Task The_lease_carries_them_to_the_executor()
    {
        // THE MIDDLE, which has gone missing before: instructions, the brief and
        // variables each reached the lease and stopped at the runner.
        var request = await ResumptionContextTests.FlyCarryingAsync(new LeaseLoop
        {
            LoopId = "investigate",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read],
            WallClockSeconds = 600,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            McpServers = [Hosted()],
            Mcp = Uses("sonarqube", "show_rule"),
        });

        await Assert.That(request.McpServers.Select(s => s.Key)).IsEquivalentTo(["sonarqube"]);
        await Assert.That(request.Mcp.Single().Allow).IsEquivalentTo(["show_rule"]);
    }
}
