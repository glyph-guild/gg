using System.Net;
using Gg.Contracts;
using Gg.Local;
using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// The adapter that performs an admitted proposal, and the three things it may
/// not do: write more than was admitted, write twice, or write at all without a
/// credential that carries write scope.
/// </summary>
/// <remarks>
/// <para>
/// <b>S35.4-02, S35.4-03 and S35.4-04.</b> This is the far side of the slice's
/// first rule - the agent proposes and never acts - and the rule only means
/// anything if the thing that DOES act takes its instructions from the
/// admission rather than from the proposals. An adapter handed twelve proposals
/// and an admission naming three must write three.
/// </para>
/// <para>
/// <b>Idempotent across the seam, on <c>IDestinationAdapter</c>'s existing
/// rule.</b> The write can succeed and the report of it fail; the batch is
/// retried; a retry must find the earlier change rather than make a second.
/// For a field or a score that is free - setting a value twice is one value -
/// and for a CREATE it is not, which is why a created item carries the
/// idempotency key of the fact that asked for it and the adapter looks for one
/// before making another.
/// </para>
/// <para>
/// <b>The credential is the developer's and neither half is enough.</b> An
/// envelope naming a tracker destination and no credential resolved is a
/// flight that proposes and lands with nothing written; a credential with no
/// destination is a runner that could write and was never told to. Both are
/// ordinary states rather than errors, and both must refuse in our own words -
/// a tracker's 401 reads like a rejected token whichever of the two happened.
/// </para>
/// </remarks>
public class ATrackerAdapterWritesOnlyWhatWasAdmittedTests
{
    private static WorkItemProposal Proposing(
        string operation, string? target = "1421", string? score = null) => new()
    {
        Operation = operation,
        Target = target,
        Score = score,
        Reason = "the repro is attached and nobody has looked at it in three weeks",
    };

    /// <summary>A tracker that records what it was asked to do and answers yes.</summary>
    private sealed class Recording : HttpMessageHandler
    {
        public List<string> Wrote { get; } = [];

        public List<string> Read { get; } = [];

        public string? Existing { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var where = request.RequestUri!.PathAndQuery;

            if (request.Method == HttpMethod.Get || where.Contains("wiql", StringComparison.Ordinal))
            {
                Read.Add(where);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(Existing ?? """{"workItems":[]}"""),
                };
            }

            Wrote.Add($"{request.Method} {where} "
                    + await request.Content!.ReadAsStringAsync(cancellationToken));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":1421,"fields":{}}"""),
            };
        }
    }

    private static WiqlWorkItemSink Sink(Recording tracker, string? secret = "a-token") =>
        new("https://tracker.example/team/project", secret, new HttpClient(tracker));

    [Test]
    public async Task An_admission_naming_three_operations_does_not_license_a_fourth()
    {
        var tracker = new Recording();

        // TWELVE PROPOSED, THREE ADMITTED. The adapter is handed the admission,
        // never the proposals, which is the whole arrangement: an adapter that
        // took proposals and filtered them by an admission would have the rule
        // in it, and a rule in the runner is one the runner can be talked out of.
        var admitted = new[]
        {
            Proposing(WorkItemOperations.Field),
            Proposing(WorkItemOperations.Score, score: "P1"),
            Proposing(WorkItemOperations.Update, target: "1189"),
        };

        var written = await Sink(tracker).PerformAsync(admitted, "gg-flight-81");

        await Assert.That(written.Count).IsEqualTo(3);
        await Assert.That(tracker.Wrote.Count).IsEqualTo(3)
            .Because("three admitted, three written, and nothing else reached the tracker. "
                   + "Wrote: " + string.Join(" | ", tracker.Wrote));
    }

    [Test]
    public async Task A_retry_finds_the_item_the_first_attempt_created()
    {
        // THE CASE THAT IS NOT FREE. A field set twice is one field; an item
        // created twice is two items and a person's morning. So a create
        // carries the key of the fact that asked for it and the adapter looks
        // before it makes.
        var tracker = new Recording
        {
            Existing = """{"workItems":[{"id":1502}]}""",
        };

        var written = await Sink(tracker).PerformAsync(
            [Proposing(WorkItemOperations.Create, target: null)], "gg-flight-81");

        await Assert.That(tracker.Wrote).IsEmpty()
            .Because("the item this flight asked for already exists, so a second write is a "
                   + "duplicate rather than a retry. Wrote: " + string.Join(" | ", tracker.Wrote));

        await Assert.That(written[0].Target).IsEqualTo("1502")
            .Because("the retry reports the item the first attempt made, or the record says a "
                   + "create produced nothing and the item is orphaned.");
    }

    [Test]
    public async Task A_created_item_carries_the_key_that_makes_the_retry_possible()
    {
        var tracker = new Recording();

        await Sink(tracker).PerformAsync(
            [Proposing(WorkItemOperations.Create, target: null)], "gg-flight-81");

        await Assert.That(string.Join(" | ", tracker.Wrote))
            .Contains("gg-flight-81", StringComparison.Ordinal)
            .Because("nothing else in the tracker ties an item to the flight that asked for "
                   + "it, so without this the search above has nothing to find.");
    }

    [Test]
    public async Task A_write_with_no_credential_is_refused_in_our_own_words()
    {
        var tracker = new Recording();

        var refused = Assert.Throws<InvalidOperationException>(
            () => Sink(tracker, secret: null));

        await Assert.That(refused!.Message.Length).IsGreaterThan(60)
            .Because("a tracker's 401 reads like a rejected token whether the token was "
                   + $"wrong or absent, and those need different fixes. Said: {refused.Message}");

        await Assert.That(tracker.Wrote).IsEmpty();
    }
}
