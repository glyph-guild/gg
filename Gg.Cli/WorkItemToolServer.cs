using System.Text;
using System.Text.Json;
using Gg.Client;
using Gg.Local;

namespace Gg.Cli;

/// <summary>
/// The reader this binary serves: one work item by id, and a page of them to
/// browse, over line-delimited JSON-RPC on standard input and output.
/// </summary>
/// <remarks>
/// <para>
/// <b>A SECOND SERVER, and the separation is load-bearing.</b>
/// <see cref="PlatformToolServer"/> earns its safety by holding nothing - no
/// credential, no session, no round trip - which is what makes it safe as a
/// child of a process the threat model treats as compromised. This one holds a
/// credential and speaks to a tracker, so it cannot make that claim and must
/// not be mixed into a server that does. Two channels, two justifications, and
/// an agent that reaches this one can still only READ.
/// </para>
/// <para>
/// <b>Why this exists rather than a script an operator installs.</b> An
/// installed reader is discovered at runtime and declares whatever it declares:
/// the one running in production declares <c>get_work_item</c> and nothing
/// else, so no console can offer a list of work to pick from. A reader this
/// repository owns is the one reader that can be HELD to
/// <see cref="BrowseTool"/> rather than asked to honour it.
/// </para>
/// <para>
/// <b>It names no forge.</b> Everything here is protocol and shape; which
/// tracker is answered lives in the <see cref="IWorkItemSource"/> handed in,
/// and the host that source speaks to is configuration. That is the split
/// <c>PathScopedGitVcsAdapter</c> already makes.
/// </para>
/// <para>
/// <b>STDOUT IS THE PROTOCOL.</b> Nothing here may narrate, log, or greet: one
/// stray line and the client sees a server that never initialized rather than a
/// tool that failed. A source that throws is answered as a tool error on the
/// request's own id, never as a crash - an agent can act on "the tracker
/// refused"; it cannot act on a closed pipe.
/// </para>
/// <para>
/// <b>Hand-written rather than an SDK</b>, for the reason the sibling server
/// records: this binary publishes AOT and carries no package references, and
/// the official server library is DI- and reflection-shaped.
/// </para>
/// </remarks>
public static class WorkItemToolServer
{
    /// <summary>The tool a flight depends on: read the item it is about.</summary>
    public const string ReadName = "get_work_item";

    /// <summary>What a page holds when the caller names no size.</summary>
    private const int DefaultLimit = 50;

    /// <summary>
    /// The most a caller may take at once.
    /// </summary>
    /// <remarks>
    /// A limit an agent chooses is a limit an injected agent chooses. This is
    /// not a security boundary - the tracker's own paging is - but it keeps a
    /// single call from turning into a whole-backlog fetch by typo.
    /// </remarks>
    private const int MaximumLimit = 200;

    public static async Task<int> RunAsync(
        TextReader input,
        TextWriter output,
        IWorkItemSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(source);

        while (await input.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonDocument message;
            try
            {
                message = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                // SKIPPED, NEVER FATAL. A dead server loses the agent its tool
                // for the whole session, and there is no id to answer on - a
                // line that will not parse has no request to fail.
                continue;
            }

            using (message)
            {
                if (await AnswerAsync(message.RootElement, source, cancellationToken) is { } answer)
                {
                    await output.WriteLineAsync(answer);
                    await output.FlushAsync(cancellationToken);
                }
            }
        }

        return 0;
    }

