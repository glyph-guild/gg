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
    /// <summary>
    /// The body as a document, or a refusal naming the likeliest cause.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THIS SHAPE ANSWERS AN UNAUTHENTICATED READ WITH 200 AND A SIGN-IN
    /// PAGE</b>, not with 401. So <c>EnsureSuccessStatusCode</c> passes, and
    /// the first thing to notice is a parse three lines later — by which point
    /// nothing in the stack remembers a credential was involved. Seen live as
    /// <i>"'&lt;' is an invalid start of a value. LineNumber: 2"</i> on a
    /// browse pane, which is true, useless, and points at neither the cause nor
    /// the fix.
    /// </para>
    /// <para>
    /// <b>The standard is the one the 404 arm already states:</b> <i>"telling
    /// an agent the item does not exist when the credential expired is the
    /// worst lie this reader could tell"</i>. A parse error is a quieter
    /// version of it — it blames the answer's syntax for a missing secret.
    /// </para>
    /// <para>
    /// <b>LIKELIEST, AND SAID AS SUCH.</b> A body that is not JSON is not
    /// PROOF of a credential problem — a proxy, a maintenance page and a
    /// misspelled host all land here too. So the sentence names what was asked,
    /// what came back, and what most often causes it, and leaves the reader to
    /// tell which. Guessing one cause and asserting it would be the same defect
    /// wearing a better sentence.
    /// </para>
    /// </remarks>
    private JsonDocument Answered(string body)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                $"{_host} answered, and the answer was not data. This shape serves a sign-in "
              + "page with a success status rather than refusing, so the usual cause is a "
              + "credential that is missing, expired, or lacks work-item read. It can also be "
              + "a proxy or a mistyped host - what arrived begins: "
              + Opening(body));
        }
    }

    /// <summary>
    /// Enough of the body to recognise it, and no more.
    /// </summary>
    /// <remarks>
    /// <b>A sign-in page is the thing being identified, so a glance has to be
    /// enough</b> — and a whole one in a diagnosis would push everything else
    /// off a pane. Control characters go because this reaches a terminal.
    /// </remarks>
    private static string Opening(string body)
    {
        var flattened = new string([.. body
            .Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ')
            .Where(c => !char.IsControl(c))]).Trim();

        return flattened.Length <= 60 ? flattened : flattened[..60] + "...";
    }

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
    private const string AreaField = "System.AreaPath";
    private const string IterationField = "System.IterationPath";

    /// <summary>
    /// What a list is, when nobody said.
    /// </summary>
    /// <remarks>
    /// <b>Open work, most recently touched first.</b> A person opening a
    /// browser is choosing something to do next, so closed items are noise and
    /// staleness is the useful sort order. This is a default and not a policy:
    /// it is here, in one string, so that changing it is one edit and reading
    /// it is one glance. That edit has now been made - a caller may narrow it -
    /// and this is still what it answers when nobody did.
    /// </remarks>
    private const string OpenWork =
        "[System.State] <> 'Closed' AND [System.State] <> 'Removed'";

    /// <summary>The query a filter asks for, or the default where it asks for nothing.</summary>
    /// <remarks>
    /// <para>
    /// <b>The narrowing happens HERE, in the query, and not over the page that
    /// came back.</b> A page is at most <c>limit</c> rows out of however many
    /// the tracker holds, so filtering it this end could only ever filter what
    /// happened to arrive: a sprint whose work sorted below the cut would read
    /// as an empty sprint. A box that says "nothing" when there is something is
    /// the one failure the browse endings exist to prevent.
    /// </para>
    /// <para>
    /// <b>An area path is asked for with <c>UNDER</c> and an iteration with
    /// <c>=</c>.</b> Both are trees, but a person picking a team means that
    /// team's work wherever it is filed beneath them, and a person picking a
    /// sprint means that sprint and not the ones nested under it.
    /// </para>
    /// <para>
    /// <b>States REPLACE the default rather than joining it.</b> Anding the
    /// not-closed default onto a caller who asked for closed work answers an
    /// empty list, for ever, with nothing on screen to say why.
    /// </para>
    /// </remarks>
    private static string Asking(WorkItemFilter? filter)
    {
        List<string> predicates = [];

        if (filter?.AreaPath is { Length: > 0 } area)
        {
            predicates.Add($"[{AreaField}] UNDER '{Quoted(area)}'");
        }

        if (filter?.Iteration is { Length: > 0 } iteration)
        {
            predicates.Add($"[{IterationField}] = '{Quoted(iteration)}'");
        }

        predicates.Add(filter?.States is { Count: > 0 } states
            ? "(" + string.Join(
                " OR ", states.Select(state => $"[{StateField}] = '{Quoted(state)}'")) + ")"
            : OpenWork);

        return "SELECT [System.Id] FROM WorkItems "
             + $"WHERE {string.Join(" AND ", predicates)} "
             + "ORDER BY [System.ChangedDate] DESC";
    }

    /// <summary>A value, safe to sit inside the single quotes of a query.</summary>
    /// <remarks>
    /// <b>Doubling is what this dialect does, and the sink beside this already
    /// does it</b> (<c>WiqlWorkItemSink.FoundAsync</c>) - so it is copied rather
    /// than reinvented. An area path with an apostrophe in it is an ordinary
    /// team name, not an attack, and it has to be browsable.
    /// </remarks>
    private static string Quoted(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);

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

        using var body = Answered(await answer.Content.ReadAsStringAsync(cancellationToken));
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

    /// <summary>
    /// What has happened to this item, oldest first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One call, because the shape carries both.</b> A comment arrives as a
    /// revision of a field like any other, so the discussion and the state
    /// changes are already interleaved in the order they happened - asking twice
    /// would be two round trips to rebuild an ordering the tracker has.
    /// </para>
    /// <para>
    /// <b>What changed, from and to.</b> "It changed" is not a fact anybody can
    /// use; where it came from is half of why it matters. The first revision has
    /// no old value for anything, which is creation and reads correctly as such.
    /// </para>
    /// <para>
    /// <b>Prose is stripped like every other body.</b> A comment is HTML from a
    /// tracker and is about to be drawn in a terminal.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<WorkItemChange>> HistoryAsync(
        string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        using var answer = await _client.GetAsync(
            $"{_host}/_apis/wit/workitems/{Uri.EscapeDataString(id)}/updates"
          + $"?api-version={ApiVersion}",
            cancellationToken);

        if (answer.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        answer.EnsureSuccessStatusCode();

        using var body = Answered(await answer.Content.ReadAsStringAsync(cancellationToken));
        var changes = new List<WorkItemChange>();

        if (!body.RootElement.TryGetProperty("value", out var revisions)
            || revisions.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        foreach (var revision in revisions.EnumerateArray())
        {
            if (!revision.TryGetProperty("fields", out var fields)
                || fields.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var who = revision.TryGetProperty("revisedBy", out var by)
                   && by.TryGetProperty("displayName", out var name)
                ? (name.ValueKind == JsonValueKind.String ? name.GetString() : null) ?? ""
                : "";

            var when = fields.TryGetProperty(ChangedField, out var stamped)
                    && Moved(stamped, "newValue") is { } stamp
                    && DateTimeOffset.TryParse(
                        stamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out var at)
                ? at
                : default;

            foreach (var field in fields.EnumerateObject())
            {
                // THE STAMP IS NOT A CHANGE. Every revision moves the changed
                // date, so reporting it would put one line of noise between
                // every two lines that mean something.
                if (string.Equals(field.Name, ChangedField, StringComparison.Ordinal))
                {
                    continue;
                }

                if (Said(field) is { } what)
                {
                    changes.Add(new WorkItemChange(when, who, what));
                }
            }
        }

        return changes;
    }

    /// <summary>The fields whose movement is worth a line, and what to call them.</summary>
    /// <remarks>
    /// <b>A board moves a dozen fields nobody asked about.</b> Rank, column,
    /// stack order and the revision number all change constantly and say nothing
    /// about the work; a history that reported them would bury the four lines
    /// that matter. These are the ones a person opens a history to read: where
    /// it went, what it is called, whose it is, and what anybody said about it.
    /// </remarks>
    private static readonly (string Field, string Label)[] WorthSaying =
    [
        (StateField, "State"),
        (TitleField, "Title"),
        ("System.AssignedTo", "Assigned to"),
        (HistoryField, "Said"),
    ];

    /// <summary>The discussion, which arrives as a field like any other.</summary>
    private const string HistoryField = "System.History";

    /// <summary>One field's move, as a person would read it.</summary>
    /// <remarks>
    /// <b>A comment says what was WRITTEN; anything else says where it WENT.</b>
    /// The discussion field's old value is the previous comment, so printing
    /// "was ... now ..." for it would bury the paragraph somebody just added
    /// under the one they added last week.
    /// </remarks>
    private static string? Said(JsonProperty change)
    {
        if (Array.Find(WorthSaying, w =>
                string.Equals(w.Field, change.Name, StringComparison.Ordinal))
            is not { Field.Length: > 0 } worth)
        {
            return null;
        }

        var now = Moved(change.Value, "newValue");

        if (now is not { Length: > 0 })
        {
            return null;
        }

        if (string.Equals(change.Name, HistoryField, StringComparison.Ordinal))
        {
            return Prose(now) is { Length: > 0 } wrote ? wrote : null;
        }

        return Moved(change.Value, "oldValue") is { Length: > 0 } before
            ? $"{worth.Label}: {before} -> {now}"
            : $"{worth.Label}: {now}";
    }

    /// <summary>
    /// One side of a field's move, as text.
    /// </summary>
    /// <remarks>
    /// <b>A person is not always a string.</b> An assignment carries an identity
    /// object, and printing its JSON would be worse than saying nothing - so the
    /// display name is taken where there is one.
    /// </remarks>
    private static string? Moved(JsonElement change, string side)
    {
        if (change.ValueKind != JsonValueKind.Object
            || !change.TryGetProperty(side, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Object
            ? value.TryGetProperty("displayName", out var named)
                && named.ValueKind == JsonValueKind.String ? named.GetString() : null
            : value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    /// <summary>
    /// What there is to narrow by, walked out of the tracker's own trees.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Three calls, because they are three different things.</b> Areas and
    /// iterations are classification trees and states belong to work item
    /// types; no endpoint here answers all three, and pretending otherwise
    /// would mean caching one of them somewhere it would go stale.
    /// </para>
    /// <para>
    /// <b>A depth is asked for, or the answer is the root and the news that it
    /// has children</b> - a list with one useless entry in it. The depth is
    /// finite because a request is: a tree deeper than this is one nobody is
    /// picking a leaf out of in a modal anyway.
    /// </para>
    /// </remarks>
    public async Task<WorkItemFacets> FacetsAsync(CancellationToken cancellationToken = default)
    {
        var areas = await NodesAsync("Areas", cancellationToken);
        var iterations = await NodesAsync("Iterations", cancellationToken);

        return new WorkItemFacets(areas, iterations, await StatesAsync(cancellationToken));
    }

    /// <summary>How deep a classification tree is read.</summary>
    /// <remarks>
    /// Deeper than any team nests in practice and finite on purpose: an
    /// unbounded read of somebody's whole project is a request that can take
    /// long enough for a console to look hung.
    /// </remarks>
    private const int TreeDepth = 6;

    private async Task<IReadOnlyList<string>> NodesAsync(
        string tree, CancellationToken cancellationToken)
    {
        using var answer = await _client.GetAsync(
            $"{_host}/_apis/wit/classificationnodes/{tree}"
          + $"?$depth={TreeDepth}&api-version={ApiVersion}",
            cancellationToken);

        answer.EnsureSuccessStatusCode();

        using var body = Answered(await answer.Content.ReadAsStringAsync(cancellationToken));

        List<string> paths = [];
        Walk(body.RootElement, paths);

        return paths;
    }

    /// <summary>Every node in the tree, parents before their children.</summary>
    /// <remarks>
    /// <b>The root is in the list.</b> It is where items land when nobody filed
    /// them deeper, and "everything, said on purpose" is a choice a person
    /// makes after narrowing too far.
    /// </remarks>
    private static void Walk(JsonElement node, List<string> paths)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (Queryable(Field(node, "path")) is { Length: > 0 } path)
        {
            paths.Add(path);
        }

        if (node.TryGetProperty("children", out var children)
            && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                Walk(child, paths);
            }
        }
    }

    /// <summary>
    /// A node's path, as a query spells it.
    /// </summary>
    /// <remarks>
    /// <b>THE TREE'S OWN NAME IS WEDGED INTO THE MIDDLE.</b> A node comes back
    /// as <c>\Widgets\Area\Platform</c>, and a query asking for that path
    /// matches nothing at all - not an error, no rows. The second segment is
    /// the tree ("Area" or "Iteration") and it is dropped, along with the
    /// leading separator, which is what leaves the shape <c>System.AreaPath</c>
    /// actually holds.
    /// </remarks>
    private static string Queryable(string? path)
    {
        if (path is not { Length: > 0 })
        {
            return "";
        }

        var segments = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length <= 1
            ? string.Join('\\', segments)
            : string.Join('\\', segments.Take(1).Concat(segments.Skip(2)));
    }

    /// <summary>Every state every type has, each one once, in the order met.</summary>
    /// <remarks>
    /// <b>Unioned, because a person filtering does not care which type a state
    /// belongs to.</b> Offering one list per work item type would be asking
    /// them to know which types have which states before they can narrow -
    /// which is the tracker's shape leaking through a pane that exists to hide
    /// it.
    /// </remarks>
    private async Task<IReadOnlyList<string>> StatesAsync(CancellationToken cancellationToken)
    {
        using var answer = await _client.GetAsync(
            $"{_host}/_apis/wit/workitemtypes?api-version={ApiVersion}", cancellationToken);

        answer.EnsureSuccessStatusCode();

        using var body = Answered(await answer.Content.ReadAsStringAsync(cancellationToken));

        if (!body.RootElement.TryGetProperty("value", out var types)
            || types.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<string> states = [];

        foreach (var type in types.EnumerateArray())
        {
            if (!type.TryGetProperty("states", out var listed)
                || listed.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var state in listed.EnumerateArray())
            {
                if (Field(state, "name") is { Length: > 0 } name
                    && !states.Contains(name, StringComparer.Ordinal))
                {
                    states.Add(name);
                }
            }
        }

        return states;
    }

    public async Task<WorkItemPage> BrowseAsync(
        string? cursor, int limit, WorkItemFilter? filter = null,
        CancellationToken cancellationToken = default)
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
            writing.WriteString("query", Asking(filter));
            writing.WriteEndObject();
        }

        using var query = new StringContent(
            Encoding.UTF8.GetString(body.ToArray()), Encoding.UTF8, "application/json");

        using var queried = await _client.PostAsync(
            $"{_host}/_apis/wit/wiql?api-version={ApiVersion}", query, cancellationToken);
        queried.EnsureSuccessStatusCode();

        using var ids = Answered(await queried.Content.ReadAsStringAsync(cancellationToken));

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

        // WHAT WAS FILTERED ON IS ON THE ROW. A person who narrows to a team
        // and is shown the same undifferentiated list cannot tell a filter that
        // took from one that did not, so the two columns a filter narrows on
        // are two columns a row carries.
        var columns = string.Join(
            ',', (string[])[TitleField, StateField, ChangedField, AreaField, IterationField]);
        using var read = await _client.GetAsync(
            $"{_host}/_apis/wit/workitems?ids={string.Join(',', wanted)}"
          + $"&fields={columns}&api-version={ApiVersion}",
            cancellationToken);
        read.EnsureSuccessStatusCode();

        using var page = Answered(await read.Content.ReadAsStringAsync(cancellationToken));

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
            Updated: Field(fields, ChangedField),
            AreaPath: Field(fields, AreaField),
            Iteration: Field(fields, IterationField));
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
