using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Runner.Intent;

/// <summary>
/// Admitted changes performed against a tracker that answers <c>_apis/wit</c>
/// and queries in WIQL.
/// </summary>
/// <remarks>
/// <para>
/// <b>No provider is named here</b>, on <see cref="WiqlWorkItemSource"/>'s
/// argument: the class is named for a SHAPE - a path prefix, a query language,
/// a field vocabulary - and the host it speaks that shape to arrives in the
/// constructor. A public binary must not name a forge or a tracker, and which
/// one a deployment points this at is a fact about a machine.
/// </para>
/// <para>
/// <b>Hand-written over <c>HttpClient</c>, no SDK</b>, and written rather than
/// serialized: this assembly carries no package references and the binary
/// publishes AOT, so reflection-based <c>JsonSerializer</c> is an error here
/// rather than a warning.
/// </para>
/// <para>
/// <b>OUR VERBS ONTO THEIRS, which is what an adapter is for.</b> The contract
/// says create, update, field, link and score because those are what a person
/// meant; this tracker spells all five as a JSON-patch document against one
/// route, and the next tracker will spell them some other way. Putting that
/// mapping here is what lets a second one arrive without moving the contract.
/// </para>
/// <para>
/// <b>The credential is required at construction, and that is the refusal.</b>
/// A sink with no token would fail at the first write with a 401 that reads
/// exactly like a rejected token - and "the token was wrong" and "there was no
/// token" need different fixes, from different people. The reader is nullable
/// here because some trackers are readable anonymously; nothing is writable
/// anonymously.
/// </para>
/// </remarks>
public sealed class WiqlWorkItemSink : IWorkItemSink
{
    /// <summary>The revision of the shape this speaks.</summary>
    /// <remarks>
    /// Pinned rather than latest, the reader's rule: a tracker that rolled its
    /// default forward would change what this WRITES without anything here
    /// changing, which is the more expensive direction of that mistake.
    /// </remarks>
    private const string ApiVersion = "7.1";

    private const string TitleField = "System.Title";
    private const string DescriptionField = "System.Description";
    // `System.WorkItemType` AND `System.State` USED TO LIVE HERE as the two
    // paths a `field` proposal could reach, beside tags. They are gone rather
    // than kept: what a field proposal sets is now the paths it NAMED, which a
    // person permitted on the destination - so a fixed list here would be a
    // second, narrower answer to a question the menu already answers. Both are
    // still writable; they are simply written because somebody asked for them.
    private const string TagsField = "System.Tags";
    private const string PriorityField = "Microsoft.VSTS.Common.Priority";

    /// <summary>
    /// What a created item is tagged with, so a retry can find it.
    /// </summary>
    /// <remarks>
    /// <b>A tag rather than a custom field</b>, because a tag exists on every
    /// project of this shape and a custom field exists on the ones somebody
    /// configured. An idempotency mechanism that needs the customer to have set
    /// something up is one that silently is not there.
    /// </remarks>
    private const string KeyTagPrefix = "gg:";

    private readonly string _host;
    private readonly HttpClient _client;

    /// <param name="host">The tracker root, up to and including this shape's scoping.</param>
    /// <param name="secret">The token. Required: nothing is writable anonymously.</param>
    /// <param name="client">The client to speak through.</param>
    public WiqlWorkItemSink(string host, string? secret, HttpClient client)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentNullException.ThrowIfNull(client);

