using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Gg.Local;

namespace Gg.Runner.Intent;

/// <summary>
/// Work items from a tracker that answers <c>_apis/wit</c> and queries in WIQL.
/// </summary>
/// <remarks>
/// <para>
/// <b>No provider is named here.</b> The class is named for a SHAPE - a path
/// prefix, a query language, a field vocabulary - and the host it speaks that
/// shape to arrives in the constructor. This is the disposition
/// <c>PathScopedGitVcsAdapter</c> already has, and the reason a work-item
/// reader can live in this repository at all: a public binary must not name a
/// forge, and which tracker a deployment points this at is a fact about a
/// machine rather than about this code.
/// </para>
/// <para>
/// <b>Hand-written over <c>HttpClient</c>, no SDK.</b> This assembly carries no
/// package references and the binary publishes AOT; the vendor client libraries
/// for this shape are reflection- and DI-heavy, and what is actually needed is
/// two GETs and a POST. The same argument <c>PlatformToolServer</c> makes about
/// the protocol, one layer down.
/// </para>
/// <para>
/// <b>The credential is a password with no user.</b> That is this shape's
/// convention for a token, and getting it wrong produces a 401 that reads like
/// a rejected credential rather than a malformed request - the most expensive
/// kind of wrong to debug from a runner.
/// </para>
/// </remarks>
public sealed class WiqlWorkItemSource : IWorkItemSource
{
    /// <summary>The revision of the shape this speaks.</summary>
    /// <remarks>
    /// Pinned rather than latest. A tracker that rolls its default forward
    /// would change what this reads without anything here changing, and the
    /// field names below are the part that would break.
    /// </remarks>
    private const string ApiVersion = "7.1";

    /// <summary>The fields a single item is rendered from, in reading order.</summary>
    private const string TypeField = "System.WorkItemType";
    private const string StateField = "System.State";
    private const string TitleField = "System.Title";
    private const string DescriptionField = "System.Description";
    private const string AcceptanceField = "Microsoft.VSTS.Common.AcceptanceCriteria";
    private const string TagsField = "System.Tags";
    private const string ChangedField = "System.ChangedDate";

    /// <summary>
    /// What a list is, when nobody said.
    /// </summary>
    /// <remarks>
    /// <b>Open work, most recently touched first.</b> A person opening a
    /// browser is choosing something to do next, so closed items are noise and
    /// staleness is the useful sort order. This is a default and not a policy:
    /// it is here, in one string, so that changing it is one edit and reading
    /// it is one glance.
    /// </remarks>
    private const string OpenWorkQuery =
        "SELECT [System.Id] FROM WorkItems "
      + "WHERE [System.State] <> 'Closed' AND [System.State] <> 'Removed' "
      + "ORDER BY [System.ChangedDate] DESC";

    private readonly string _host;
    private readonly HttpClient _client;

    /// <param name="host">
    /// The tracker root, up to and including whatever scoping this shape puts
    /// in the path. Everything after it belongs to the shape and lives here.
    /// </param>
    /// <param name="secret">The token, or null where the tracker needs none.</param>
    /// <param name="client">The client to speak through.</param>
    public WiqlWorkItemSource(string host, string? secret, HttpClient client)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentNullException.ThrowIfNull(client);

        _host = host.TrimEnd('/');
        _client = client;

