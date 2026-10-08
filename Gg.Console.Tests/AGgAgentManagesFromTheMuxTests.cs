using System.Text.Json;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// <c>g</c> in the column's "+ new agent" menu starts an agent that manages gg through the
/// <c>gg-manage</c> tools: reads granted, acts asked for each time (owner's call, 2026-10-08).
/// </summary>
public class AGgAgentManagesFromTheMuxTests
{
    [Test]
    public async Task G_in_the_menu_starts_a_gg_agent_on_its_prompt_and_tools()
    {
        var root = Directory.CreateTempSubdirectory("gg-manage-agent-");
        var script = Path.Combine(root.FullName, "agent.sh");
        File.WriteAllText(script,
            $"printf '%s\\n' \"$@\" > '{root.FullName}/argv'; while :; do sleep 1; done\n");

        try
        {
            using var fixture = new MuxFixture(columns: 100, rows: 20, agentCommand: $"/bin/sh {script}");

            var showing = fixture.Showing(MuxTab.New);
            await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("manage gg", StringComparison.Ordinal)))
                .IsTrue();
            fixture.Terminal.Type("g");

            var argvPath = Path.Combine(root.FullName, "argv");
            await Assert.That(MuxFixture.Until(() => File.Exists(argvPath) && File.ReadAllLines(argvPath).Length > 3))
                .IsTrue();
            await Assert.That(fixture.Mux.Rows().Select(row => row.Label)).IsEquivalentTo(["gg"]);

            var argv = File.ReadAllLines(argvPath);
            await Assert.That(argv[0]).IsEqualTo(AgentOpening.Manage())
                .Because("the prompt comes before every flag, or a list flag swallows it.");

            var flag = Array.IndexOf(argv, "--mcp-config");
            using var config = JsonDocument.Parse(argv[flag + 1]);
            var server = config.RootElement.GetProperty("mcpServers").GetProperty(ManageTool.Server);
            await Assert.That(server.GetProperty("command").GetString()).IsEqualTo("/usr/local/bin/gg");
            await Assert.That(server.GetProperty("args").EnumerateArray().Select(a => a.GetString()).ToList())
                .IsEquivalentTo((string?[])["manage", "tools"]);

            foreach (var read in ManageTool.Reads)
            {
                await Assert.That(argv).Contains(ManageTool.Qualified(read));
            }

            foreach (var act in ManageTool.Acts)
            {
                await Assert.That(argv).DoesNotContain(ManageTool.Qualified(act))
                    .Because("an act is asked for every time: Claude Code's prompt is the confirmation.");
            }

            fixture.Mux.EndAll();
            await showing.WaitAsync(TimeSpan.FromSeconds(20));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_opening_names_every_tool_and_that_acts_are_asked_for()
    {
        var prompt = AgentOpening.Manage();

        foreach (var tool in ManageTool.Reads.Concat(ManageTool.Acts))
        {
            await Assert.That(prompt).Contains(ManageTool.Qualified(tool));
        }

        await Assert.That(prompt).Contains("only when I say", StringComparison.Ordinal);
        await Assert.That(prompt).DoesNotContain("\n");
    }
}
