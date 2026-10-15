using System.Text.Json;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Runner.Tests;

/// <summary>
/// An earlier finding crosses on the lease with what it rests on (S79.3-01, ADR-0041
/// Decision 9).
/// </summary>
/// <remarks>
/// Structured, because the runner compares each finding's commits with the tree it fetched,
/// and the tree never reaches the control plane.
/// </remarks>
public class AnEarlierFindingCrossesOnTheLeaseTests
{
    private static readonly EarlierFinding GG1134 = new()
    {
        FlightNumber = "GG-1134",
        Kind = "investigate-instructions",
        Chain = ["investigate@v7", "investigate-instructions@v2"],
        FoundAt = new DateTimeOffset(2026, 10, 14, 14, 38, 20, TimeSpan.Zero),
        Text = "<h2>What the item asks</h2><p>Clear the two Web:S6845 issues.</p>",
        Models = ["claude-opus-5-5"],
        Commits = [new FindingCommit { Repository = "JDNext", Commit = "a8717f77362a2604cd3c08342e61a7518c4a9a54" }],
    };

    private static LeaseLoop ALoop(params EarlierFinding[] findings) => new()
    {
        LoopId = "investigate",
        Executor = ExecutorRungs.Frontier,
        Moves = [LoopMoves.Read],
        WallClockSeconds = 600,
        OnExhaustion = ExhaustionPolicies.HandoffToHuman,
        Findings = findings,
    };

    [Test]
    public async Task A_finding_crosses_with_its_text_and_provenance()
    {
        var json = JsonSerializer.Serialize(ALoop(GG1134), RunnerJsonContext.Default.LeaseLoop);
        var read = JsonSerializer.Deserialize(json, RunnerJsonContext.Default.LeaseLoop)!;

        await Assert.That(json).Contains("\"findings\"");
        var finding = read.Findings.Single();
        await Assert.That(finding.FlightNumber).IsEqualTo("GG-1134");
        await Assert.That(finding.Kind).IsEqualTo("investigate-instructions");
        await Assert.That(finding.Chain).IsEquivalentTo(GG1134.Chain);
        await Assert.That(finding.FoundAt).IsEqualTo(GG1134.FoundAt);
        await Assert.That(finding.Text).IsEqualTo(GG1134.Text);
        await Assert.That(finding.Models).IsEquivalentTo(GG1134.Models);
        await Assert.That(finding.Commits.Single().Commit).IsEqualTo(GG1134.Commits[0].Commit);
    }

    [Test]
    public async Task A_control_plane_that_predates_it_sends_none_and_nothing_breaks()
    {
        const string Old = """
            {"loopId":"investigate","executor":"frontier","moves":["read"],
             "wallClockSeconds":600,"onExhaustion":"handoff-to-human"}
            """;

        var read = JsonSerializer.Deserialize(Old, RunnerJsonContext.Default.LeaseLoop)!;

        await Assert.That(read.Findings).IsEmpty();
    }

    [Test]
    public async Task The_surface_declares_it()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(LeaseLoop)]).Contains("findings");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(EarlierFinding)])
            .IsEquivalentTo(new[] { "flightNumber", "kind", "chain", "foundAt", "text", "models", "commits" });
        await Assert.That(ProtocolSurface.JsonMembers[typeof(FindingCommit)])
            .IsEquivalentTo(new[] { "repository", "commit" });
        await Assert.That(Vocabulary.Types).Contains(typeof(EarlierFinding));
        await Assert.That(Vocabulary.Types).Contains(typeof(FindingCommit));
    }
}