        if (secret is { Length: > 0 })
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(":" + secret)));
        }
    }

    public async Task<WorkItem?> ReadAsync(
        string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        using var answer = await _client.GetAsync(
            $"{_host}/_apis/wit/workitems/{Uri.EscapeDataString(id)}?api-version={ApiVersion}",
            cancellationToken);

        // NOT FOUND IS AN ANSWER, NOT A FAULT. The server turns null into a
        // sentence an agent can stop on. Every other refusal - 401 above all -
        // throws, because telling an agent the item does not exist when the
        // credential expired is the worst lie this reader could tell.
        if (answer.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        answer.EnsureSuccessStatusCode();

        using var body = JsonDocument.Parse(
            await answer.Content.ReadAsStringAsync(cancellationToken));
        var root = body.RootElement;
        var fields = root.TryGetProperty("fields", out var named) ? named : default;

        return new WorkItem(
            Id: Identifier(root) ?? id,
            Type: Field(fields, TypeField) ?? "",
            State: Field(fields, StateField) ?? "",
            Title: Field(fields, TitleField) ?? "",
            Description: Prose(Field(fields, DescriptionField)),
            AcceptanceCriteria: Prose(Field(fields, AcceptanceField)),
            Tags: Field(fields, TagsField),
            Url: $"{_host}/_workitems/edit/{Uri.EscapeDataString(id)}");
    }

    public async Task<WorkItemPage> BrowseAsync(
        string? cursor, int limit, CancellationToken cancellationToken = default)
    {
        var from = int.TryParse(cursor, out var offset) && offset > 0 ? offset : 0;

        // WRITTEN, NOT SERIALIZED. Reflection-based JsonSerializer is an
        // error in this repository, not a warning: this assembly is published
        // AOT and the analyzer is the thing that says so. One object with one
        // string does not need a source-generated context to escape properly.
        await using var body = new MemoryStream();
        await using (var writing = new Utf8JsonWriter(body))
        {
            writing.WriteStartObject();
            writing.WriteString("query", OpenWorkQuery);
            writing.WriteEndObject();
        }

        using var query = new StringContent(
            Encoding.UTF8.GetString(body.ToArray()), Encoding.UTF8, "application/json");

        using var queried = await _client.PostAsync(
            $"{_host}/_apis/wit/wiql?api-version={ApiVersion}", query, cancellationToken);
        queried.EnsureSuccessStatusCode();

        using var ids = JsonDocument.Parse(
            await queried.Content.ReadAsStringAsync(cancellationToken));

        var matched = ids.RootElement.TryGetProperty("workItems", out var items)
                   && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray()
                .Select(Identifier)
                .Where(id => id is not null)
                .Select(id => id!)
                .ToList()
            : [];

        var wanted = matched.Skip(from).Take(limit).ToList();

        // NO IDS, NO SECOND CALL. A batch read of an empty set is a request
        // this shape rejects, and the reader would report a tracker error where
        // the honest answer is that there is no work.
        if (wanted.Count == 0)
        {
            return new WorkItemPage([], null);
        }

        var columns = string.Join(',', (string[])[TitleField, StateField, ChangedField]);
        using var read = await _client.GetAsync(
            $"{_host}/_apis/wit/workitems?ids={string.Join(',', wanted)}"
          + $"&fields={columns}&api-version={ApiVersion}",
            cancellationToken);
        read.EnsureSuccessStatusCode();

        using var page = JsonDocument.Parse(
            await read.Content.ReadAsStringAsync(cancellationToken));

        var answered = page.RootElement.TryGetProperty("value", out var values)
                    && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Select(Summary).ToDictionary(
                item => item.Id, StringComparer.Ordinal)
            : [];

        // THE QUERY IS THE ONLY THING THAT SAID WHAT ORDER THIS IS IN, and the
        // batch read answers in its own - id ascending, against the tracker
        // this was measured on. Returning that discards the sort, so a person
        // browsing "most recently touched" gets whatever has the lowest number.
        // An id the batch did not answer for is dropped rather than rendered as
        // an empty row: it most likely moved out of scope between the two calls.
        var summaries = wanted
            .Select(id => answered.TryGetValue(id, out var item) ? item : null)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();

        // NULL IS THE END OF THE LIST. An empty string here would be handed
        // back by a caller and answered with the first page, for ever.
        var consumed = from + wanted.Count;

        return new WorkItemPage(
            summaries,
            consumed < matched.Count ? consumed.ToString(null as IFormatProvider) : null);
    }

    private WorkItemSummary Summary(JsonElement item)
    {
        var id = Identifier(item) ?? "";
        var fields = item.TryGetProperty("fields", out var named) ? named : default;

        return new WorkItemSummary(
            Id: id,
            Title: Field(fields, TitleField) ?? "",
            State: Field(fields, StateField) ?? "",
            Url: $"{_host}/_workitems/edit/{Uri.EscapeDataString(id)}",
            Updated: Field(fields, ChangedField));
    }

    /// <summary>An id, whether the tracker quoted it or not.</summary>
    private static string? Identifier(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out var id)
            ? id.ValueKind switch
            {
                JsonValueKind.Number => id.GetRawText(),
                JsonValueKind.String => id.GetString(),
                _ => null,
            }
            : null;

    private static string? Field(JsonElement fields, string name) =>
        fields.ValueKind == JsonValueKind.Object && fields.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// Markup as the prose it was written as.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Handing html through would spend the agent's context on tags</b>, and
    /// the installed reader this replaces strips it - so this strips it too,
    /// and to the same shape.
    /// </para>
    /// <para>
    /// <b>STRIPPING IS NOT DELETING.</b> A break and a closing block carry the
    /// only paragraph structure a description has; closing them up hands the
    /// agent one run-on line and loses the shape of what the author wrote. So
    /// the tags that meant "a line ends here" become a line ending.
    /// </para>
    /// <para>
    /// <b>Not a parser, and not trying to be.</b> This reads tracker-authored
    /// description html into prose. It is not a sanitiser and nothing is
    /// rendered from it.
    /// </para>
    /// </remarks>
    private static string? Prose(string? markup)
    {
        if (markup is not { Length: > 0 })
        {
            return markup;
        }

        var prose = new StringBuilder(markup.Length);

        for (var at = 0; at < markup.Length; at++)
        {
            if (markup[at] != '<' || TagEnding(markup, at) is not { } closing)
            {
                prose.Append(markup[at]);
                continue;
            }

            if (BreaksTheLine(markup[(at + 1)..closing]))
            {
                prose.Append('\n');
            }

            at = closing;
        }

        return Entities(prose.ToString()).Trim();
    }

    /// <summary>
    /// Where the tag opening at <paramref name="at"/> closes, or null if this
    /// is not a tag at all.
    /// </summary>
    /// <remarks>
    /// <b>The live tracker answers markdown for some items, not html.</b> A
    /// strip that treated every <c>&lt;</c> as a tag opening deleted everything
    /// up to the next <c>&gt;</c> - so "fails when x &lt; y and n &gt; 0"
    /// arrived as "fails when x 0", prose with a hole in it and nothing to say
    /// there had been one. A tag starts with a letter or a slash and closes on
    /// the same line; anything else is a character the author typed.
    /// </remarks>
    private static int? TagEnding(string markup, int at)
    {
        var first = at + 1;
        if (first < markup.Length && markup[first] == '/')
        {
            first++;
        }

        if (first >= markup.Length || !char.IsAsciiLetter(markup[first]))
        {
            return null;
        }

        var closing = markup.IndexOf('>', first);

        // A '<' with no '>' after it, or with a newline in between, is a
        // comparison somebody wrote and not a tag somebody opened.
        return closing < 0
            || markup.AsSpan(first, closing - first).ContainsAny('\n', '<')
            ? null
            : closing;
    }

    /// <summary>Whether a tag meant a line ends here.</summary>
    private static bool BreaksTheLine(string tag)
    {
        var name = tag.TrimStart('/').Split([' ', '\t', '\n', '/'], 2)[0];

        return name is "br" or "p" or "div" or "li" or "tr"
                    or "h1" or "h2" or "h3" or "h4" or "h5" or "h6";
    }

    /// <summary>
    /// The five entities a tracker's editor actually emits.
    /// </summary>
    /// <remarks>
    /// Named rather than general: a full entity table is a dependency, and
    /// everything past these five arrives as a literal character from the
    /// editors this shape ships with. <c>&amp;amp;</c> is last so that an
    /// escaped entity does not become a real one.
    /// </remarks>
    private static string Entities(string text) => text
        .Replace("&nbsp;", " ", StringComparison.Ordinal)
        .Replace("&lt;", "<", StringComparison.Ordinal)
        .Replace("&gt;", ">", StringComparison.Ordinal)
        .Replace("&quot;", "\"", StringComparison.Ordinal)
        .Replace("&#39;", "'", StringComparison.Ordinal)
        .Replace("&amp;", "&", StringComparison.Ordinal);
}
