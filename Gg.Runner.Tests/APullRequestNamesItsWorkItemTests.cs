using System.Net;
using System.Text.Json;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A proposal names the work item it was opened for, and says what changed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on a real one.</b> Pull request 8629 opened with the title
/// <i>"GG-118: Destination 'pull-request' requires 'in-scope', and it holds."</i>
/// and no work item attached, on a flight opened from a ticket.
/// </para>
/// <para>
/// <b>The title was an obligation verdict.</b> <c>Reason</c> on an admission
/// answers <i>why was this allowed to land</i> - it is written for an audit
/// trail by <c>AdmissionEngine</c>, from the destination id and the obligations
/// it requires. The landing used it because it was the nearest string in scope;
/// the <c>?? push.Reason</c> beside it is the tell that nothing ever chose a
/// title. Meanwhile the agent's own account of the change sat in
/// <c>run.Reason</c>, three lines up, unread.
/// </para>
/// <para>
/// <b>And the work item was not unknown.</b> The lease carries
/// <c>IntentProvider</c> and <c>IntentId</c> - the tracker key and <c>18490</c> for
/// that flight - and <c>LandingRequest</c> had six members, none of them the
/// intent, so the adapter could not have attached it if it had wanted to.
/// </para>
/// <para>
/// <b>What this does not fix.</b> A first sentence cut out of a summary is a
/// better title than an obligation verdict and it is still not one the agent
/// chose. That is the move-and-tool slice; this one makes the fallback honest
/// and gives the reviewer their link back to the backlog.
/// </para>
/// </remarks>
public class APullRequestNamesItsWorkItemTests
{
    /// <summary>Answers every call and keeps what was posted.</summary>
    /// <remarks>
    /// <b>The body on the wire, not a helper's return value.</b> What is in
    /// question is what the provider receives, and a test that read a composed
    /// object would pass whether or not the member was serialized - which is
    /// exactly how a field arrives absent with nothing erroring.
    /// </remarks>
    private sealed class Recording : HttpMessageHandler
    {
        internal List<string> Posted { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post && request.Content is { } content)
            {
                Posted.Add(await content.ReadAsStringAsync(cancellationToken));
            }

            // NO OPEN PROPOSAL, so the create path is the one reached. An
            // existing-proposal answer would return early and post nothing.
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    request.Method == HttpMethod.Post
                        ? """
                          {"pullRequestId":8629,
                           "repository":{"webUrl":"https://forge.invalid/acme/_git/widgets"}}
                          """
                        : """{"value":[]}""",
                    System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }

    private static LandingRequest Asking(LandingIntent? intent) => new()
    {
        WorkingDirectory = Path.GetTempPath(),
        Slug = "acme/widgets",
        Branch = "gg/GG-118",
        BaseRef = "develop",
        Title = "GG-118: the residual explicit local is gone",
        Secret = "not-a-real-credential",
        Intent = intent,
    };

    private static async Task<JsonElement> PostedAsync(LandingIntent? intent)
    {
        var recording = new Recording();
        var adapter = new RefNamedDestinationAdapter(
            "forge", "unreachable.invalid",
            new HttpClient(recording) { BaseAddress = new Uri("https://unreachable.invalid/") });

        await adapter.ProposeAsync(Asking(intent), CancellationToken.None);

        return JsonDocument.Parse(recording.Posted.Single()).RootElement.Clone();
    }

    [Test]
    public async Task The_work_item_the_flight_was_opened_for_is_attached()
    {
        // WHAT A REVIEWER OPENS THE PULL REQUEST FOR. A proposal that cannot be
        // traced back to the backlog item it answers is one somebody has to
        // match up by reading the diff.
        var posted = await PostedAsync(new LandingIntent
        {
            Provider = "a-tracker",
            Id = "18490",
            Uri = "https://tracker.invalid/acme/work/18490",
        });

        await Assert.That(posted.TryGetProperty("workItemRefs", out var refs)).IsTrue()
            .Because("this provider takes the attachment on the create body, and a proposal "
                   + "posted without the member is one nothing links afterwards. Posted: "
                   + posted.ToString());

        await Assert.That(refs.EnumerateArray().Select(r => r.GetProperty("id").GetString()))
            .Contains("18490");
    }

    [Test]
    public async Task A_flight_opened_from_no_ticket_attaches_nothing()
    {
        // ABSENT RATHER THAN EMPTY. A flight opened from a sentence has no work
        // item, and an empty array is a claim that it was asked and answered
        // nothing - which is the shape a provider is entitled to reject.
        var posted = await PostedAsync(null);

        await Assert.That(posted.TryGetProperty("workItemRefs", out _)).IsFalse()
            .Because("posted: " + posted.ToString());
    }

    [Test]
    public async Task The_title_a_proposal_carries_is_the_one_it_was_given()
    {
        // The adapter does not compose a title and must not start: which
        // sentence a person reads is decided one layer up, where the loop's
        // outcome and the admission are both in scope.
        var posted = await PostedAsync(null);

        await Assert.That(posted.GetProperty("title").GetString())
            .IsEqualTo("GG-118: the residual explicit local is gone");
    }
}
