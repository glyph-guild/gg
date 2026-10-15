using Gg.Contracts;
using Gg.Runner.Execution;
using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// The runner reads the field before the agent starts and the Task tier quotes it: as written
/// by the finding's flight, edited by a person since (their text governs), or written by
/// nothing recorded; an empty or unreadable field is said and the flight runs on the item
/// (S82.3-01, ADR-0041 Decision 10).
/// </summary>
public class AFlightReadsItsInstructionsFromTheItemTests
{
    private const string Field = "Custom.ClaudeCodePlan";

    private sealed class Throwing : IItemFieldHistory
    {
        public Task<(string? Value, IReadOnlyList<int> Touched)> FieldAsync(
            string id, string field, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("401 Unauthorized");
    }

    private static string Prompt(ItemInstructionsRead? read) =>
        ClaudeCodeExecutor.PromptFor(new ExecutorRequest
        {
            WorkingDirectory = "/work/flight",
            LoopId = "implement",
            Moves = [LoopMoves.Read, LoopMoves.Edit],
            IntentProvider = "agentic-backlog",
            IntentId = "18492",
            WallClock = TimeSpan.FromMinutes(20),
            TranscriptPath = "/work/flight/transcript.ndjson",
            ItemInstructions = read,
        });

    [Test]
    public async Task Nothing_recorded_wrote_it_and_an_empty_field_is_said()
    {
        await Assert.That(ItemInstructions.Decide("18492", Field, "<p>by hand</p>", [3], []).Standing)
            .IsEqualTo(InstructionStandings.Unrecorded);
        await Assert.That(ItemInstructions.Decide("18492", Field, "  ", [3], []).Standing)
            .IsEqualTo(InstructionStandings.Empty);
        await Assert.That(ItemInstructions.Decide("18492", Field, null, [], []).Standing)
            .IsEqualTo(InstructionStandings.Empty);
    }

    [Test]
    public async Task A_field_that_cannot_be_read_is_said_never_thrown()
    {
        var read = await ItemInstructions.ReadAsync(new Throwing(), "18492", Field, []);

        await Assert.That(read.Standing).IsEqualTo(InstructionStandings.Unreadable);
        await Assert.That(read.Diagnosis).Contains("401");
    }

    [Test]
    public async Task The_task_quotes_the_field_and_says_how_it_stands()
    {
        var asWritten = Prompt(new ItemInstructionsRead
        {
            Field = Field, Text = "<h2>Plan</h2><p>Move the tab stop.</p>",
            Standing = InstructionStandings.AsWritten, WrittenBy = "GG-1140",
        });
        await Assert.That(asWritten).Contains("<h2>Plan</h2><p>Move the tab stop.</p>");
        await Assert.That(asWritten).Contains("written by GG-1140");

        var edited = Prompt(new ItemInstructionsRead
        {
            Field = Field, Text = "<p>Use a button.</p>",
            Standing = InstructionStandings.EditedSince, WrittenBy = "GG-1140",
        });
        await Assert.That(edited).Contains("changed it after GG-1140 wrote it");
        await Assert.That(edited).Contains("governs");

        var unrecorded = Prompt(new ItemInstructionsRead
        {
            Field = Field, Text = "<p>by hand</p>", Standing = InstructionStandings.Unrecorded,
        });
        await Assert.That(unrecorded).Contains("no recorded flight wrote it");
    }

    [Test]
    public async Task An_empty_or_unreadable_field_is_said_and_the_flight_runs_on_the_item()
    {
        var empty = Prompt(new ItemInstructionsRead { Field = Field, Standing = InstructionStandings.Empty });
        await Assert.That(empty).Contains($"{Field} is empty");
        await Assert.That(empty).Contains("work item 18492");

        var unreadable = Prompt(new ItemInstructionsRead
        {
            Field = Field, Standing = InstructionStandings.Unreadable, Diagnosis = "401 Unauthorized",
        });
        await Assert.That(unreadable).Contains($"{Field} could not be read");
        await Assert.That(Prompt(null)).DoesNotContain(Field);
    }
}
