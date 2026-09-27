using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// What earlier flights learned about this place reaches the agent working in it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The feature had both ends and no middle until this.</b> A rehearsal could hand
/// advice back, a person could approve it, and it landed on the tenant's root — and
/// nothing ever read it. There were ZERO non-test readers of
/// <see cref="Envelope.Learned"/>, so every flight after an approved rehearsal
/// started exactly as ignorant as the one before. This is the first reader.
/// </para>
/// <para>
/// <b>Carried on the lease as rendered text, on <see cref="LeaseLoop.Instructions"/>'s
/// own terms.</b> The runner never sees an envelope: the control plane composes and
/// renders, so there is one place that decides what a flight is told. Filtering is
/// the same decision — advice is keyed by what it was learned against, and which
/// entries this flight is owed is a question about the flight, which the control
/// plane knows and a runner does not.
/// </para>
/// <para>
/// <b>Fenced exactly as a nomination note is, and for a stronger reason.</b> This
/// text was drafted by a MACHINE and approved by a person; it is not the operator's
/// policy and must not read as it, or a later flight cannot tell reviewed advice from
/// the rules it is judged against. So: after the instructions, attributed, and saying
/// plainly that it grants nothing.
/// </para>
/// <para>
/// <b>And it is what makes carrying advice forward possible at all.</b> A rehearsal
/// replaces the entry for its subject wholesale, so one that learns two things where
/// the last learned four would delete the difference. It can only carry the rest
/// forward if it can SEE the rest, which before this it could not.
/// </para>
/// </remarks>
public class WhatThisPlaceIsLikeReachesThePromptTests
{
    private const string Advice =
        "node_modules is absent at checkout; npm install takes about two minutes.\n"
      + "jsdom performs no layout, so a margin assertion proves nothing here.";

    private static ExecutorRequest ARequest(string? learned, string? instructions = null) => new()
    {
        WorkingDirectory = "/tmp/tree",
        LoopId = "implement",
        IntentUri = "https://forge.example/acme/widgets/issues/1",
        Learned = learned,
        Instructions = instructions,
        Moves = [LoopMoves.Read, LoopMoves.Edit],
        WallClock = TimeSpan.FromMinutes(20),
        TranscriptPath = "/tmp/transcript.ndjson",
    };

    [Test]
    public async Task The_advice_reaches_the_prompt_verbatim()
    {
        var prompt = ClaudeCodeExecutor.PromptFor(ARequest(Advice));

        await Assert.That(prompt).Contains(Advice)
            .Because("a person approved these words; a runner that summarised them would "
                   + "be re-deciding what was reviewed.");
    }

    [Test]
    public async Task It_says_a_person_approved_it_and_that_it_grants_nothing()
    {
        var prompt = ClaudeCodeExecutor.PromptFor(ARequest(Advice));

        await Assert.That(prompt).Contains("---");

        await Assert.That(prompt).Contains("do not change what you are allowed to do")
            .Because("the wording the nomination note already uses. Advice that reads as "
                   + "permission is the failure this path is most able to cause: it was "
                   + "drafted by a machine, and it arrives in every later flight.");

        await Assert.That(prompt).Contains("approved")
            .Because("a person is the only reason these words are here rather than in a "
                   + "proposal, and an agent that cannot tell reviewed advice from an "
                   + "unreviewed suggestion has no way to weigh it.");
    }

    [Test]
    public async Task The_operators_instructions_come_first_and_the_advice_after()
    {
        // ORDER IS THE RANKING, and asserted on positions because a membership
        // check passes on a prompt that put them the other way round.
        var instructions = "\n\nThe operator's standing instructions for this work.";
        var prompt = ClaudeCodeExecutor.PromptFor(ARequest(Advice, instructions));

        await Assert.That(prompt.IndexOf("standing instructions", StringComparison.Ordinal))
            .IsLessThan(prompt.IndexOf(Advice, StringComparison.Ordinal))
            .Because("advice read above the operator's policy would be a machine's draft "
                   + "arriving with the standing of reviewed policy.");
    }

    [Test]
    public async Task A_place_nobody_has_learned_anything_about_has_an_unchanged_prompt()
    {
        var withAdvice = ClaudeCodeExecutor.PromptFor(ARequest(Advice));
        var without = ClaudeCodeExecutor.PromptFor(ARequest(learned: null));

        await Assert.That(without).DoesNotContain("approved")
            .Because("a tenant that has approved no advice must read exactly as it did "
                   + "before this existed - a heading over nothing is a prompt telling an "
                   + "agent something was learned when nothing was.");

        await Assert.That(without.Length).IsLessThan(withAdvice.Length);
    }

    [Test]
    public async Task The_lease_carries_it_so_one_place_decides_what_a_flight_is_told()
    {
        // THE MEMBER ITSELF, asserted because the runner must not compose this. The
        // control plane holds the estate, composes the envelope and knows which
        // entries match this flight's subject; a runner that filtered would be a
        // second answer to that question, free to disagree with the first.
        var loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read],
            WallClockSeconds = 1200,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            Learned = Advice,
        };

        await Assert.That(loop.Learned).IsEqualTo(Advice);
    }
}
