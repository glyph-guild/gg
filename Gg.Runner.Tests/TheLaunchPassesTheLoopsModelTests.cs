using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// A loop that names its model is launched on it, headless or attended, and a
/// loop that names none is launched on whatever the account defaults to
/// (slice seventy-five).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the envelope and not the machine.</b> GG-1076 and GG-1081 (ado#18492,
/// 2026-10-11) both ran on the account's default model because nothing in gg
/// or the airspace chose one, and both stopped at the template layer a
/// stronger model is likelier to read past. Which model investigates is a
/// decision about the work, so it is the work kind's to make, in the document
/// a person reviews, rather than an operator's environment variable on one
/// runner that another runner lacks.
/// </para>
/// <para>
/// <b>The flag, not the environment.</b> Claude Code reads <c>--model</c>
/// ahead of <c>ANTHROPIC_MODEL</c>, and a tenant could already place the
/// variable through <c>variables:</c> - this is the same decision carried
/// where a reader of the document can see it, and where the attended launch
/// shares it: a hand flight is the same loop watched.
/// </para>
/// </remarks>
public class TheLaunchPassesTheLoopsModelTests
{
    private static readonly Gg.Local.SelfInvocation Self =
        Gg.Local.SelfInvocation.For("/bin/gg", null)!;

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

    private static string? ModelFlagOf(IReadOnlyList<string> arguments)
    {
        var at = arguments.ToList().IndexOf("--model");
        return at < 0 ? null : arguments[at + 1];
    }

    [Test]
    public async Task A_named_model_is_passed_to_the_headless_launch()
    {
        var arguments = ClaudeCodeExecutor.ArgumentsFor(
            Request("claude-opus-5-5"), readers: [], secretFor: _ => null);

        await Assert.That(ModelFlagOf(arguments)).IsEqualTo("claude-opus-5-5")
            .Because("the loop named it. Passed: " + string.Join(" ", arguments));
    }

    [Test]
    public async Task A_named_model_is_passed_to_the_attended_launch_too()
    {
        var arguments = AttendedExecutor.StartInfoFor(Request("opus"), [], null, Self).ArgumentList;

        await Assert.That(ModelFlagOf(arguments)).IsEqualTo("opus")
            .Because("a hand flight is the same loop watched, and runs on the same model. "
                   + "Passed: " + string.Join(" ", arguments));
    }

    [Test]
    public async Task No_model_means_no_flag()
    {
        var headless = ClaudeCodeExecutor.ArgumentsFor(Request(null), readers: [], secretFor: _ => null);
        var attended = AttendedExecutor.StartInfoFor(Request(null), [], null, Self).ArgumentList;

        await Assert.That(headless.Contains("--model")).IsFalse()
            .Because("absent means the account's default, which only the client knows.");
        await Assert.That(attended.Contains("--model")).IsFalse();
    }

    [Test]
    public async Task The_lease_carries_it_to_the_executor()
    {
        // THE MIDDLE, which has gone missing before: instructions, the brief,
        // variables and the MCP servers each reached the lease and stopped at
        // the runner once.
        var request = await ResumptionContextTests.FlyCarryingAsync(new LeaseLoop
        {
            LoopId = "investigate",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read],
            WallClockSeconds = 600,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            Model = "claude-opus-5-5",
        });

        await Assert.That(request.Model).IsEqualTo("claude-opus-5-5");
    }
}
