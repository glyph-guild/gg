using System.Net;
using Gg.Contracts;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Gg.Runner.Vcs;

/// <summary>
/// Opens a proposal on a provider that spells one differently, and composes the
/// link it does not return.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured against the real service before it was written.</b> A probe
/// opened and abandoned a real proposal, and found three differences where two
/// were expected: the path and body members, and — the surprise — that
/// <b>nothing in the response is a url a person can open</b>. There is a
/// <c>url</c>, an api address full of guids, and ten <c>_links</c> members, none
/// of them <c>web</c>.
/// </para>
/// <para>
/// <b>So this adapter composes one, and that is the only place it may happen.</b>
/// Deriving structure from a string is the move this project keeps refusing, and
/// it is unavoidable here because the provider does not return the value. It
/// therefore happens inside the adapter that knows this provider's spelling,
/// from two members the response really gives, and never in shared code —
/// <see cref="LandingOutcome.Landed"/>'s contract is unchanged: a url a person
/// can open. Only who builds it moves.
/// </para>
/// <para>
/// <b>And an absent <c>repository.webUrl</c> throws rather than guessing.</b> A
/// url assembled from the api address would look like a link and open nothing,
/// which is worse than saying the proposal opened and its link is unknown.
/// </para>
/// </remarks>
public sealed class RefNamedDestinationAdapter(string provider, string host, HttpClient http)
    : IDestinationAdapter
{
    private readonly string _host = host;
    private readonly HttpClient _http = http;

    public string Provider { get; } = provider;

    /// <summary>
    /// Where a person reads the proposal, built because nothing returns it.
    /// </summary>
    /// <remarks>
    /// Public and named, because a composition this project would normally
    /// refuse should be the easiest thing in the file to find and to test.
    /// </remarks>
    public static string ProposalUrl(string repositoryWebUrl, int pullRequestId)
    {
        if (repositoryWebUrl is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                "The provider described the proposal without describing its repository, and a "
              + "link cannot be composed from an api address full of identifiers. The proposal "
              + "opened; where a person reads it is unknown, and saying so beats a url that "
              + "looks like a link and opens nothing.");
        }

        return $"{repositoryWebUrl.TrimEnd('/')}/pullrequest/{pullRequestId}";
    }

    public async Task<PushOutcome> PushAsync(
        LandingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // THE STEP THAT WAS MISSING HERE. GitPush pushes HEAD, so without this the
        // destination branch was created at the commit the workspace materialized
        // AT - proposing the repository back to itself, and leaving the agent's
        // work uncommitted in the tree. See GitCommit's remarks for how it read.
        if (await GitCommit.ForPushAsync(
                request.WorkingDirectory, request.Branch, request.Title, cancellationToken)
            is { } uncommittable)
        {
            return new PushOutcome.NothingToPush(uncommittable);
        }

        // The git half is git's, and it is the same question for every url
        // shape - which is why it lives in GitPush rather than here.
        return await GitPush.PushAsync(
            PathScopedCloneUrl(_host, request.Slug), request.WorkingDirectory, request.Branch,
            request.Slug, request.Secret, cancellationToken);
    }

    public async Task<LandingOutcome> ProposeAsync(
        LandingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The same refusal the other adapter makes, for the same reason: sent
        // anonymously and refused by the provider produces a sentence blaming a
        // credential nobody presented.
        if (request.Secret is not { Length: > 0 })
        {
            return new LandingOutcome.CredentialRefused(
                CredentialLocator.ForRepo(request.Slug),
                $"No credential was available for {request.Slug}, so the proposal was not "
              + "attempted. The branch is on the remote and this flight did not land.");
        }

        var repository = request.Slug[(request.Slug.IndexOf('/', StringComparison.Ordinal) + 1)..];

        using var existing = await SendAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                $"repositories/{repository}/pullrequests"
              + $"?searchCriteria.status=active&searchCriteria.sourceRefName=refs/heads/{request.Branch}"
              + "&api-version=7.1"),
            request.Secret, cancellationToken);

        if (existing.IsSuccessStatusCode
            && await existing.Content.ReadFromJsonAsync(
                   RefNamedJson.Default.RefNamedProposalList, cancellationToken) is { Value: [var open, ..] })
        {
            // THE LIST DOES NOT CARRY WHAT THE LINK IS COMPOSED FROM, and only a
            // real service says so. Measured: create and single-fetch return a
            // repository carrying `webUrl`; this list returns one carrying only
            // id, name, project and url. Reading it here threw the refusal below
            // on every re-proposal - so a flight reported failure while its
            // proposal sat open, which is the worst of the three answers.
            var webUrl = open.Repository?.WebUrl is { Length: > 0 } listed
                ? listed
                : await RepositoryWebUrlAsync(
                    repository, open.PullRequestId, request.Secret, cancellationToken);

            return new LandingOutcome.Landed(
                request.Branch, ProposalUrl(webUrl, open.PullRequestId), open.PullRequestId);
        }

        using var creation = new HttpRequestMessage(
            HttpMethod.Post, $"repositories/{repository}/pullrequests?api-version=7.1")
        {
            Content = JsonContent.Create(
                new RefNamedNewProposal
                {
                    SourceRefName = $"refs/heads/{request.Branch}",
                    TargetRefName = $"refs/heads/{request.BaseRef}",
                    Title = request.Title,
                    Description = $"Opened by a governed flight. Branch `{request.Branch}`.",
                },
                RefNamedJson.Default.RefNamedNewProposal),
        };

        using var created = await SendAsync(creation, request.Secret, cancellationToken);

        if (created.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new LandingOutcome.CredentialRefused(
                CredentialLocator.ForRepo(request.Slug),
                $"The credential registered for {request.Slug} would not open a proposal on it. "
              + "The envelope declared a destination, which is permission for this flight to "
              + "land somewhere - it is not the ability to. The branch is on the remote and "
              + "this flight did not land.");
        }

        if (!created.IsSuccessStatusCode)
        {
            return new LandingOutcome.Unsupported(
                $"The branch pushed and the change could not be proposed: {created.StatusCode}. "
              + "The branch is on the remote and this flight did not land.");
        }

        var proposal = await created.Content.ReadFromJsonAsync(
            RefNamedJson.Default.RefNamedProposal, cancellationToken);

        return proposal is null
            ? new LandingOutcome.Unsupported(
                "The provider accepted the proposal and described nothing, so there is no "
              + "reference to record.")
            : new LandingOutcome.Landed(
                request.Branch,
                ProposalUrl(proposal.Repository?.WebUrl ?? "", proposal.PullRequestId),
                proposal.PullRequestId);
    }

    /// <summary>The clone url this provider takes, without a <c>.git</c> suffix.</summary>
    private static string PathScopedCloneUrl(string host, string slug)
    {
        var trimmed = slug.Trim('/');
        var separator = trimmed.IndexOf('/', StringComparison.Ordinal);

        return separator <= 0
            ? throw new VcsCapabilityException(
                "slug", $"'{slug}' is not <project>/<repository>.")
            : $"https://{host}/{trimmed[..separator]}/_git/{trimmed[(separator + 1)..]}";
    }

    /// <summary>
    /// Where a person reads this repository, asked of the endpoint that says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A second request, and not a composed url, on purpose.</b> This adapter
    /// already knows the host and the slug, and the string it builds for cloning
    /// is byte-identical to the <c>webUrl</c> this provider returns — that was
    /// checked against a real service rather than assumed. It is still not what
    /// is used here: the provider <i>does</i> publish the value, and asking it
    /// is not the same kind of act as deriving structure from a string. A guess
    /// that happens to be right on one provider is the move this project keeps
    /// refusing.
    /// </para>
    /// <para>
    /// Returns empty when this endpoint does not say either, so the refusal in
    /// <see cref="ProposalUrl"/> still fires for the case it was written for —
    /// a provider that really does not publish a page a person can open.
    /// </para>
    /// </remarks>
    private async Task<string> RepositoryWebUrlAsync(
        string repository, int proposal, string secret, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                $"repositories/{repository}/pullrequests/{proposal}?api-version=7.1"),
            secret, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return "";
        }

        var fetched = await response.Content.ReadFromJsonAsync(
            RefNamedJson.Default.RefNamedProposal, cancellationToken);

        return fetched?.Repository?.WebUrl ?? "";
    }

    /// <summary>One request, carrying the credential the developer registered.</summary>
    /// <remarks>
    /// On the message rather than the client, for the reason the other adapter
    /// records: the client is shared across every repository this runner lands
    /// in, and a default header would send one repository's credential to
    /// another's api. This provider takes a personal access token as the
    /// PASSWORD of a basic credential with an empty user.
    /// </remarks>
    private Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, string secret, CancellationToken cancellationToken)
    {
        // A USER AGENT, because a provider really does refuse without one -
        // "Request forbidden by administrative rules" - and the client
        // production builds has no default headers at all. Found by proposing
        // through that client rather than through a test's, which is the same
        // reason the missing credential survived: a suite that supplies what
        // production does not is asking whether the shape compiles.
        //
        // On the REQUEST, like the credential, so it holds however the client
        // was constructed.
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("gg", "1"));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{secret}")));

        return _http.SendAsync(request, cancellationToken);
    }
}