        if (secret is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                "This runner was asked to write to a work-item tracker and no credential was "
              + "resolved for it. The envelope naming a tracker destination is not enough on "
              + "its own: the credential is the developer's, registered with `gg credential "
              + "add` and resolved on this machine, and it must carry write scope. Nothing "
              + "was written.");
        }

        _host = host.TrimEnd('/');
        _client = client;

        // A PASSWORD WITH NO USER, this shape's convention for a token. The
        // reader's note applies here and costs more: getting it wrong produces a
        // 401 that reads like a rejected credential rather than a malformed
        // request, and on a write that is indistinguishable from a token
        // without write scope.
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(":" + secret)));
    }

    public async Task<IReadOnlyList<WorkItemWrite>> PerformAsync(
        IReadOnlyList<WorkItemProposal> admitted,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(admitted);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        var written = new List<WorkItemWrite>(admitted.Count);

        // IN ORDER, AND EVERY ONE. There is no filtering here and there is no
        // skipping: what arrives has already been admitted, and an adapter that
        // decided anything would be a second copy of that decision in the
        // process the threat model trusts least.
        foreach (var proposal in admitted)
        {
            written.Add(string.Equals(
                proposal.Operation, WorkItemOperations.Create, StringComparison.Ordinal)
                ? await CreatedAsync(proposal, idempotencyKey, cancellationToken)
                : await PatchedAsync(proposal, cancellationToken));
        }

        return written;
    }

    /// <summary>Creates the item, unless this flight already did.</summary>
    /// <remarks>
    /// <b>The case idempotency is not free in.</b> Setting a field twice is one
    /// field; creating an item twice is two items and somebody's morning
    /// deduplicating them. So the item carries this flight's key as a tag and
    /// the search comes first - which makes a retry after a lost report find
    /// what the first attempt made rather than making a second.
    /// </remarks>
    private async Task<WorkItemWrite> CreatedAsync(
        WorkItemProposal proposal, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (await FoundAsync(idempotencyKey, cancellationToken) is { } already)
        {
            return new WorkItemWrite(
                proposal.Operation, already, Where(already), AlreadyDone: true);
        }

        var type = Detail(proposal, "type") ?? "Issue";

        var body = Patch(writer =>
        {
            Add(writer, TitleField, Detail(proposal, "title") ?? proposal.Reason);
            Add(writer, DescriptionField, Detail(proposal, "description") ?? proposal.Reason);
            Add(writer, TagsField, KeyTagPrefix + idempotencyKey);

            if (proposal.Score is { } score)
            {
                Add(writer, PriorityField, score);
            }
        });

        using var answer = await SendAsync(
            HttpMethod.Post,
            $"{_host}/_apis/wit/workitems/${Uri.EscapeDataString(type)}"
          + $"?api-version={ApiVersion}",
            body,
            cancellationToken);

        answer.EnsureSuccessStatusCode();

        var id = await IdentifiedAsync(answer, cancellationToken);
        return new WorkItemWrite(proposal.Operation, id, Where(id), AlreadyDone: false);
    }

    /// <summary>Applies a change to an item that already exists.</summary>
    /// <remarks>
    /// <b>Idempotent for free, and that is a property of the tracker rather
    /// than of this code.</b> A patch sets a field to a value; applying it
    /// twice leaves the same value. The one exception is a LINK, which this
    /// shape appends - so the existing relations are read first and a link
    /// already present is not added again.
    /// </remarks>
    private async Task<WorkItemWrite> PatchedAsync(
        WorkItemProposal proposal, CancellationToken cancellationToken)
    {
        var target = proposal.Target!;

        if (string.Equals(proposal.Operation, WorkItemOperations.Link, StringComparison.Ordinal))
        {
            return await LinkedAsync(proposal, target, cancellationToken);
        }

        var body = Patch(writer =>
        {
            switch (proposal.Operation)
            {
                case WorkItemOperations.Update:
                    Add(writer, TitleField, Detail(proposal, "title"));
                    Add(writer, DescriptionField, Detail(proposal, "description"));
                    break;

                case WorkItemOperations.Field:
                    // THE PROPOSAL'S NAMED EDITS, which is what changed. This
                    // read three fixed paths out of the opaque detail - fine
                    // while nothing gated them, and wrong the moment a
                    // destination's menu did: what a person permitted is a
                    // path, so what gets written has to be the path that was
                    // permitted rather than whatever this file knows about.
                    foreach (var edit in proposal.Fields ?? [])
                    {
                        Add(writer, edit.Path, edit.Value);
                    }

                    break;

                case WorkItemOperations.Score:
                    Add(writer, PriorityField, proposal.Score);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"'{proposal.Operation}' was admitted and this adapter has no way to "
                      + "perform it. That is a gap between the contract's operations and this "
                      + "tracker's shape, and reporting it as done would be the worse answer.");
            }
        });

        using var answer = await SendAsync(
            HttpMethod.Patch,
            $"{_host}/_apis/wit/workitems/{Uri.EscapeDataString(target)}"
          + $"?api-version={ApiVersion}",
            body,
            cancellationToken);

        await RefusedAsync(answer, proposal, target, cancellationToken);

        return new WorkItemWrite(proposal.Operation, target, Where(target), AlreadyDone: false);
    }

    /// <summary>
    /// Throws naming what the tracker would not take, or returns.
    /// </summary>
    /// <remarks>
    /// <b>A patch is ONE request for every field in it</b>, so a tracker
    /// refusing one refuses the lot - and a flight that reported "nothing
    /// written" without saying which field was the problem would send somebody
    /// to read five field definitions. The tracker's own sentence is carried
    /// through rather than summarised, because it names the field and this
    /// code cannot.
    /// </remarks>
    private static async Task RefusedAsync(
        HttpResponseMessage answer,
        WorkItemProposal proposal,
        string target,
        CancellationToken cancellationToken)
    {
        if (answer.IsSuccessStatusCode)
        {
            return;
        }

        var said = await answer.Content.ReadAsStringAsync(cancellationToken);
        var paths = string.Join(", ", (proposal.Fields ?? []).Select(f => f.Path));

        throw new InvalidOperationException(
            $"The tracker refused a '{proposal.Operation}' on work item {target} with "
          + $"{(int)answer.StatusCode}. It was asked to set: {paths}. Nothing was written - a "
          + "patch is one request for every field in it, so one field it will not take "
          + $"refuses the rest. It said: {said}");
    }

    /// <summary>Adds a relation, unless the item already has it.</summary>
    private async Task<WorkItemWrite> LinkedAsync(
        WorkItemProposal proposal, string target, CancellationToken cancellationToken)
    {
        var to = Detail(proposal, "to")
            ?? throw new InvalidOperationException(
                $"A link on work item {target} was admitted and names no item to link TO. The "
              + "tool takes that in the proposal's detail, under `to`; without it there is "
              + "nothing to relate and inventing one is not this adapter's to do.");

        var relation = Detail(proposal, "relation") ?? "System.LinkTypes.Related";
        var url = $"{_host}/_apis/wit/workitems/{Uri.EscapeDataString(to)}";

        // READ FIRST, because this shape APPENDS relations rather than setting
        // them - so a retry would leave the same item linked twice, which is
        // the duplicate the seam's rule forbids.
        //
        // AND COMPARED ON THE ID, NOT THE URL, which a walk against a real
        // tracker is what taught this. The relation comes back naming the
        // project by GUID where the request named it by NAME:
        //
        //   asked:    .../ORG/JDX/_apis/wit/workitems/18599
        //   returned: .../ORG/139e24b0-…/_apis/wit/workItems/18599
        //
        // so whole-url equality never matches and every retry duplicates. The
        // trailing id is the only part both spellings agree on.
        //
        // ON THE KIND AS WELL, because an item can be a parent of the thing it
        // duplicates: a proposal for a related link is not satisfied by a
        // hierarchy link that happens to point at the same item.
        using (var reading = await _client.GetAsync(
            $"{_host}/_apis/wit/workitems/{Uri.EscapeDataString(target)}"
          + $"?$expand=relations&api-version={ApiVersion}",
            cancellationToken))
        {
            reading.EnsureSuccessStatusCode();

            using var existing = JsonDocument.Parse(
                await reading.Content.ReadAsStringAsync(cancellationToken));

            if (existing.RootElement.TryGetProperty("relations", out var relations)
                && relations.ValueKind == JsonValueKind.Array
                && relations.EnumerateArray().Any(r =>
                    r.TryGetProperty("url", out var at)
                    && string.Equals(Related(at.GetString()), to, StringComparison.Ordinal)
                    && r.TryGetProperty("rel", out var kind)
                    && string.Equals(kind.GetString(), relation, StringComparison.OrdinalIgnoreCase)))
            {
                return new WorkItemWrite(
                    proposal.Operation, target, Where(target), AlreadyDone: true);
            }
        }

        var body = Patch(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("op", "add");
            writer.WriteString("path", "/relations/-");
            writer.WriteStartObject("value");
            writer.WriteString("rel", relation);
            writer.WriteString("url", url);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });

        using var answer = await SendAsync(
            HttpMethod.Patch,
            $"{_host}/_apis/wit/workitems/{Uri.EscapeDataString(target)}"
          + $"?api-version={ApiVersion}",
            body,
            cancellationToken);

        answer.EnsureSuccessStatusCode();

        return new WorkItemWrite(proposal.Operation, target, Where(target), AlreadyDone: false);
    }

    /// <summary>
    /// The work item a relation points at, or null where it points at something
    /// that is not one.
    /// </summary>
    /// <remarks>
    /// <b>The last segment, and nothing before it.</b> Relations also carry
    /// attachments and hyperlinks, whose urls end in something that is not an
    /// item id - so this answers null for those rather than matching a link by
    /// accident. What makes the last segment trustworthy is that every spelling
    /// of a work-item url this tracker produces ends with the id.
    /// </remarks>
    private static string? Related(string? url) =>
        url is { Length: > 0 }
        && url.Contains("/_apis/wit/workitems/", StringComparison.OrdinalIgnoreCase)
            ? url[(url.LastIndexOf('/') + 1)..]
            : null;

    /// <summary>The item this flight already created, if it created one.</summary>
    private async Task<string?> FoundAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        await using var body = new MemoryStream();
        await using (var writing = new Utf8JsonWriter(body))
        {
            writing.WriteStartObject();
            writing.WriteString("query",
                "SELECT [System.Id] FROM WorkItems WHERE [System.Tags] CONTAINS '"
              + KeyTagPrefix + idempotencyKey.Replace("'", "''", StringComparison.Ordinal) + "'");
            writing.WriteEndObject();
        }

        using var content = new ByteArrayContent(body.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var answer = await _client.PostAsync(
            $"{_host}/_apis/wit/wiql?api-version={ApiVersion}", content, cancellationToken);

        answer.EnsureSuccessStatusCode();

        using var found = JsonDocument.Parse(
            await answer.Content.ReadAsStringAsync(cancellationToken));

        if (!found.RootElement.TryGetProperty("workItems", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var id))
            {
                return id.ValueKind == JsonValueKind.Number
                    ? id.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : id.GetString();
            }
        }

        return null;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string uri, byte[] body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Content = new ByteArrayContent(body);

        // THE CONTENT TYPE IS THE PROTOCOL for this shape: a patch sent as
        // application/json is refused with a message about the body rather than
        // about the type, which is the expensive kind of wrong from a runner.
        request.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/json-patch+json");

        return await _client.SendAsync(request, cancellationToken);
    }

    /// <summary>A patch document, written rather than serialized.</summary>
    private static byte[] Patch(Action<Utf8JsonWriter> operations)
    {
        using var body = new MemoryStream();
        using (var writing = new Utf8JsonWriter(body))
        {
            writing.WriteStartArray();
            operations(writing);
            writing.WriteEndArray();
        }

        return body.ToArray();
    }

    /// <summary>One field set, or nothing where the proposal named none.</summary>
    private static void Add(Utf8JsonWriter writer, string field, string? value)
    {
        if (value is not { Length: > 0 })
        {
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("op", "add");
        writer.WriteString("path", "/fields/" + field);
        writer.WriteString("value", value);
        writer.WriteEndObject();
    }

    /// <summary>
    /// One string out of the proposal's opaque detail.
    /// </summary>
    /// <remarks>
    /// <b>THE ONE PLACE ANYTHING READS THAT MEMBER, and it reads it as this
    /// tracker's shape rather than as the contract's.</b> The contract declares
    /// the detail opaque on purpose; an adapter has to put SOMETHING in a title,
    /// and what it looks for is a convention between this adapter and the skill
    /// that writes the proposals - which is exactly the seam an adapter is. A
    /// key that is absent is a field left alone, never a field cleared.
    /// </remarks>
    private static string? Detail(WorkItemProposal proposal, string key) =>
        proposal.Detail is { } detail
        && detail.ValueKind == JsonValueKind.Object
        && detail.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private async Task<string> IdentifiedAsync(
        HttpResponseMessage answer, CancellationToken cancellationToken)
    {
        using var made = JsonDocument.Parse(
            await answer.Content.ReadAsStringAsync(cancellationToken));

        if (made.RootElement.TryGetProperty("id", out var id))
        {
            return id.ValueKind == JsonValueKind.Number
                ? id.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture)
                : id.GetString() ?? "";
        }

        throw new InvalidOperationException(
            "The tracker accepted a work item and its answer names no id. Nothing can find "
          + "that item again, including the retry this flight would make - so it is reported "
          + "as a failure rather than as a create whose result was lost.");
    }

    private string Where(string id) =>
        $"{_host}/_workitems/edit/{Uri.EscapeDataString(id)}";
}
