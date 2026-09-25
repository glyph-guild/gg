using System.Diagnostics;
using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The variables an envelope declares reach the process the agent runs in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The middle of a feature that had both ends.</b> An envelope may declare
/// <c>variables:</c>, the composer merges them across layers, and apply accepts
/// them. Nothing carried them to a machine. Measured on GG-294: the
/// <c>ui-preview</c> work kind declares <c>PREVIEW_PORT</c>, <c>HUSKY</c> and
/// <c>CI</c>, and the agent reported <i>"that variable is not in my environment
/// and I found no gg config or envelope file on this machine that defines it"</i>
/// — then stopped, because its standing instruction says the port is not its to
/// choose.
/// </para>
/// <para>
/// <b>HUSKY=0 was the reason this field exists.</b> A fresh <c>npm install</c>
/// writes <c>core.hooksPath</c> into the tree's own config, and the first
/// ui-preview flight died on <c>npx: not found</c> in a commit hook nobody
/// asked for. A field that cannot reach the process solves none of that.
/// </para>
/// <para>
/// <b>On the loop, beside the instructions</b>, which is where a runner already
/// reads what governs the work it is about to invoke.
/// </para>
/// </remarks>
public class VariablesReachTheAgentTests
{
    private static ExecutorRequest Asking(params (string Name, string Value)[] variables) => new()
    {
        LoopId = "implement-and-serve",
        WorkingDirectory = "/work/tree",
        Trees = [],
        Moves = [LoopMoves.Read, LoopMoves.Edit],
        WallClock = TimeSpan.FromMinutes(30),
        Task = "make the change",
        TranscriptPath = "/work/transcript.jsonl",
        Variables = [.. variables.Select(v => new EnvelopeVariable { Name = v.Name, Value = v.Value })],
    };

    [Test]
    public async Task They_are_placed_in_the_child_environment()
    {
        var info = new ProcessStartInfo();

        ClaudeCodeExecutor.PlaceVariables(info, Asking(("PREVIEW_PORT", "8080"), ("HUSKY", "0")));

        await Assert.That(info.Environment["PREVIEW_PORT"]).IsEqualTo("8080");
        await Assert.That(info.Environment["HUSKY"]).IsEqualTo("0")
            .Because("a fresh npm install writes core.hooksPath into the tree's own config, and "
                   + "the first ui-preview flight died on 'npx: not found' in a commit hook "
                   + "nobody asked for. This field exists for that.");
    }

    [Test]
    public async Task A_loop_declaring_none_changes_nothing()
    {
        // EVERY ENVELOPE WRITTEN BEFORE THIS declares none, and a runner must
        // behave exactly as it did - the child inherits this process's
        // environment and nothing here adds to or removes from it.
        var info = new ProcessStartInfo();
        var before = info.Environment.Count;

        ClaudeCodeExecutor.PlaceVariables(info, Asking());

        await Assert.That(info.Environment.Count).IsEqualTo(before);
    }

    [Test]
    public async Task They_do_not_displace_the_scratch_directory_or_the_token()
    {
        // ORDER MATTERS AND IS NOT LEFT TO CHANCE. TMPDIR and the agent's token
        // are placed by gg for reasons a tenant document cannot know about, so
        // a document naming either must not win - it would redirect an agent's
        // temp files or blank its credential.
        var info = new ProcessStartInfo();
        info.Environment["TMPDIR"] = "/scratch/gg";
        info.Environment["ANTHROPIC_AUTH_TOKEN"] = "placed-by-gg";

        ClaudeCodeExecutor.PlaceVariables(
            info, Asking(("TMPDIR", "/tmp/theirs"), ("ANTHROPIC_AUTH_TOKEN", "theirs")));

        await Assert.That(info.Environment["TMPDIR"]).IsEqualTo("/scratch/gg");
        await Assert.That(info.Environment["ANTHROPIC_AUTH_TOKEN"]).IsEqualTo("placed-by-gg")
            .Because("an envelope is a git-tracked document readable by everyone who can read "
                   + "the airspace, so a value in one must never replace a credential gg put "
                   + "in the child's environment.");
    }

    [Test]
    public async Task The_lease_carries_them_from_the_envelope()
    {
        // THE HALF THAT WAS MISSING. The composer merges variables across
        // layers and apply accepts them; nothing put them on the lease, so no
        // runner could ever have read one.
        var loop = new LeaseLoop
        {
            LoopId = "implement-and-serve",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read],
            WallClockSeconds = 600,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            Variables = [new EnvelopeVariable { Name = "PREVIEW_PORT", Value = "8080" }],
        };

        await Assert.That(loop.Variables!.Single().Value).IsEqualTo("8080");
    }
}
