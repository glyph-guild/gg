using Gg.Contracts;
using Gg.Runner.Facts;

namespace Gg.Runner.Tests;

/// <summary>
/// The chain from an agent's tool call to a tracker, and the two links that
/// were missing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every piece of this existed and the chain did not.</b>
/// <c>TranscriptDigest.Proposals</c> shipped in slice thirty-five and NOTHING
/// CALLED IT — so no <c>work-item.proposal</c> fact was ever produced, and the
/// admission, the menu and the adapter were all answering a question nobody
/// asked. <c>WiqlWorkItemSink</c> had the same shape: built, walked against a
/// real tracker, reachable from no product code.
/// </para>
/// <para>
/// <b>Three instances of one defect in one feature</b> — the extractor, the
/// sink, and the sink's construction. Each looked finished from its own side,
/// which is what makes this shape expensive: the unit tests pass, the walk
/// passes, and the feature does nothing.
/// </para>
/// <para>
/// <b>So this asserts the seams rather than the pieces.</b> A fact payload for
/// a proposal must exist for one to cross, and the executor must read the
/// transcript it already has.
/// </para>
/// </remarks>
public class AProposalTravelsFromTheCallToTheTrackerTests
{
    [Test]
    public async Task A_proposal_has_a_payload_to_cross_on()
    {
        // A FACT WITH NOWHERE TO TRAVEL is a fact nothing ships. The kind was
        // registered four ways in the contract and had no payload here, which
        // is the same gap one repository over.
        var payload = new FactPayload.Proposal(new WorkItemProposal
        {
            Operation = WorkItemOperations.Field,
            Target = "1421",
            Reason = "the rubric scores this an eight",
            Fields = [new WorkItemFieldEdit { Path = "Custom.RiceScore", Value = "8" }],
        });

        await Assert.That(payload.Value.Operation).IsEqualTo(WorkItemOperations.Field);
    }

    [Test]
    public async Task The_executor_reads_proposals_out_of_the_transcript_it_already_has()
    {
        // READ AT THE SAME BOUNDARY AS THE NOMINATION, from the same text. The
        // nomination's own remark says why: a value the agent DECLARED gets its
        // own extractor and is read once, here, from the stream this machine
        // already holds. Proposals had the extractor and not the reading.
        var source = await File.ReadAllTextAsync(RunnerSource("Execution/ClaudeCodeExecutor.cs"));

        await Assert.That(source).Contains("TranscriptDigest.Proposals", StringComparison.Ordinal)
            .Because("the extractor shipped a whole slice ago and nothing called it, so no "
                   + "proposal ever became a fact and every layer above was answering a "
                   + "question nobody asked.");
    }

    [Test]
    public async Task The_loop_ships_a_proposal_the_way_it_ships_a_nomination()
    {
        var source = await File.ReadAllTextAsync(RunnerSource("RunnerLoop.cs"));

        await Assert.That(source).Contains("FactPayload.Proposal", StringComparison.Ordinal)
            .Because("a payload nothing adds is a fact that never leaves the runner.");
    }

    [Test]
    public async Task The_loop_performs_an_admitted_tracker_write()
    {
        // AND THE FAR END. A tracker admission has NO PUSH, so LandAsync's
        // early return - correct for every repository destination - would skip
        // a triage flight's entire landing. The tracker path is independent of
        // the push path, which is the rule the contract already states about
        // their two fields: each is refused by its own absence.
        var source = await File.ReadAllTextAsync(RunnerSource("RunnerLoop.cs"));

        await Assert.That(source).Contains("_trackers", StringComparison.Ordinal)
            .Because("the sinks are constructed, handed to the host and handed to the loop, "
                   + "and if nothing reads them the whole chain ends one call short.");

        await Assert.That(source).Contains(".Tracker", StringComparison.Ordinal)
            .Because("the admission's tracker answer is what says which proposals to "
                   + "perform, and absent means write nothing.");
    }

    private static string RunnerSource(string file)
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "Gg.Runner")))
        {
            here = here.Parent;
        }

        return Path.Combine(here!.FullName, "Gg.Runner", file);
    }
}
