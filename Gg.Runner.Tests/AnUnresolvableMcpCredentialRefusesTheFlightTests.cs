using Gg.Local;
using Gg.Runner.Execution;
using static Gg.Runner.Tests.McpLaunch;

namespace Gg.Runner.Tests;

/// <summary>
/// S72.5-02: a credential reference this machine cannot resolve refuses the
/// flight before anything is spent.
/// </summary>
/// <remarks>
/// A server started without its credential fails at its service with an
/// authentication error nobody can trace back to a missing credential here,
/// and the agent spends its turns calling it. The tracker reader already
/// refuses this way; an external server is no different.
/// </remarks>
public class AnUnresolvableMcpCredentialRefusesTheFlightTests
{
    private const string NoSuchBinary = "/nonexistent/gg-test/claude";

    [Test]
    public async Task The_flight_is_refused_naming_the_server_and_the_locator()
    {
        var run = await new ClaudeCodeExecutor(NoSuchBinary, readers: [], secretFor: _ => null)
            .ExecuteAsync(Request([Hosted()], Uses("sonarqube")), CancellationToken.None);

        await Assert.That(run.Outcome).IsEqualTo(Gg.Contracts.LoopOutcomes.Failed);
        await Assert.That(run.Reason).Contains("sonarqube");
        await Assert.That(run.Reason).Contains(Locator);
        await Assert.That(run.Reason).DoesNotContain("could not start")
            .Because("refused before the executor was started, which is what 'nothing was spent' means.");
    }

    [Test]
    public async Task A_server_under_the_tracker_readers_key_is_refused()
    {
        // Two servers under one key in one configuration: the agent would get
        // whichever was written second, and the other's tools would vanish.
        var run = await new ClaudeCodeExecutor(
                NoSuchBinary,
                IntentConfiguration.FromEnvironment("sonarqube=tracker-mcp"),
                secretFor: Resolve)
            .ExecuteAsync(Request([Hosted()], Uses("sonarqube"), provider: "sonarqube"), CancellationToken.None);

        await Assert.That(run.Outcome).IsEqualTo(Gg.Contracts.LoopOutcomes.Failed);
        await Assert.That(run.Reason).Contains("sonarqube");
        await Assert.That(run.Reason).DoesNotContain("could not start");
    }
}
