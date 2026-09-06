using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The menu reaches the agent that is asked to choose from it.
/// </summary>
/// <remarks>
/// <b>Rendered once, control-plane-side, and inserted verbatim.</b> The same
/// disposition <c>Instructions</c> has, for the same reason: the contract
/// composes the envelope and renders the permitted sets from the destination
/// that bounds them, and a runner that re-worded the list would be a second
/// statement of what admission accepts.
/// </remarks>
public class TheMenuInThePromptTests
{
    private const string Menu =
        "\n\nIf you nominate a flight, these are what this destination admits."
      + "\n  work kinds: implement, research\n  environments: dev, staging";

    private static ExecutorRequest ARequest(string? menu, string? instructions = null) => new()
    {
        WorkingDirectory = "/tmp/tree",
        LoopId = "classify",
        IntentUri = "https://forge.example/acme/widgets/issues/1",
        Menu = menu,
        Instructions = instructions,
        Moves = [LoopMoves.Read, LoopMoves.Propose],
        WallClock = TimeSpan.FromMinutes(20),
        TranscriptPath = "/tmp/transcript.ndjson",
    };

    [Test]
    public async Task The_menu_reaches_the_prompt_verbatim()
    {
        var prompt = ClaudeCodeExecutor.PromptFor(ARequest(Menu));

        await Assert.That(prompt).Contains("work kinds: implement, research");
        await Assert.That(prompt).Contains("environments: dev, staging")
            .Because("a runner that re-worded the list would be a second statement of what "
                   + "admission accepts, and the two would drift.");
    }

    [Test]
    public async Task A_flight_that_can_nominate_nothing_is_not_offered_a_menu()
    {
        // S30.4-08. Most flights are not classify flights, and theirs must read
        // exactly as they did before any of this existed.
        var prompt = ClaudeCodeExecutor.PromptFor(ARequest(menu: null));

        await Assert.That(prompt.Contains("nominate", StringComparison.OrdinalIgnoreCase))
            .IsFalse();
    }

    [Test]
    public async Task The_operators_instructions_still_come_first()
    {
        // The ranking again. Standing policy is read before a list of what may
        // be asked for - the menu is a fact about the destination, not guidance,
        // and putting it above reviewed policy would rank it as such.
        var instructions = "\n\nThe operator's standing instructions for this work.";
        var prompt = ClaudeCodeExecutor.PromptFor(ARequest(Menu, instructions));

        await Assert.That(prompt.IndexOf("standing instructions", StringComparison.Ordinal))
            .IsLessThan(prompt.IndexOf("work kinds:", StringComparison.Ordinal));
    }
}
