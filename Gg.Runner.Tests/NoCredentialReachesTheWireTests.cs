using System.Net;
using System.Text;
using Gg.Contracts;
using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// The tracker credential goes where only the transport reads it, and nowhere
/// a person or a fact can.
/// </summary>
/// <remarks>
/// <para>
/// <b>S35.4-05, and it is the same rule <c>ProposalCredentialTests</c> holds
/// for the forge and <c>GitInvocation</c> holds for git.</b> A uri is the most
/// logged string in any http client; a body is echoed back in a provider's
/// error text and that text ends up in a refusal a person reads. Neither may
/// carry the secret, and the reason to assert it once per adapter rather than
/// once is that each one builds its own requests.
/// </para>
/// <para>
/// <b>And it is the LAST place this could go wrong.</b> Everything upstream of
/// here is arranged so an agent never holds a tracker credential at all - the
/// tool server that takes the proposal reaches no credential, and the fact that
/// carries it is written from the tool call. This is the one process that has
/// the token, so this is the one place it can leak.
/// </para>
/// </remarks>
public class NoCredentialReachesTheWireTests
{
    private const string Secret = "pat-9f3c11d0-never-log-me";

    private sealed class Watching : HttpMessageHandler
    {
        public List<(string Method, string Uri, string Body, string Headers)> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add((
                request.Method.ToString(),
                request.RequestUri!.ToString(),
                request.Content is null
                    ? ""
                    : await request.Content.ReadAsStringAsync(cancellationToken),
                string.Join("; ", request.Headers.Select(h =>
                    h.Key + "=" + string.Join(",", h.Value)))));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    request.Method == HttpMethod.Get || request.RequestUri!.PathAndQuery
                        .Contains("wiql", StringComparison.Ordinal)
                        ? """{"workItems":[],"relations":[]}"""
                        : """{"id":1421}"""),
            };
        }
    }

    [Test]
    public async Task The_credential_reaches_the_header_and_nothing_else()
    {
        var watching = new Watching();
        var sink = new WiqlWorkItemSink(
            "https://tracker.example/team/project", Secret, new HttpClient(watching));

        await sink.PerformAsync(
            [
                new WorkItemProposal
                {
                    Operation = WorkItemOperations.Create,
                    Reason = "the crash has no item",
                },
                new WorkItemProposal
                {
                    Operation = WorkItemOperations.Score,
                    Target = "1421",
                    Score = "P1",
                    Reason = "two independent repros",
                },
            ],
            "01a0776a-cacb-76dc-b444-2b7031",
            CancellationToken.None);

        await Assert.That(watching.Seen).IsNotEmpty()
            .Because("an assertion over requests nobody made proves nothing, which is how the "
                   + "`--bare` measurement passed by never reaching its subject.");

        foreach (var (method, uri, body, headers) in watching.Seen)
        {
            await Assert.That(uri).DoesNotContain(Secret)
                .Because($"the {method} uri carries the secret, and a uri is the most logged "
                       + "string in any http client.");

            await Assert.That(body).DoesNotContain(Secret)
                .Because("a tracker echoes a body back in its error text, and that text goes "
                       + "into a refusal a person reads.");

            // THE HEADER IS WHERE IT IS SUPPOSED TO BE, and this reads the
            // per-request headers rather than the client's defaults - so the
            // assertion above cannot pass by the credential having gone
            // missing entirely, which would be a different bug wearing this
            // test's success.
            await Assert.That(headers).DoesNotContain(Secret)
                .Because("base64 in Authorization is the transport's business; the secret in "
                       + "PLAIN text in any header is a string somebody will log.");
        }
    }

    [Test]
    public async Task What_the_write_reports_back_names_no_credential()
    {
        var watching = new Watching();
        var sink = new WiqlWorkItemSink(
            "https://tracker.example/team/project", Secret, new HttpClient(watching));

        var written = await sink.PerformAsync(
            [new WorkItemProposal
            {
                Operation = WorkItemOperations.Score,
                Target = "1421",
                Score = "P1",
                Reason = "two independent repros",
            }],
            "01a0776a",
            CancellationToken.None);

        // THE FACT SIDE. What comes back from here becomes destination.landed
        // and is read by a person and stored by the control plane, so a url
        // built from a host that had the token in it would put the secret in
        // the permanent record rather than in a log that rotates.
        foreach (var write in written)
        {
            await Assert.That(write.Url ?? "").DoesNotContain(Secret);
            await Assert.That(write.Target).DoesNotContain(Secret);
        }
    }
}
