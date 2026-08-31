using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gg.Contracts;

namespace Gg.Runner.Vcs;

/// <summary>
/// Pushes a branch and proposes a change, over https, with the developer's own
/// credential.
/// </summary>
/// <remarks>
/// <para>
/// <b>The runner writes; the control plane does not.</b> The platform's own app
/// has no permission to push or to propose, and adding one would make every
/// existing installation re-approve. It does not need to: the credential the
/// developer registered is already on this machine, and a local runner IS them
/// - so the proposal is authored by the developer, which is honest and gets
/// attribution for free.
/// </para>
/// <para>
/// <b>Forge-neutral, like the read adapter.</b> Host and api base are deployment
/// knowledge, injected, and named nowhere in this binary. The path shapes below
/// are one convention; a provider that spells them differently is a second
/// adapter, not a special case in this one.
/// </para>
/// </remarks>
public sealed class HttpsDestinationAdapter(
    string provider, string host, HttpClient http) : IDestinationAdapter
{
    private readonly string _host = host;
    private readonly HttpClient _http = http;

    public string Provider { get; } = provider;

    /// <summary>Pushes, and proposes nothing.</summary>
    /// <remarks>
    /// <b>The first gate, on its own.</b> Granted when no machine obligation is
    /// violated, which is weaker than admission: a flight whose human obligation is
    /// pending pushes its branch so a person has a commit to decide about, and opens
    /// no proposal.
    /// </remarks>
    public async Task<PushOutcome> PushAsync(
        LandingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var url = $"https://{_host}/{request.Slug}.git";

        // Committed first: the agent edited a working tree and left it dirty, and a
        // push carries commits rather than changes. Authored as the developer,
        // because their credential is what is about to push it.
        if (await CommitAsync(request, cancellationToken) is { } uncommittable)
        {
            return uncommittable switch
            {
                LandingOutcome.CredentialRefused(var locator, var diagnosis) =>
                    new PushOutcome.Refused(locator, diagnosis),
                LandingOutcome.Unsupported(var diagnosis) =>
                    new PushOutcome.NothingToPush(diagnosis),
                _ => new PushOutcome.Refused(request.Slug, "the tree could not be committed"),
            };
        }

        // THE GIT HALF, WHICH IS GIT'S. Fast-forward rules and what a remote's branch
        // points at are answered the same way for every url shape, so they live in
        // GitPush - where they can be proven against a real bare repository in CI rather
        // than only against a forge no build can reach.
        return await GitPush.PushAsync(
            url, request.WorkingDirectory, request.Branch, request.Slug,
            request.Secret, cancellationToken);
    }

    /// <summary>
    /// Turns the agent's edits into a commit on a new branch.
    /// </summary>
    /// <remarks>
    /// A local branch first, so the push has one ref to send and the remote
    /// name is decided by the refspec rather than by whatever the tree happened
    /// to be on.
    /// </remarks>
    private async Task<LandingOutcome?> CommitAsync(
        LandingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var plan in (GitInvocation[])
                     [
                         GitInvocation.Plain("checkout", "-b", request.Branch),
                         GitInvocation.Plain("add", "--all"),
                         GitInvocation.Plain(
                             "-c", "user.name=gg",
                             "-c", "user.email=gg@localhost",
                             "commit", "--message", request.Title),
                     ])
            {
                await plan.RunAsync(request.WorkingDirectory, cancellationToken);
            }

            return null;
        }
        catch (InvalidOperationException failure)
        {
            // Article XI: the diagnosis names what would not happen. "Could not
            // land" would send somebody looking at the remote for a problem that
            // was on this disk.
            return new LandingOutcome.Unsupported(
                $"This flight's work could not be committed before pushing: {failure.Message}");
        }
    }

    /// <summary>
    /// Opens the proposal, or returns the one already open for this branch.
    /// </summary>
    /// <remarks>
    /// <b>The idempotency this seam needs.</b> Push succeeds, proposal fails, the
    /// batch carrying the admission is retried - and there must not be two. The
    /// existing-proposal query runs FIRST rather than as a fallback after a
    /// duplicate error, because a provider that reports duplicates differently
    /// would otherwise produce the second one.
    /// </remarks>
    public async Task<LandingOutcome> ProposeAsync(
        LandingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // REFUSED BEFORE THE CALL, not sent anonymously and refused by the
        // provider. That was the state this method shipped in, and the sentence
        // it produced blamed a credential nobody had presented.
        if (request.Secret is not { Length: > 0 })
        {
            return NotAttempted(request);
        }

        var head = $"{request.Slug.Split('/')[0]}:{request.Branch}";

        // ON BOTH CALLS, and the query is the one that matters most. An
        // unauthenticated GET does not error - it fails IsSuccessStatusCode and
        // degrades silently to "there is no existing proposal", so a retry after
        // a successful push would open a SECOND pull request on a branch that
        // already had one. The idempotency this seam is built around is only
        // idempotent if the question can be asked.
        using var existing = await SendAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                $"repos/{request.Slug}/pulls?state=open&head={Uri.EscapeDataString(head)}"),
            request.Secret, cancellationToken);

        if (existing.IsSuccessStatusCode
            && await existing.Content.ReadFromJsonAsync(
                   DestinationJson.Default.ListProposal, cancellationToken) is [var open, ..])
        {
            return new LandingOutcome.Landed(request.Branch, open.HtmlUrl, open.Number);
        }

        using var creation = new HttpRequestMessage(HttpMethod.Post, $"repos/{request.Slug}/pulls")
        {
            Content = JsonContent.Create(
                new NewProposal
                {
                    Title = request.Title,
                    Head = request.Branch,
                    Base = request.BaseRef,
                    Body = $"Opened by a governed flight. Branch `{request.Branch}`.",
                },
                DestinationJson.Default.NewProposal),
        };

        using var created = await SendAsync(creation, request.Secret, cancellationToken);

        if (created.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return Refusal(request, await created.Content.ReadAsStringAsync(cancellationToken));
        }

        if (!created.IsSuccessStatusCode)
        {
            return new LandingOutcome.Unsupported(
                $"The branch pushed and the change could not be proposed: {created.StatusCode}. "
              + "The branch is on the remote and this flight did not land.");
        }

        var proposal = await created.Content.ReadFromJsonAsync(
            DestinationJson.Default.Proposal, cancellationToken);

        return proposal is null
            ? new LandingOutcome.Unsupported(
                "The provider accepted the proposal and described nothing, so there is no reference "
              + "to record.")
            : new LandingOutcome.Landed(request.Branch, proposal.HtmlUrl, proposal.Number);
    }

    /// <summary>
    /// One request, carrying the credential the developer registered.
    /// </summary>
    /// <remarks>
    /// <b>On the message rather than on the client</b>, because the client is
    /// shared across every repository this runner lands in and a default header
    /// would send one repository's credential to another's api. The secret goes
    /// in a header and nowhere else - not the uri, which is the most logged
    /// string any http client has, and not the body, which a provider echoes
    /// back into an error a person reads.
    /// </remarks>
    private Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, string secret, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);

        return _http.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// A refusal that names the credential rather than the status code.
    /// </summary>
    /// <remarks>
    /// The whole point of the two-control design is that this case is
    /// diagnosable. A 403 with somebody else's wording on it tells a developer
    /// nothing about which of their credentials was too narrow.
    /// </remarks>
    /// <summary>
    /// No credential was available, so nothing was asked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The half that has never existed, and the one every run produced.</b>
    /// Until the credential was threaded onto the calls above, every proposal
    /// went out anonymous, came back 401, and got
    /// <see cref="Refusal"/>'s sentence — which sends a developer to rotate a
    /// credential that was resolved, scope-checked, used successfully to push,
    /// and then never presented. <i>No credential was sent</i> and <i>the
    /// credential was refused</i> are two different facts, and they had one
    /// string between them.
    /// </para>
    /// <para>
    /// <b>It names the locator and not the scope</b>, because the thing to fix
    /// is that there is nothing registered there — not that what is registered
    /// is too narrow. Offering the scope advice here is what made the old
    /// sentence confident and wrong.
    /// </para>
    /// </remarks>
    private static LandingOutcome NotAttempted(LandingRequest request) =>
        new LandingOutcome.CredentialRefused(
            CredentialLocator.ForRepo(request.Slug),
            $"No credential was available for {request.Slug}, so the proposal was not "
          + "attempted. The branch is on the remote and this flight did not land. Register a "
          + $"credential for {CredentialLocator.ForRepo(request.Slug)} and the flight can be "
          + "retried.");

    private static LandingOutcome Refusal(LandingRequest request, string detail) =>
        new LandingOutcome.CredentialRefused(
            CredentialLocator.ForRepo(request.Slug),
            $"The credential registered for {request.Slug} would not write to it. The envelope "
          + "declared a destination, which is permission for this flight to land somewhere - it is "
          + "not the ability to. Register a credential with write scope for this repository, or "
          + "remove the destination from the envelope. The branch is on the remote and this "
          + $"flight did not land. The provider said: {Short(detail)}");

    private static string Short(string detail) =>
        detail.Length <= 200 ? detail.ReplaceLineEndings(" ").Trim() : detail[..200].Trim() + "…";
}

/// <summary>A proposal as the provider describes it.</summary>
internal sealed record Proposal
{
    [JsonPropertyName("number")]
    public int Number { get; init; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = "";
}

/// <summary>A proposal being asked for.</summary>
internal sealed record NewProposal
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("head")]
    public string Head { get; init; } = "";

    [JsonPropertyName("base")]
    public string Base { get; init; } = "";

    [JsonPropertyName("body")]
    public string Body { get; init; } = "";
}

/// <summary>Source-generated, because this ships in a Native AOT binary.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Proposal))]
[JsonSerializable(typeof(List<Proposal>))]
[JsonSerializable(typeof(NewProposal))]
internal sealed partial class DestinationJson : JsonSerializerContext;
