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

    /// <summary>Pushes, then proposes, and never overwrites either.</summary>
    public async Task<LandingOutcome> LandAsync(
        LandingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var url = $"https://{_host}/{request.Slug}.git";

        // Committed first: the agent edited a working tree and left it dirty,
        // and a push carries commits rather than changes. Authored as the
        // developer, because their credential is what is about to push it.
        if (await CommitAsync(request, cancellationToken) is { } uncommittable)
        {
            return uncommittable;
        }

        try
        {
            await GitInvocation.Push(url, "HEAD", request.Branch, request.Secret)
                .RunAsync(request.WorkingDirectory, cancellationToken);
        }
        catch (InvalidOperationException refused)
        {
            // A push that will not fast-forward and a push the credential will
            // not do both arrive here. They are told apart by asking the remote
            // whether the branch is already there - because "refused" and
            // "already exists" are different things to a person, and one of them
            // is not a problem with their credential.
            return await ExistsAsync(request, cancellationToken)
                ? new LandingOutcome.BranchExists(request.Branch)
                : Refusal(request, refused.Message);
        }

        // Keyed on the branch, so a retry after a proposal failure finds the one
        // that exists rather than opening a second.
        return await ProposeAsync(request, cancellationToken);
    }

    /// <summary>Whether the remote already has this branch.</summary>
    /// <remarks>
    /// Asked only after a push was refused, and asked of the REMOTE rather than
    /// inferred from git's wording: a message that changes between versions is
    /// not something a refusal should be classified by.
    /// </remarks>
    private async Task<bool> ExistsAsync(LandingRequest request, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(
            $"repos/{request.Slug}/branches/{Uri.EscapeDataString(request.Branch)}", cancellationToken);

        return response.IsSuccessStatusCode;
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
    private async Task<LandingOutcome> ProposeAsync(
        LandingRequest request, CancellationToken cancellationToken)
    {
        var head = $"{request.Slug.Split('/')[0]}:{request.Branch}";

        using var existing = await _http.GetAsync(
            $"repos/{request.Slug}/pulls?state=open&head={Uri.EscapeDataString(head)}", cancellationToken);

        if (existing.IsSuccessStatusCode
            && await existing.Content.ReadFromJsonAsync(
                   DestinationJson.Default.ListProposal, cancellationToken) is [var open, ..])
        {
            return new LandingOutcome.Landed(request.Branch, open.HtmlUrl, open.Number);
        }

        using var created = await _http.PostAsJsonAsync(
            $"repos/{request.Slug}/pulls",
            new NewProposal
            {
                Title = request.Title,
                Head = request.Branch,
                Base = request.BaseRef,
                Body = $"Opened by a governed flight. Branch `{request.Branch}`.",
            },
            DestinationJson.Default.NewProposal, cancellationToken);

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
    /// A refusal that names the credential rather than the status code.
    /// </summary>
    /// <remarks>
    /// The whole point of the two-control design is that this case is
    /// diagnosable. A 403 with somebody else's wording on it tells a developer
    /// nothing about which of their credentials was too narrow.
    /// </remarks>
    private static LandingOutcome Refusal(LandingRequest request, string detail) =>
        new LandingOutcome.CredentialRefused(
            CredentialLocator.ForRepo(request.Slug),
            $"The credential registered for {request.Slug} would not write to it. The envelope "
          + "declared a destination, which is permission for this flight to land somewhere - it is "
          + "not the ability to. Register a credential with write scope for this repository, or "
          + $"remove the destination from the envelope. The provider said: {Short(detail)}");

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
