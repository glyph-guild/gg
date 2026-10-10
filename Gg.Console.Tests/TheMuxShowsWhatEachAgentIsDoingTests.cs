using System.Text.Json;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// The mux shows whether each agent is working, waiting for a prompt, or blocked on the person -
/// told by Claude Code's own hooks, given for that launch only through <c>--settings</c> (owner,
/// 2026-10-09).
/// </summary>
public class TheMuxShowsWhatEachAgentIsDoingTests
{
    [Test]
    [Arguments("working", null, AgentActivity.Working)]
    [Arguments("waiting", null, AgentActivity.Waiting)]
    [Arguments("notification", """{"notification_type":"permission_prompt","message":"Claude needs your permission to use Bash"}""", AgentActivity.NeedsYou)]
    [Arguments("notification", """{"message":"Claude needs your permission to use Edit"}""", AgentActivity.NeedsYou)]
    [Arguments("notification", """{"notification_type":"idle_prompt","message":"Claude is waiting for your input"}""", AgentActivity.Waiting)]
    [Arguments("something", null, AgentActivity.None)]
    public async Task Each_hook_says_one_thing(string word, string? input, AgentActivity expected)
    {
        await Assert.That(MuxActivity.Interpret(word, input)).IsEqualTo(expected);
    }

    [Test]
    public async Task A_mark_is_read_back_and_only_inside_the_activity_folder()
    {
        var root = Directory.CreateTempSubdirectory("gg-activity-");
        try
        {
            var folder = Path.Combine(root.FullName, "mux", "activity");
            var mine = Path.Combine(folder, "agent-1");

            await Assert.That(MuxActivity.Read(mine)).IsEqualTo(AgentActivity.None);

            MuxActivity.Mark(mine, "working", null, folder);
            await Assert.That(MuxActivity.Read(mine)).IsEqualTo(AgentActivity.Working);

            var elsewhere = Path.Combine(root.FullName, "not-the-folder");
            MuxActivity.Mark(elsewhere, "working", null, folder);
            await Assert.That(File.Exists(elsewhere)).IsFalse()
                .Because("a hook writes where gg pointed it and nowhere else.");

            MuxActivity.Mark(null, "working", null, folder);
            await Assert.That(MuxActivity.Read(null)).IsEqualTo(AgentActivity.None)
                .Because("outside the mux the hook has nothing to say and says nothing.");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_settings_hook_the_four_events_to_gg_mux_mark()
    {
        var self = new SelfInvocation("/usr/local/bin/gg", ["runner", "tools"]);

        using var settings = JsonDocument.Parse(MuxActivity.Settings(self));
        var hooks = settings.RootElement.GetProperty("hooks");

        string CommandOf(string hookEvent) =>
            hooks.GetProperty(hookEvent)[0].GetProperty("hooks")[0].GetProperty("command").GetString()!;

        await Assert.That(CommandOf("UserPromptSubmit")).IsEqualTo("'/usr/local/bin/gg' 'mux' 'mark' 'working'");
        await Assert.That(CommandOf("PreToolUse")).IsEqualTo("'/usr/local/bin/gg' 'mux' 'mark' 'working'");
        await Assert.That(CommandOf("Stop")).IsEqualTo("'/usr/local/bin/gg' 'mux' 'mark' 'waiting'");
        await Assert.That(CommandOf("Notification")).IsEqualTo("'/usr/local/bin/gg' 'mux' 'mark' 'notification'");
        await Assert.That(hooks.GetProperty("PreToolUse")[0].GetProperty("matcher").GetString()).IsEqualTo("*");
    }

    [Test]
    public async Task A_claude_the_mux_starts_is_given_the_hooks_and_its_own_state_file()
    {
        var root = Directory.CreateTempSubdirectory("gg-activity-launch-");
        try
        {
            // A STAND-IN NAMED claude, because the mux injects only into Claude Code.
            var claude = Path.Combine(root.FullName, "claude");
            File.WriteAllText(claude,
                $"#!/bin/sh\nprintf '%s\\n' \"$@\" > '{root.FullName}/argv'\nprintf '%s' \"${MuxActivity.Variable}\" > '{root.FullName}/env'\nwhile :; do sleep 1; done\n");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(claude, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            using var fixture = new MuxFixture(agentCommand: claude);
            _ = fixture.Mux.StartClaudeCode(root.FullName);

            await Assert.That(MuxFixture.Until(() => File.Exists(Path.Combine(root.FullName, "env"))
                                                    && File.Exists(Path.Combine(root.FullName, "argv")))).IsTrue();
            var argv = File.ReadAllLines(Path.Combine(root.FullName, "argv"));
            var stateFile = File.ReadAllText(Path.Combine(root.FullName, "env"));

            var at = Array.IndexOf(argv, "--settings");
            await Assert.That(at).IsGreaterThanOrEqualTo(0).Because(string.Join(" | ", argv));
            await Assert.That(argv[at + 1]).Contains("mux");
            await Assert.That(stateFile).StartsWith(Path.Combine(fixture.Directory, "mux", "activity"))
                .Because("the state file sits beside the mux's own ledger, never in the person's Claude settings.");

            // THE HOOK'S MARK REACHES THE ROW.
            MuxActivity.Mark(stateFile, "notification", """{"notification_type":"permission_prompt"}""", Path.GetDirectoryName(stateFile)!);
            await Assert.That(MuxFixture.Until(() => fixture.Mux.Rows() is [{ Activity: AgentActivity.NeedsYou }])).IsTrue();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_column_marks_each_row_by_what_its_agent_is_doing()
    {
        MuxRow Row(int n, AgentActivity activity) => new(n, $"agent {n}", TimeSpan.FromMinutes(3), false, activity);

        var lines = MuxColumn.Lines(
            [Row(1, AgentActivity.Working), Row(2, AgentActivity.Waiting), Row(3, AgentActivity.NeedsYou), Row(4, AgentActivity.None)],
            MuxTab.Gg, 16).Select(l => l.Text).ToList();

        await Assert.That(lines[2]).Contains(" 1 » agent 1");
        await Assert.That(lines[3]).Contains(" 2 · agent 2");
        await Assert.That(lines[4]).Contains(" 3 ? agent 3");
        await Assert.That(lines[5]).Contains(" 4   agent 4");
        await Assert.That(lines.All(l => l.Length <= MuxColumn.Width)).IsTrue();
    }
}
