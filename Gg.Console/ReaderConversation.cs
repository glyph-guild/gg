using System.Text;
using System.Text.Json;
using Gg.Client;
using Gg.Local;

namespace Gg.Console;

/// <summary>
/// One conversation with one reader, over line-delimited JSON-RPC.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hand-written, because this project may not gain a package.</b>
/// <c>Gg.Local</c> and the console both publish into an AOT binary and
/// <c>ProjectReferenceTests</c> holds the package list empty. What is needed is
/// four methods over line-delimited JSON, which is the same argument
/// <c>PlatformToolServer</c> makes from the other side of the same pipe.
/// </para>
/// <para>
/// <b>Streams, not a process.</b> This type never spawns anything: it is handed
/// a reader and a writer and speaks. That is what lets the whole protocol be
/// exercised without a child anywhere, and it keeps process ownership - the
/// deadline, the killing, the reaping - in one place that is not this one.
/// </para>
/// <para>
/// <b>Every ending is named rather than thrown.</b> See
/// <see cref="BrowseOutcome"/>: a console has to draw a failure next to the
/// reader's key and carry on, and an exception crossing into a redraw is a
/// console that dies because a tracker did.
/// </para>
/// <para>
/// <b>Asked once, not per page.</b> <c>initialize</c> and <c>tools/list</c>
/// happen on the first ask and never again: a round trip per keystroke is one a
/// person feels while scrolling, and what a server declares cannot change
/// inside one conversation.
/// </para>
/// </remarks>
public sealed class ReaderConversation(
    TextReader replies, TextWriter requests, string providerKey)
{
    private readonly TextReader _replies = replies;
    private readonly TextWriter _requests = requests;
    private readonly string _key = providerKey;

    private int _id;
    private bool _opened;
    private BrowseOutcome? _refusedToOpen;

    /// <summary>What the reader said it can do, asked once.</summary>
    /// <remarks>
    /// <b>Held rather than judged at the door.</b> Opening used to refuse any
    /// reader that could not be BROWSED, which made the ordinary reader - the
    /// one that declares <c>get_work_item</c> and nothing else - unreachable for
    /// the one thing it can do. Each verb asks its own question of this list
    /// now, and the conversation opens for both.
    /// </remarks>
    private IReadOnlyList<string>? _declared;

    /// <summary>A page of work, or the reason there is not one.</summary>
    public async Task<BrowseOutcome> BrowseAsync(
        string? cursor, int limit, CancellationToken cancellationToken = default)
    {
        if (await OpenAsync(cancellationToken) is { } refused)
        {
            return refused;
        }

        // DECLARED AND NOT BROWSABLE, which is not an error and not an empty
        // tracker. BrowseTool.IsBrowsable is the contract's own predicate, so
        // this asks the same question a reader answers.
        if (!BrowseTool.IsBrowsable(_declared))
        {
            return new BrowseOutcome.NotBrowsable(BrowseTool.NotBrowsable(_key));
        }

        var call = await CallAsync(
            BrowseTool.Name,
            arguments =>
            {
                if (cursor is { Length: > 0 })
                {
                    arguments.WriteString(BrowseTool.Paging.Cursor, cursor);
                }
                arguments.WriteNumber(BrowseTool.Paging.Limit, limit);
            },
            cancellationToken);

        return call switch
        {
            { Outcome: { } ended } => ended,
            { Text: { } text } => Paged(text),
            _ => new BrowseOutcome.Unintelligible(Saying("answered a call with no content")),
        };
    }

    /// <summary>
    /// What one work item says, in the reader's own words.
    /// </summary>
    /// <remarks>
    /// <b>The rendering is the answer.</b> The server already turns an item into
    /// text for an agent - type, state, title, body, acceptance criteria, tags,
    /// in the order a person reads them - and parsing that back into fields here
    /// would be a second opinion about the same bytes.
    /// </remarks>
    public async Task<ItemOutcome> ReadAsync(
        string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (await OpenAsync(cancellationToken) is { } refused)
        {
            return new ItemOutcome.Nothing(Why(refused));
        }

        if (!ItemTool.IsReadable(_declared))
        {
            return new ItemOutcome.Nothing(ItemTool.NotReadable(_key));
        }

        var call = await CallAsync(
            ItemTool.Name,
            arguments => arguments.WriteString(ItemTool.Id, id),
            cancellationToken);

        return call switch
        {
            { Outcome: { } ended } => new ItemOutcome.Nothing(Why(ended)),
            { Text: { } text } => new ItemOutcome.Read(text),
            _ => new ItemOutcome.Nothing(Saying("answered a call with no content")),
        };
    }

    /// <summary>
    /// What has happened to one item, in the reader's own words.
    /// </summary>
    /// <remarks>
    /// <b>Its own verb, and a reader may not have it.</b> A reader that answers
    /// what an item IS without answering what happened to it is still useful,
    /// so this says so by name rather than failing the whole modal.
    /// </remarks>
    public async Task<ItemOutcome> HistoryAsync(
        string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (await OpenAsync(cancellationToken) is { } refused)
        {
            return new ItemOutcome.Nothing(Why(refused));
        }

        if (!ItemTool.HasHistory(_declared))
        {
            return new ItemOutcome.Nothing(ItemTool.NoHistory(_key));
        }

        var call = await CallAsync(
            ItemTool.HistoryName,
            arguments => arguments.WriteString(ItemTool.Id, id),
            cancellationToken);

        return call switch
        {
            { Outcome: { } ended } => new ItemOutcome.Nothing(Why(ended)),
            { Text: { } text } => new ItemOutcome.Read(text),
            _ => new ItemOutcome.Nothing(Saying("answered a call with no content")),
        };
    }

    /// <summary>
    /// One ending's words, whichever ending it is.
    /// </summary>
    /// <remarks>
    /// <b>Flattened on purpose.</b> The five browse endings are five different
    /// things to go and do when a LIST is empty; about one item they are all
    /// "here is what the reader said", and a modal that sorted them would be
    /// offering a distinction nobody acts on.
    /// </remarks>
    private static string Why(BrowseOutcome outcome) => outcome switch
    {
        BrowseOutcome.NotBrowsable(var why) => why,
        BrowseOutcome.Refused(var why) => why,
        BrowseOutcome.Unintelligible(var why) => why,
        BrowseOutcome.Silent(var why) => why,
        _ => "The reader answered something this console could not read.",
    };

    /// <summary>
    /// initialize, then tools/list, once.
    /// </summary>
    /// <returns>The reason this conversation cannot proceed, or null.</returns>
    private async Task<BrowseOutcome?> OpenAsync(CancellationToken cancellationToken)
    {
        if (_opened)
        {
            return _refusedToOpen;
        }

        _opened = true;

        var greeting = await ExchangeAsync(
            Request("initialize", parameters =>
            {
                parameters.WriteString("protocolVersion", "2024-11-05");
                parameters.WriteStartObject("capabilities");
                parameters.WriteEndObject();
                parameters.WriteStartObject("clientInfo");
                parameters.WriteString("name", "gg-console");
                parameters.WriteString("version", GgVersions.Binary);
                parameters.WriteEndObject();
            }),
            cancellationToken);

        if (greeting is null)
        {
            return _refusedToOpen = new BrowseOutcome.Silent(
                Saying("said nothing at all. The reader process is not running, or it "
                     + "stopped before it answered."));
        }

        if (greeting.Value.Bad is { } unreadable)
        {
            return _refusedToOpen = unreadable;
        }

        var listed = await ExchangeAsync(Request("tools/list", null), cancellationToken);

        if (listed is null)
        {
            return _refusedToOpen = new BrowseOutcome.Silent(
                Saying("initialized and then stopped without saying what it can do."));
        }

        if (listed.Value.Bad is { } wrong)
        {
            return _refusedToOpen = wrong;
        }

        _declared = ToolNames(listed.Value.Document!.RootElement);

        // WHAT IT CAN DO IS NOT WHETHER IT OPENED. A reader that lists no browse
        // tool is still a reader, and refusing the conversation here made the
        // ordinary one - `get_work_item` and nothing else - unreachable for the
        // one thing it does.
        return _refusedToOpen = null;
    }

    private async Task<(BrowseOutcome? Outcome, string? Text)> CallAsync(
        string tool, Action<Utf8JsonWriter> arguments, CancellationToken cancellationToken)
    {
        var answer = await ExchangeAsync(
            Request("tools/call", parameters =>
            {
                parameters.WriteString("name", tool);
                parameters.WriteStartObject("arguments");
                arguments(parameters);
                parameters.WriteEndObject();
            }),
            cancellationToken);

        if (answer is null)
        {
            return (new BrowseOutcome.Silent(
                Saying($"stopped without answering '{tool}'.")), null);
        }

        if (answer.Value.Bad is { } wrong)
        {
            return (wrong, null);
        }

        var result = answer.Value.Document!.RootElement.GetProperty("result");
        var text = result.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.Array
            ? string.Concat(content.EnumerateArray()
                .Where(part => part.TryGetProperty("text", out _))
                .Select(part => part.GetProperty("text").GetString()))
            : null;

        // THE READER'S OWN WORDS. isError on a result is the tool saying why it
        // could not answer - an unreachable tracker, a credential that expired.
        // It already said it; saying it differently here would be a second
        // answer to one question.
        return result.TryGetProperty("isError", out var failed) && failed.ValueKind == JsonValueKind.True
            ? (new BrowseOutcome.Refused(text ?? Saying($"refused '{tool}' without saying why.")), null)
            : (null, text);
    }

    /// <summary>Write one request, read one reply.</summary>
    private async Task<(JsonDocument? Document, BrowseOutcome? Bad)?> ExchangeAsync(
        string request, CancellationToken cancellationToken)
    {
        try
        {
            await _requests.WriteLineAsync(request);
            await _requests.FlushAsync(cancellationToken);
        }
        catch (Exception gone) when (gone is IOException or ObjectDisposedException)
        {
            // A BROKEN PIPE IS A CHILD THAT IS NOT THERE, and it is what a
            // reader that died at startup actually produces - found by spawning
            // one rather than by scripting a stream, which cannot break. Null
            // here means "nothing came back", which the caller already words.
            return null;
        }

        while (await ReadLineAsync(cancellationToken) is { } line)
        {
            // BLANK LINES ARE NOT NARRATION. Framing whitespace is the one thing
            // a well-behaved server may emit that carries nothing.
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                return (JsonDocument.Parse(line), null);
            }
            catch (JsonException)
            {
                // ONE STRAY LINE AND THE CONVERSATION IS OVER. Skipping it and
                // reading on would resynchronise onto a reply belonging to a
                // different request, which is worse than stopping: the pane
                // would show somebody else's answer and nothing would say so.
                return (null, new BrowseOutcome.Unintelligible(
                    Saying("wrote a line that is not JSON-RPC, so the conversation cannot be "
                         + "trusted to line up: " + Short(line))));
            }
        }

        return null;
    }

    /// <summary>One line from the child, or null where there will be no more.</summary>
    /// <remarks>
    /// A read can fail the same way a write can - the child exits between the
    /// request and the answer - and an <c>IOException</c> reaching a redraw is
    /// the failure <see cref="BrowseOutcome"/> exists to prevent.
    /// </remarks>
    private async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _replies.ReadLineAsync(cancellationToken);
        }
        catch (Exception gone) when (gone is IOException or ObjectDisposedException)
        {
            return null;
        }
    }

    private string Request(string method, Action<Utf8JsonWriter>? parameters)
    {
        var id = _id++;
        using var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            writer.WriteNumber("id", id);
            writer.WriteString("method", method);

            if (parameters is not null)
            {
                writer.WriteStartObject("params");
                parameters(writer);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private BrowseOutcome Paged(string text)
    {
        JsonDocument body;
        try
        {
            body = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return new BrowseOutcome.Unintelligible(
                Saying("declared " + BrowseTool.Name + " and answered with something that is "
                     + "not the shape it promised: " + Short(text)));
        }

        using (body)
        {
            var root = body.RootElement;

            var items = root.TryGetProperty(BrowseTool.Paging.Items, out var listed)
                     && listed.ValueKind == JsonValueKind.Array
                ? listed.EnumerateArray().Select(Summary).ToList()
                : [];

            var next = root.TryGetProperty(BrowseTool.Paging.NextCursor, out var cursor)
                    && cursor.ValueKind == JsonValueKind.String
                ? cursor.GetString()
                : null;

            // NULL IS THE END. An empty string handed back as a cursor would
            // fetch the first page again, for ever.
            return new BrowseOutcome.Listed(
                new WorkItemPage(items, next is { Length: > 0 } ? next : null));
        }
    }

    private static WorkItemSummary Summary(JsonElement item) => new(
        Id: Field(item, BrowseTool.Fields.Id) ?? "",
        Title: Field(item, BrowseTool.Fields.Title) ?? "",
        State: Field(item, BrowseTool.Fields.State) ?? "",
        Url: Field(item, BrowseTool.Fields.Url) ?? "",
        Updated: Field(item, BrowseTool.Fields.Updated));

    private static string? Field(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<string> ToolNames(JsonElement reply) =>
        reply.TryGetProperty("result", out var result)
        && result.TryGetProperty("tools", out var tools)
        && tools.ValueKind == JsonValueKind.Array
            ? [.. tools.EnumerateArray()
                .Where(tool => tool.TryGetProperty("name", out _))
                .Select(tool => tool.GetProperty("name").GetString() ?? "")]
            : [];

    /// <summary>
    /// Every sentence names the reader.
    /// </summary>
    /// <remarks>
    /// A tenant may configure more than one, so "the reader did not answer" is
    /// a sentence a person cannot act on. Which one is the whole content.
    /// </remarks>
    private string Saying(string what) => $"The reader for '{_key}' {what}";

    /// <summary>Enough of a bad line to recognise it, and not a screenful.</summary>
    private static string Short(string line) =>
        line.Length <= 120 ? line : line[..120] + "…";
}
