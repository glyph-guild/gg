using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// <b>S69.6-01</b> - history lists the <c>.proposed</c> records with their plan's state and the
/// mux's sessions, newest first; opening a Claude Code session resumes it as a new tab by the id the
/// mux gave it.
/// </summary>
public class HistoryRevisitsProposalsAndSessionsTests
{
    private static BoardPage Plan(params (string Subject, string State, string? Ending)[] legs) => new()
    {
        IncludedEnded = true,
        Nominations = [.. legs.Select(leg => new NominationSummary
        {
            NominationId = Guid.NewGuid(),
            Nominator = "person",
            Subject = leg.Subject,
            Version = "1",
            WorkKind = "implement",
            Mode = "gated",
            State = leg.State,
            Ending = leg.Ending,
            MadeAt = DateTimeOffset.UnixEpoch,
        })],
    };

    [Test]
    public async Task Proposals_with_their_state_then_sessions_newest_first()
    {
        using var fixture = new MuxFixture();
        fixture.Drafts.KeepProposed("console", new ProposedPlan(
            "ITN-61", "GG-972", ["plan-reviewed"], "d1", DateTimeOffset.Parse("2026-10-07T01:00:00Z")));
        fixture.Drafts.KeepProposed("console-2", new ProposedPlan(
            "ITN-62", "GG-973", ["plan-reviewed"], "d2", DateTimeOffset.Parse("2026-10-07T02:00:00Z")));
        fixture.Ledger.Keep(new MuxSession("old", "Claude Code", "/work/a", DateTimeOffset.Parse("2026-10-06T09:00:00Z")));
        fixture.Ledger.Keep(new MuxSession("new", "plan · console", "/work/b", DateTimeOffset.Parse("2026-10-07T09:00:00Z")));

        var plans = new Dictionary<string, BoardPage>
        {
            ["ITN-61"] = Plan(("a", "ended", "declined"), ("b", "ended", "declined")),
            ["ITN-62"] = Plan(("c", "standing", null), ("d", "standing", null), ("e", "standing", null)),
        };

        var rows = MuxHistory.Rows(fixture.Drafts, fixture.Ledger, itn => plans.GetValueOrDefault(itn));
        var proposals = rows.Where(row => row.Kind == HistoryKind.Proposal).ToList();
        var sessions = rows.Where(row => row.Kind == HistoryKind.Session).ToList();

        await Assert.That(proposals.Select(row => row.Reference!)).IsEquivalentTo(["ITN-62", "ITN-61"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(proposals[0].Text).Contains("ITN-62 · 3 legs: 3 standing · draft console-2");
        await Assert.That(proposals[1].Text).Contains("ITN-61 · 2 legs: 2 declined")
            .Because("the plan's state is read now, not remembered from when it was proposed.");
        await Assert.That(sessions.Select(row => row.Reference!)).IsEquivalentTo(["new", "old"], TUnit.Assertions.Enums.CollectionOrdering.Matching);

        await Assert.That(MuxHistory.Describe("ITN-62", plans["ITN-62"])).Contains("c · implement · standing");
    }

    [Test]
    public async Task A_claude_code_session_is_started_with_an_id_kept_in_the_ledger()
    {
        // A STAND-IN NAMED claude, so the mux treats it as Claude Code: it prints what it was given.
        var bin = Directory.CreateTempSubdirectory("gg-claude-").FullName;
        var claude = Path.Combine(bin, "claude");
        File.WriteAllText(claude, "#!/bin/sh\necho \"args: $*\"\nwhile :; do sleep 1; done\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(claude, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        // WIDE ENOUGH THAT AN ID IS NOT WRAPPED across two of the agent's rows.
        using var fixture = new MuxFixture(columns: 140, agentCommand: claude);
        fixture.Mux.StartClaudeCode(fixture.Directory);

        var kept = fixture.Ledger.Read();
        await Assert.That(kept).Count().IsEqualTo(1);
        await Assert.That(MuxFixture.Until(() =>
                fixture.Mux.Screen(1).Contains($"args: --session-id {kept[0].Id}", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the mux gives the session its id, so history knows it without reading Claude Code's files.");

        fixture.Mux.StartClaudeCode(fixture.Directory, resume: kept[0].Id);
        await Assert.That(MuxFixture.Until(() =>
                fixture.Mux.Screen(2).Contains($"args: --resume {kept[0].Id}", StringComparison.Ordinal)))
            .IsTrue()
            .Because("resuming is a new tab running `claude --resume <id>`.");
        await Assert.That(fixture.Mux.Rows()[1].Label).IsEqualTo("Claude Code · resumed");
    }

    [Test]
    public async Task Enter_on_a_session_in_history_resumes_it_as_a_new_agent()
    {
        var bin = Directory.CreateTempSubdirectory("gg-claude-").FullName;
        var claude = Path.Combine(bin, "claude");
        File.WriteAllText(claude, "#!/bin/sh\necho \"args: $*\"\nwhile :; do sleep 1; done\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(claude, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        using var fixture = new MuxFixture(columns: 120, rows: 20, agentCommand: claude);
        fixture.Ledger.Keep(new MuxSession("s-1", "Claude Code", fixture.Directory, DateTimeOffset.UtcNow));
        fixture.Mux.Reading(fixture.Drafts, _ => null);

        var showing = fixture.Showing(MuxTab.History);
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("Sessions", StringComparison.Ordinal)))
            .IsTrue();

        fixture.Terminal.Type("\r");
        await Assert.That(MuxFixture.Until(() => fixture.Mux.Screen(1).Contains("args: --resume s-1", StringComparison.Ordinal)))
            .IsTrue();

        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg);
    }
}
