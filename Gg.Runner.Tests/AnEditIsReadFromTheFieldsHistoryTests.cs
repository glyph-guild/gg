using System.Net;
using Gg.Contracts;
using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// A person's edit is found in the field's revision history, not by comparing text: a later
/// revision touching the field is an edit, one touching only other fields is not (S82.3-02,
/// ADR-0041 Decision 10).
/// </summary>
/// <remarks>
/// <b>The owner's choice, 2026-10-15</b>: a tracker rewrites rich text on save, so comparing a
/// field's text with what a flight wrote would report edits nobody made.
/// </remarks>
public class AnEditIsReadFromTheFieldsHistoryTests
{
    private const string Field = "Custom.ClaudeCodePlan";

    private static readonly EarlierFinding Wrote = new()
    {
        FlightNumber = "GG-1140",
        Kind = "investigate-instructions",
        FoundAt = new DateTimeOffset(2026, 10, 15, 5, 6, 0, TimeSpan.Zero),
        Text = "<h2>Plan</h2>",
        Target = "18492",
        Field = Field,
        Revision = 13,
    };

    [Test]
    public async Task No_revision_after_the_write_touched_the_field_so_it_stands_as_written()
    {
        var read = ItemInstructions.Decide("18492", Field, "<h2>Plan</h2>", [5, 12, 13], [Wrote]);

        await Assert.That(read.Standing).IsEqualTo(InstructionStandings.AsWritten);
        await Assert.That(read.WrittenBy).IsEqualTo("GG-1140");
    }

    [Test]
    public async Task A_later_revision_that_touched_the_field_is_a_persons_edit()
    {
        var read = ItemInstructions.Decide("18492", Field, "<h2>Plan, amended</h2>", [12, 13, 15], [Wrote]);

        await Assert.That(read.Standing).IsEqualTo(InstructionStandings.EditedSince);
        await Assert.That(read.WrittenBy).IsEqualTo("GG-1140");
    }

    /// <summary>A tracker whose item has a later revision that touched only the title.</summary>
    private sealed class History : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var where = request.RequestUri!.PathAndQuery;
            var body = where.Contains("/updates", StringComparison.Ordinal)
                ? """
                  {"value":[
                    {"rev":12,"fields":{"System.Title":{"newValue":"x"}}},
                    {"rev":13,"fields":{"Custom.ClaudeCodePlan":{"newValue":"<h2>Plan</h2>"}}},
                    {"rev":14,"fields":{"System.Title":{"newValue":"y"}}}
                  ]}
                  """
                : """{"id":18492,"rev":14,"fields":{"Custom.ClaudeCodePlan":"<h2>Plan</h2>"}}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    [Test]
    public async Task The_tracker_says_which_revisions_touched_the_field_and_a_title_edit_is_not_one()
    {
        var source = new WiqlWorkItemSource(
            "https://tracker.example/team/project", "a-token", new HttpClient(new History()));

        var (value, touched) = await source.FieldAsync("18492", Field);

        await Assert.That(value).IsEqualTo("<h2>Plan</h2>");
        await Assert.That(touched).IsEquivalentTo(new[] { 13 });

        var read = await ItemInstructions.ReadAsync(source, "18492", Field, [Wrote]);
        await Assert.That(read.Standing).IsEqualTo(InstructionStandings.AsWritten)
            .Because("revision 14 changed only the title, so the field is as GG-1140 wrote it.");
    }
}
