using Gg.Contracts;
using Gg.Runner.Execution;
using Gg.Runner.Facts;

namespace Gg.Runner.Tests;

/// <summary>
/// Every launch of the loop's agent is recorded as a <c>loop.model</c> fact,
/// and a launch is addressable so it can be started over (slice seventy-six).
/// </summary>
/// <remarks>
/// <para>
/// <b>The owner's words:</b> <i>"we need some of those out of scope things
/// (model flag, runner fact)."</i> The runner is the only side that can say
/// which flag each launch was given; the control plane records what was asked
/// and the transcript what answered.
/// </para>
/// <para>
/// <b>The session id is chosen here, not read back.</b> A relaunch resumes the
/// session by id, and an id the runner picked is one it has before the child
/// says anything - so a child killed before its init record is still
/// resumable, and a test can name it.
/// </para>
/// </remarks>
public class EveryLaunchSaysWhichModelTests
{
    private static ExecutorRequest Request(string? model) => new()
    {
        WorkingDirectory = "/work/flight",
        LoopId = "investigate",
        Moves = [LoopMoves.Read],
        IntentUri = "https://example.invalid/work/1",
        WallClock = TimeSpan.FromMinutes(20),
        TranscriptPath = "/work/flight/transcript.ndjson",
        Model = model,
    };

    private static string? After(IReadOnlyList<string> arguments, string flag)
    {
        var at = arguments.ToList().IndexOf(flag);
        return at < 0 ? null : arguments[at + 1];
    }

    [Test]
    public async Task A_first_launch_names_its_session_and_resumes_nothing()
    {
        var arguments = ClaudeCodeExecutor.ArgumentsFor(Request("claude-sonnet-5-5"), readers: [], secretFor: _ => null);

        await Assert.That(Guid.TryParse(After(arguments, "--session-id"), out _)).IsTrue()
            .Because("the runner picks the id, so a child killed before it speaks is still "
                   + "resumable. Passed: " + string.Join(" ", arguments));
        await Assert.That(arguments.Contains("--resume")).IsFalse();
        await Assert.That(After(arguments, "--model")).IsEqualTo("claude-sonnet-5-5");
    }

    [Test]
    public async Task A_relaunch_resumes_that_session_on_the_new_model_and_says_so()
    {
        var session = Guid.NewGuid().ToString();
        var arguments = ClaudeCodeExecutor.ArgumentsFor(
            Request("claude-opus-5-5"), readers: [], secretFor: _ => null, resuming: session);

        await Assert.That(After(arguments, "--resume")).IsEqualTo(session);
        await Assert.That(arguments.Contains("--session-id")).IsFalse()
            .Because("a resumed session keeps its id; naming a second would be two ids for one "
                   + "conversation.");
        await Assert.That(After(arguments, "--model")).IsEqualTo("claude-opus-5-5");

        var prompt = After(arguments, "-p") ?? "";
        await Assert.That(prompt).Contains("claude-opus-5-5")
            .And.Contains("moved")
            .Because("the agent is told why it was started over, in a line, rather than "
                   + "handed the whole brief again. Prompt:\n" + prompt);
    }

    [Test]
    public async Task A_move_wakes_the_waiter_once_and_the_same_name_twice_is_not_a_move()
    {
        var moves = new ModelMoves();
        var waiting = moves.NextAsync(CancellationToken.None);
        await Assert.That(waiting.IsCompleted).IsFalse();

        moves.Move("claude-opus-5-5");
        await waiting;
        await Assert.That(moves.Latest).IsEqualTo("claude-opus-5-5");

        var again = moves.NextAsync(CancellationToken.None);
        moves.Move("claude-opus-5-5");
        await Assert.That(again.IsCompleted).IsFalse()
            .Because("every renewal of a moved flight carries the same name, and a signal per "
                   + "renewal would relaunch the child every few minutes.");

        moves.Move("claude-sonnet-5-5");
        await again;
        await Assert.That(moves.Latest).IsEqualTo("claude-sonnet-5-5");
    }

    [Test]
    public async Task Every_launch_crosses_as_its_own_fact_in_order()
    {
        var digested = FactPipeline.Digest(
            FactHygiene.Clean(new GatheredFacts(
            [
                new FactPayload.Model(new LoopModel { LoopId = "investigate", Model = "claude-sonnet-5-5" }),
                new FactPayload.Model(new LoopModel
                {
                    LoopId = "investigate", Model = "claude-opus-5-5", Relaunched = true,
                }),
            ])),
            "a-flight",
            DateTimeOffset.UnixEpoch);

        var launches = digested.Items.Where(f => f.Kind == FactKinds.LoopModel).ToList();
        await Assert.That(launches.Count).IsEqualTo(2);
        await Assert.That(launches[0].Model!.Model).IsEqualTo("claude-sonnet-5-5");
        await Assert.That(launches[0].Model!.Relaunched).IsFalse();
        await Assert.That(launches[1].Model!.Model).IsEqualTo("claude-opus-5-5");
        await Assert.That(launches[1].Model!.Relaunched).IsTrue();
        await Assert.That(launches[0].IdempotencyKey).IsNotEqualTo(launches[1].IdempotencyKey)
            .Because("two launches are two facts, not one fact shipped twice.");
    }
}
