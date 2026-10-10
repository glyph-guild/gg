namespace Gg.Console.Tests;

/// <summary>
/// A plain Claude Code session's row reads the title the session sets for itself - the summary
/// Claude Code writes as the terminal's title - rather than "Claude Code" on every row (owner,
/// 2026-10-09).
/// </summary>
public class APlainSessionIsNamedByItsTitleTests
{
    [Test]
    [Arguments("✳ Basic arithmetic question", "Basic arithmetic question")]
    [Arguments("◑ Fix the bar colour", "Fix the bar colour")]
    [Arguments("Plain words", "Plain words")]
    [Arguments("✳ Claude Code", null)]
    [Arguments("◐ Claude Code", null)]
    [Arguments("", null)]
    [Arguments("✳ ", null)]
    public async Task The_title_loses_its_status_glyph_and_the_default_says_nothing(string title, string? expected)
    {
        await Assert.That(MuxTitle.Of(title)).IsEqualTo(expected);
    }

    [Test]
    public async Task A_plain_session_s_row_reads_its_title_and_a_launched_agent_keeps_its_label()
    {
        var bin = Directory.CreateTempSubdirectory("gg-claude-title-").FullName;
        try
        {
            // A STAND-IN NAMED claude, setting its title as Claude Code does: OSC 0, a glyph first.
            var claude = Path.Combine(bin, "claude");
            File.WriteAllText(claude, "#!/bin/sh\nprintf '\\033]0;\\342\\234\\263 Fix the bar colour\\007ready'\nwhile :; do sleep 1; done\n");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(claude, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            using var fixture = new MuxFixture(agentCommand: claude);
            fixture.Agent("plan · console", "printf '\\033]0;\\342\\234\\263 Some title\\007planning'; while :; do sleep 1; done");
            fixture.Mux.StartClaudeCode(fixture.Directory);

            await Assert.That(MuxFixture.Until(() => fixture.Mux.Rows() is [_, { Label: "Fix the bar colour" }])).IsTrue()
                .Because(string.Join(" | ", fixture.Mux.Rows().Select(r => r.Label)));
            await Assert.That(MuxFixture.Until(() => fixture.Mux.Screen(1).Contains("planning", StringComparison.Ordinal))).IsTrue();
            await Assert.That(fixture.Mux.Rows()[0].Label).IsEqualTo("plan · console")
                .Because("a plan's label names its draft, and FreshDraft reads it back.");
        }
        finally
        {
            Directory.Delete(bin, recursive: true);
        }
    }
}
