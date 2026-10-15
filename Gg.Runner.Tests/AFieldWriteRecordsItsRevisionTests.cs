using System.Net;
using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Facts;
using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// A tracker field write records the revision it produced, and an earlier finding that wrote
/// a field names the item, the field and that revision (S82.1-02, ADR-0041 Decision 10).
/// </summary>
/// <remarks>
/// <b>The revision rather than the text</b> (owner, 2026-10-15): a tracker rewrites rich text
/// on save, so a later flight asks whether any revision after this one touched the field.
/// </remarks>
public class AFieldWriteRecordsItsRevisionTests
{
    /// <summary>A tracker that answers a write with the revision it made.</summary>
    private sealed class Revising : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.Method == HttpMethod.Get
                    ? """{"workItems":[]}"""
                    : """{"id":18492,"rev":13,"fields":{}}"""),
            });
    }

    private static readonly WorkItemProposal Plan = new()
    {
        Operation = WorkItemOperations.Field,
        Target = "18492",
        Reason = "the plan, where a person reads it",
        Fields = [new WorkItemFieldEdit { Path = "Custom.ClaudeCodePlan", Value = "<h2>Plan</h2>" }],
    };

    [Test]
    public async Task The_sink_returns_the_revision_the_tracker_answered()
    {
        var sink = new WiqlWorkItemSink(
            "https://tracker.example/team/project", "a-token", new HttpClient(new Revising()));

        var written = (await sink.PerformAsync([Plan], "agentic-backlog")).Single();

        await Assert.That(written.Revision).IsEqualTo(13);
        await Assert.That(written.Fields).IsEquivalentTo(new[] { "Custom.ClaudeCodePlan" });
    }

    [Test]
    public async Task The_write_becomes_a_fact_of_its_own_kind()
    {
        var fact = FactPipeline.Digest(
            FactHygiene.Clean(new GatheredFacts([new FactPayload.Written(new WorkItemWritten
            {
                DestinationId = "agentic-backlog",
                Target = "18492",
                Fields = ["Custom.ClaudeCodePlan"],
                Revision = 13,
            })])),
            "flight-1", new DateTimeOffset(2026, 10, 15, 8, 0, 0, TimeSpan.Zero)).Items.Single();

        await Assert.That(fact.Kind).IsEqualTo(FactKinds.WorkItemWritten);
        await Assert.That(fact.Written!.Revision).IsEqualTo(13);
        await Assert.That(FactEnvelope.Validate(fact)).IsNull();
    }

    [Test]
    public async Task The_vocabulary_and_the_surface_declare_it()
    {
        await Assert.That(FactKinds.All).Contains(FactKinds.WorkItemWritten);
        await Assert.That(Vocabulary.Types).Contains(typeof(WorkItemWritten));
        await Assert.That(ProtocolSurface.JsonMembers[typeof(WorkItemWritten)])
            .IsEquivalentTo(new[] { "destinationId", "target", "fields", "revision" });
        await Assert.That(ProtocolSurface.JsonMembers[typeof(FactEnvelope)]).Contains("written");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(EarlierFinding)]).Contains("target");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(EarlierFinding)]).Contains("field");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(EarlierFinding)]).Contains("revision");
    }
}
