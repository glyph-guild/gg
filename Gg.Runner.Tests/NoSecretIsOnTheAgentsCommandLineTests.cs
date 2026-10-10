using Gg.Local;
using Gg.Runner.Execution;
using static Gg.Runner.Tests.McpLaunch;

namespace Gg.Runner.Tests;

/// <summary>
/// S72.5-03: no resolved secret appears on the agent's command line, a tracker
/// reader's included.
/// </summary>
/// <remarks>
/// <b>The configuration used to BE an argument.</b> The tracker reader's
/// secret was written into the JSON handed to <c>--mcp-config</c>, and an
/// argument is readable by every user on the host - the opposite of what the
/// comment beside it claimed. The configuration is now a file only this user
/// can read, and the argument is its path.
/// </remarks>
public class NoSecretIsOnTheAgentsCommandLineTests
{
    private const string TrackerSecret = "the-tracker-secret";

    [Test]
    public async Task Neither_a_tracker_secret_nor_a_servers_secret_is_an_argument()
    {
        var arguments = ClaudeCodeExecutor.ArgumentsFor(
            Request([Hosted()], Uses("sonarqube"), provider: "a-tracker"),
            IntentConfiguration.FromEnvironment("a-tracker=tracker-mcp|TRACKER_TOKEN=local:acme/board"),
            secret: TrackerSecret,
            secretFor: Resolve);

        var path = ConfigPath(arguments);
        var config = File.ReadAllText(path);
        File.Delete(path);

        await Assert.That(arguments.Any(a => a.Contains(TrackerSecret, StringComparison.Ordinal))).IsFalse()
            .Because("an argument is readable by every user on the host.");
        await Assert.That(arguments.Any(a => a.Contains(Secret, StringComparison.Ordinal))).IsFalse();
        await Assert.That(config).Contains(TrackerSecret)
            .Because("the secret still reaches the reader's own block, in the file.");
        await Assert.That(config).Contains(Secret);
    }
}