/// <summary>A proposal as this provider describes it.</summary>
internal sealed record RefNamedProposal
{
    [JsonPropertyName("pullRequestId")]
    public int PullRequestId { get; init; }

    [JsonPropertyName("repository")]
    public RefNamedRepository? Repository { get; init; }
}

/// <summary>The one member of the repository a link can be composed from.</summary>
internal sealed record RefNamedRepository
{
    [JsonPropertyName("webUrl")]
    public string WebUrl { get; init; } = "";
}

/// <summary>What this provider answers a listing with.</summary>
internal sealed record RefNamedProposalList
{
    [JsonPropertyName("value")]
    public IReadOnlyList<RefNamedProposal> Value { get; init; } = [];
}

/// <summary>A proposal being asked for.</summary>
internal sealed record RefNamedNewProposal
{
    [JsonPropertyName("sourceRefName")]
    public string SourceRefName { get; init; } = "";

    [JsonPropertyName("targetRefName")]
    public string TargetRefName { get; init; } = "";

    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";
}

/// <summary>Source-generated, because this ships in a Native AOT binary.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RefNamedProposal))]
[JsonSerializable(typeof(RefNamedProposalList))]
[JsonSerializable(typeof(RefNamedNewProposal))]
internal sealed partial class RefNamedJson : JsonSerializerContext;
