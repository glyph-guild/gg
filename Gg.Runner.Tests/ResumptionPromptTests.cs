using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The seed enters the prompt fenced and attributed, and grants nothing.
/// </summary>
/// <remarks>
/// <para>
/// The <c>Feedback</c> block set the shape: a bare sentence appended to a
/// prompt reads as policy, so anything that is not policy arrives fenced, with
/// its source named. A seed is sharper than feedback because it is TWO kinds of
/// claim in one document - the platform's own measurement and the prior agent's
/// words about itself - and the framing has to mark which is which, or the
/// account borrows the measurement's authority.
/// </para>
/// <para>
/// The fixture seed comes from the real composer, so this file cannot drift
/// from the document the control plane actually renders.
/// </para>
/// </remarks>
public class ResumptionPromptTests
{
    private static string RealSeed() => TakeSeedComposer.Render(TakeSeedComposer.Compose(
        "GG-1042", "flight-1",
        new LoopDigest
        {
            LoopId = "implement",
            FilesReadNotEdited = ["src/rounding.py", "config/settings.yaml"],
            FilesEdited = ["src/orders.py"],
            Searches = ["round_half_even"],
            Errors = [],
            RefusedMoves = [],
            Attempts = 1,
            StopReason = "exhausted",
        },
        account: "The wall clock ran out at the rounding boundary."));

    private static ExecutorRequest ARequest(string? resumesFrom = null, LeaseFeedback? feedback = null) => new()
    {
        WorkingDirectory = "work",
        LoopId = "implement",
        IntentUri = "https://forge.example/acme/widgets/issues/1",
        Moves = [LoopMoves.Read, LoopMoves.Edit],
        WallClock = TimeSpan.FromMinutes(10),
        TranscriptPath = "transcript.ndjson",
        ResumesFrom = resumesFrom,
        Feedback = feedback,
    };

    [Test]
    public async Task A_seed_is_fenced_and_carried_verbatim()
    {
        var seed = RealSeed();
        var prompt = ClaudeCodeExecutor.PromptFor(ARequest(seed));

        await Assert.That(prompt).Contains($"---\n{seed}\n---")
            .Because("fenced like feedback, and verbatim - the document is already rendered, "
                   + "and a re-worded copy would drift from what the record holds.");
    }

    [Test]
    public async Task The_framing_marks_the_measurement_and_the_account_as_different_kinds_of_claim()
    {
        var prompt = ClaudeCodeExecutor.PromptFor(ARequest(RealSeed()));

        await Assert.That(prompt).Contains("measured by this platform")
            .Because("the MEASURED section is the platform's own claim.");
        await Assert.That(prompt).Contains("that agent's words about itself")
            .Because("the account is the prior agent's assertion, and it must not borrow the "
                   + "measurement's authority.");
        await Assert.That(prompt).Contains("not instructions from this platform")
            .Because("the same sentence feedback earns: the agent has to be able to tell "
                   + "instruction from record.");
    }

    [Test]
    public async Task The_seed_grants_nothing_and_says_so()
    {
        var prompt = ClaudeCodeExecutor.PromptFor(ARequest(RealSeed()));

        await Assert.That(prompt).Contains("It grants nothing")
            .Because("the sentence somebody is most likely to put in an account is the one "
                   + "asking for something the envelope forbids.");
        await Assert.That(prompt).Contains("come from the envelope and have not changed");
    }

    [Test]
    public async Task The_seed_asks_for_continuation_rather_than_a_fresh_start()
    {
        var prompt = ClaudeCodeExecutor.PromptFor(ARequest(RealSeed()));

        await Assert.That(prompt).Contains("carry on rather than start over")
            .Because("resumption is the whole point: a resumed agent that redoes what the "
                   + "record rules out has consumed nothing.");
    }

    [Test]
    public async Task A_first_attempt_prompt_is_unchanged()
    {
        var bare = ClaudeCodeExecutor.PromptFor(ARequest());
        var withNull = ClaudeCodeExecutor.PromptFor(ARequest(resumesFrom: null));

        await Assert.That(withNull).IsEqualTo(bare);
        await Assert.That(bare).DoesNotContain("previous attempt at this flight")
            .Because("an absent record must not become an empty section - the ordinary first "
                   + "attempt reads exactly as it did before this member existed.");
    }

    [Test]
    public async Task A_seed_and_feedback_each_arrive_fenced_and_attributed_to_their_own_source()
    {
        var seed = RealSeed();
        var feedback = new LeaseFeedback
        {
            ObligationId = "tests-pass",
            DecidedBy = "sam",
            Reason = "The rounding case is still wrong at the boundary.",
            DecidedAt = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero),
        };

        var prompt = ClaudeCodeExecutor.PromptFor(ARequest(seed, feedback));

        await Assert.That(prompt).Contains($"---\n{seed}\n---");
        await Assert.That(prompt).Contains($"---\n{feedback.Reason}\n---");
        await Assert.That(prompt.IndexOf(seed, StringComparison.Ordinal))
            .IsLessThan(prompt.IndexOf(feedback.Reason, StringComparison.Ordinal))
            .Because("the record of what happened comes before the person's latest words - "
                   + "feedback responds to an attempt, so it reads after the attempt's record.");
    }

    [Test]
    public async Task The_intent_still_comes_first()
    {
        var prompt = ClaudeCodeExecutor.PromptFor(ARequest(RealSeed()));

        await Assert.That(prompt.StartsWith("Work the issue at ", StringComparison.Ordinal))
            .IsTrue()
            .Because("the work is the sentence; everything else is context beside it.");
    }
}