    private static async Task<string?> AnswerAsync(
        JsonElement message, IWorkItemSource source, CancellationToken cancellationToken)
    {
        var method = message.TryGetProperty("method", out var named) ? named.GetString() : null;

        // A NOTIFICATION HAS NO ID AND TAKES NO RESPONSE. Answering one writes
        // a line the client cannot match to a request.
        if (!message.TryGetProperty("id", out var id) || id.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return method switch
        {
            "initialize" => Initialized(id, message),
            "tools/list" => Listed(id),
            "tools/call" => await CalledAsync(id, message, source, cancellationToken),
            _ => Error(id, -32601,
                $"'{method}' is not a method this server has. It has initialize, tools/list "
              + "and tools/call."),
        };
    }

    private static string Initialized(JsonElement id, JsonElement message) =>
        Write(writer =>
        {
            Envelope(writer, id);
            writer.WriteStartObject("result");

            // ECHOED RATHER THAN DECLARED, as the sibling server does: the
            // client has already said which revision it speaks.
            writer.WriteString("protocolVersion",
                message.TryGetProperty("params", out var parameters)
                && parameters.TryGetProperty("protocolVersion", out var version)
                && version.GetString() is { Length: > 0 } spoken
                    ? spoken
                    : "2024-11-05");

            writer.WriteStartObject("capabilities");
            writer.WriteStartObject("tools");
            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteStartObject("serverInfo");
            writer.WriteString("name", "tracker");
            writer.WriteString("version", GgVersions.Binary);
            writer.WriteEndObject();

            writer.WriteEndObject();
        });

    private static string Listed(JsonElement id) =>
        Write(writer =>
        {
            Envelope(writer, id);
            writer.WriteStartObject("result");
            writer.WriteStartArray("tools");

            writer.WriteStartObject();
            writer.WriteString("name", ReadName);

            // THE WORDING A DEPLOYMENT ALREADY PROVED. This is what the
            // installed reader tells an agent, and changing it at the same
            // time as changing the language underneath would make any shift in
            // agent behaviour impossible to attribute to either.
            writer.WriteString("description",
                "Read one work item: its type, state, title, description and acceptance "
              + "criteria. Use this to find out what the flight is about.");
            writer.WriteStartObject("inputSchema");
            writer.WriteString("type", "object");
            writer.WriteStartObject("properties");
            writer.WriteStartObject("id");
            writer.WriteString("type", "string");
            writer.WriteString("description", "The work item's numeric id.");
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteStartArray("required");
            writer.WriteStringValue("id");
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteStartObject();
            writer.WriteString("name", BrowseTool.Name);
            writer.WriteString("description",
                "List work items to choose from: id, title, state, url and when each last "
              + "changed. Use this to find work, not to understand one item - call "
              + ReadName + " once you have picked one.");
            writer.WriteStartObject("inputSchema");
            writer.WriteString("type", "object");
            writer.WriteStartObject("properties");

            writer.WriteStartObject(BrowseTool.Paging.Cursor);
            writer.WriteString("type", "string");
            writer.WriteString("description",
                "Where to continue from, as returned in "
              + BrowseTool.Paging.NextCursor + ". Omit for the first page.");
            writer.WriteEndObject();

            writer.WriteStartObject(BrowseTool.Paging.Limit);
            writer.WriteString("type", "integer");
            writer.WriteString("description",
                $"How many to return. Defaults to {DefaultLimit}, capped at {MaximumLimit}.");
            writer.WriteEndObject();

            writer.WriteEndObject();

            // NEITHER IS REQUIRED. A first page with a default size is the
            // ordinary call, and making a caller name a cursor to get one
            // would be a contract that cannot be started.
            writer.WriteStartArray("required");
            writer.WriteEndArray();

            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

    private static async Task<string> CalledAsync(
        JsonElement id, JsonElement message, IWorkItemSource source,
        CancellationToken cancellationToken)
    {
        var parameters = message.TryGetProperty("params", out var given) ? given : default;
        var name = parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty("name", out var named)
            ? named.GetString()
            : null;
        var arguments = parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty("arguments", out var supplied)
            ? supplied
            : default;

        try
        {
            return name switch
            {
                ReadName => await ReadAsync(id, arguments, source, cancellationToken),
                BrowseTool.Name => await BrowseAsync(id, arguments, source, cancellationToken),
                _ => Error(id, -32602,
                    $"'{name}' is not a tool this server has. It has {ReadName} and "
                  + BrowseTool.Name + "."),
            };
        }
        catch (Exception problem) when (problem is not OperationCanceledException)
        {
            // A TOOL ERROR, NOT A DEAD SERVER. The tracker being unreachable is
            // something an agent can say out loud and stop for; a closed pipe
            // is something it can only fail at.
            return Failed(id, problem.Message);
        }
    }

    private static async Task<string> ReadAsync(
        JsonElement id, JsonElement arguments, IWorkItemSource source,
        CancellationToken cancellationToken)
    {
        var wanted = arguments.ValueKind == JsonValueKind.Object
                  && arguments.TryGetProperty("id", out var given)
            ? Text(given)
            : null;

        if (string.IsNullOrWhiteSpace(wanted))
        {
            return Failed(id, "This tool needs the work item's id, and none was given.");
        }

        var item = await source.ReadAsync(wanted, cancellationToken);

        return item is null
            ? Failed(id, $"There is no work item {wanted} at this tracker.")
            : Content(id, Rendered(item));
    }

    private static async Task<string> BrowseAsync(
        JsonElement id, JsonElement arguments, IWorkItemSource source,
        CancellationToken cancellationToken)
    {
        var cursor = arguments.ValueKind == JsonValueKind.Object
                  && arguments.TryGetProperty(BrowseTool.Paging.Cursor, out var from)
            ? Text(from)
            : null;

        var asked = arguments.ValueKind == JsonValueKind.Object
                 && arguments.TryGetProperty(BrowseTool.Paging.Limit, out var many)
                 && many.ValueKind is JsonValueKind.Number or JsonValueKind.String
                 && int.TryParse(Text(many), out var parsed)
            ? parsed
            : DefaultLimit;

        var limit = Math.Clamp(asked, 1, MaximumLimit);
        var page = await source.BrowseAsync(cursor, limit, cancellationToken);

        // THE CONTRACT'S OWN SHAPE, written by its own names. A pane parses
        // this, so a field spelled differently here is a field it cannot find.
        var body = Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray(BrowseTool.Paging.Items);
            foreach (var item in page.Items)
            {
                writer.WriteStartObject();
                writer.WriteString(BrowseTool.Fields.Id, item.Id);
                writer.WriteString(BrowseTool.Fields.Title, item.Title);
                writer.WriteString(BrowseTool.Fields.State, item.State);
                writer.WriteString(BrowseTool.Fields.Url, item.Url);
                writer.WriteString(BrowseTool.Fields.Updated, item.Updated);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            if (page.NextCursor is { Length: > 0 } next)
            {
                writer.WriteString(BrowseTool.Paging.NextCursor, next);
            }
        });

        return Content(id, body);
    }

    /// <summary>
    /// The item as a person reads it, in the order the deployment settled on.
    /// </summary>
    /// <remarks>
    /// Matched to what the installed reader renders today, field for field and
    /// label for label. Changing the language and the rendering in one step
    /// would leave any shift in agent behaviour unattributable to either.
    /// </remarks>
    private static string Rendered(WorkItem item)
    {
        var rendered = new StringBuilder();

        Append(rendered, "Type", item.Type);
        Append(rendered, "State", item.State);
        Append(rendered, "Title", item.Title);
        Append(rendered, "Description", item.Description);
        Append(rendered, "Acceptance criteria", item.AcceptanceCriteria);
        Append(rendered, "Tags", item.Tags);

        return rendered.ToString().TrimEnd();
    }

    private static void Append(StringBuilder rendered, string label, string? value)
    {
        // AN ABSENT FIELD IS OMITTED, not rendered as an empty heading. A list
        // of labels with nothing after them reads as a tracker that lost the
        // data rather than one that never held it.
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        rendered.Append(label).Append(": ").Append(value).Append('\n');
    }

    /// <summary>A json value as text, whether the caller quoted it or not.</summary>
    /// <remarks>
    /// An id is declared as a string and arrives as a number often enough that
    /// refusing the number would be a server that is right and useless.
    /// </remarks>
    private static string? Text(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        _ => null,
    };

    private static string Content(JsonElement id, string text) =>
        Write(writer =>
        {
            Envelope(writer, id);
            writer.WriteStartObject("result");
            writer.WriteStartArray("content");
            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", text);
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteBoolean("isError", false);
            writer.WriteEndObject();
        });

    /// <summary>
    /// A tool that could not answer, said as a result rather than an error.
    /// </summary>
    /// <remarks>
    /// <c>isError</c> on a RESULT is what the protocol gives the model to read;
    /// a JSON-RPC error is for the client and the agent never sees the text. An
    /// agent that cannot find its work item needs to be told why.
    /// </remarks>
    private static string Failed(JsonElement id, string why) =>
        Write(writer =>
        {
            Envelope(writer, id);
            writer.WriteStartObject("result");
            writer.WriteStartArray("content");
            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", why);
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteBoolean("isError", true);
            writer.WriteEndObject();
        });

    private static string Error(JsonElement id, int code, string message) =>
        Write(writer =>
        {
            Envelope(writer, id);
            writer.WriteStartObject("error");
            writer.WriteNumber("code", code);
            writer.WriteString("message", message);
            writer.WriteEndObject();
        });

    private static void Envelope(Utf8JsonWriter writer, JsonElement id)
    {
        writer.WriteStartObject();
        writer.WriteString("jsonrpc", "2.0");
        writer.WritePropertyName("id");
        id.WriteTo(writer);
    }

    private static string Write(Action<Utf8JsonWriter> body)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
