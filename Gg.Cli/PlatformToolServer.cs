using System.Text;
using System.Text.Json;
using Gg.Client;
using Gg.Runner.Execution;

namespace Gg.Cli;

/// <summary>
/// The platform's own tool server: its tools, over line-delimited JSON-RPC on
/// standard input and output.
/// </summary>
/// <remarks>
/// <para>
/// <b>A structured channel, because prose is not an answer.</b> A classifier
/// has to hand back a VALUE - a work kind - and a closing summary that happens
/// to mention one is a sentence somebody could have written about anything. A
/// tool call is a thing the agent chose to make, in a shape the runner reads
/// mechanically, and it is a narrower thing to trust than a sentence the agent
/// was told to write. The same argument covers an agent saying it is stuck.
/// </para>
/// <para>
/// <b>ONE CHANNEL, TWO TOOLS, and they are granted on opposite terms.</b>
/// Nominating a work kind is the whole output of one KIND of work, so an
/// envelope that never declares `propose` has no business granting it. Asking
/// for a decision is not a move at all and no envelope may withhold it: one
/// able to would be one that makes a stuck agent silent. Both are listed here
/// and the grant is decided in the one place a grant is already decided - the
/// launch's allow-list - because a `tools/list` that varied by envelope would
/// be a second place the same rule lives.
/// </para>
/// <para>
/// <b>STDOUT IS THE PROTOCOL.</b> Nothing here may narrate, log, or greet: one
/// stray line and the client sees a server that never initialized rather than a
/// tool that failed. This type therefore reaches no session store, no
/// configuration, and no control plane - it is a pure function of the lines it
/// is handed.
/// </para>
/// <para>
/// <b>It holds nothing open and needs nothing.</b> No credential, no session,
/// no round trip: it validates two strings and returns a receipt. That is what
/// makes it safe to run as a child of a process the threat model treats as
/// compromised - an injected agent that reaches it can at most record a request
/// admission will refuse against a menu a person wrote.
/// </para>
/// <para>
/// <b>Hand-written rather than an SDK.</b> <c>Gg.Runner</c> carries no package
/// references and this binary publishes AOT; the official server library is DI-
/// and reflection-shaped. What is needed is four methods over line-delimited
/// JSON - the shape the launch's own config writer already uses, and for the
/// same stated reason.
/// </para>
/// </remarks>
public static class PlatformToolServer
{
    /// <summary>What the agent needs decided. One argument, and it is required.</summary>
    private const string QuestionArgument = "question";

    private const string WorkKindArgument = "work_kind";
    private const string ReasonArgument = "reason";

    /// <summary>
    /// Serves until the input ends, and answers nothing else.
    /// </summary>
    /// <returns>
    /// Zero. A tool server's exit code is not a verdict on the work - the agent
    /// that launched it is long gone by the time anybody reads one.
    /// </returns>
    public static async Task<int> RunAsync(
        TextReader input, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

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
                if (Answer(message.RootElement) is { } answer)
                {
                    await output.WriteLineAsync(answer);
                    await output.FlushAsync(cancellationToken);
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// The line to write back, or null where the protocol says to write none.
    /// </summary>
    private static string? Answer(JsonElement message)
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
            "tools/call" => Called(id, message),

            // THE ID COMES BACK even on an error, or a client matching
            // responses to requests waits for ever.
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

            // ECHOED RATHER THAN DECLARED. The client has already said which
            // revision it speaks; claiming a different one would be this
            // server asserting something about a protocol it does not own.
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
            writer.WriteString("name", NominationTool.Server);
            // The binary's own version, the one `gg version` reports. The
            // client ignores it; a person reading a transcript does not.
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

            writer.WriteString("name", NominationTool.Name);

            // THE PROMPT AN AGENT ACTUALLY READS. It says to call once, that
            // declining is a real answer, and that nominating grants nothing -
            // because an agent that thinks it has opened a flight stops
            // waiting for one, and an agent that thinks it must choose will
            // choose from an item that does not say.
            writer.WriteString("description",
                "Nominate the kind of work this item needs. Call it once with the kind you "
              + "choose and the reason, then stop and say what you nominated and why. "
              + "Nominating grants nothing and opens nothing: a person decides whether the "
              + "kind you name is one this work may become. If the item does not say enough "
              + "to choose, do NOT call this - say which question you could not answer and "
              + "stop. Declining is a real answer and it is not a failure.");

            writer.WriteStartObject("inputSchema");
            writer.WriteString("type", "object");

            writer.WriteStartObject("properties");
            writer.WriteStartObject(WorkKindArgument);
            writer.WriteString("type", "string");
            writer.WriteString("description", "One of the work kinds you were offered.");
            writer.WriteEndObject();
            writer.WriteStartObject(ReasonArgument);
            writer.WriteString("type", "string");
            writer.WriteString("description",
                "Why this kind and not the others, in your own words.");
            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteStartArray("required");
            writer.WriteStringValue(WorkKindArgument);
            writer.WriteStringValue(ReasonArgument);
            writer.WriteEndArray();

            writer.WriteEndObject();
            writer.WriteEndObject();

            // THE SECOND TOOL, and its description is the part that decides
            // whether it is ever used. An agent is otherwise being told to
            // complete a task by a system it cannot see, so it has to be told
            // in as many words that stopping to ask is a real answer - and
            // that it must not do a different piece of work instead, which is
            // the second-best-looking thing a stuck agent can do.
            writer.WriteStartObject();
            writer.WriteString("name", HelpTool.Name);
            writer.WriteString("description",
                "Ask for a decision you are not allowed to make: a question only a person "
              + "can answer, or two ways forward with nothing in the tree to choose between "
              + "them. Call it once with the question, then stop and say what you did and "
              + "what you were left with. Asking is not failing. Do not guess, and do not do "
              + "a different piece of work instead. This grants nothing and changes nothing: "
              + "a person reads the question and answers it, and the work comes back to you "
              + "with their answer.");

            writer.WriteStartObject("inputSchema");
            writer.WriteString("type", "object");
            writer.WriteStartObject("properties");
            writer.WriteStartObject(QuestionArgument);
            writer.WriteString("type", "string");
            writer.WriteString("description",
                "What you need decided, in your own words. Say what you were doing, what the "
              + "choices are, and what you could not tell from the work itself.");
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteStartArray("required");
            writer.WriteStringValue(QuestionArgument);
            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteEndObject();

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

    private static string Called(JsonElement id, JsonElement message)
    {
        var parameters = message.TryGetProperty("params", out var given) ? given : default;

        var arguments = parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty("arguments", out var supplied)
            && supplied.ValueKind == JsonValueKind.Object
                ? supplied
                : default;

        var called = parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty("name", out var name)
                ? name.GetString()
                : null;

        if (string.Equals(called, HelpTool.Name, StringComparison.Ordinal))
        {
            return Asked(id, arguments);
        }

        // NOT AN UNKNOWN-TOOL ARM, deliberately. The nomination tool is what
        // this server answered before there were two, and a call with no name
        // at all is still that one - which keeps every client written against
        // the one-tool server working rather than making its calls vanish.
        var workKind = Text(arguments, WorkKindArgument);
        var reason = Text(arguments, ReasonArgument);

        // AN ERROR RESULT RATHER THAN A PROTOCOL ERROR. The call reached the
        // tool and the tool refused it, which is something the agent can read
        // and fix - and something the extractor must not read as a nomination,
        // because half a nomination is a value the runner would have to invent
        // the rest of.
        if (workKind is null || reason is null)
        {
            return Content(id, isError: true,
                $"Refused: a nomination needs both '{WorkKindArgument}' and "
              + $"'{ReasonArgument}'. Nothing was recorded.");
        }

        // ECHOED BACK IN CANONICAL FORM, so an agent can see what was taken
        // rather than assume its own spelling survived.
        return Content(id, isError: false,
            $"Recorded: work kind '{workKind}'. This grants nothing and opens nothing - a "
          + "person decides whether a flight of that kind is opened. Your part is done: stop "
          + "now and say what you nominated and why.");
    }

    /// <summary>
    /// Takes a question and returns a receipt. It does not wait.
    /// </summary>
    /// <remarks>
    /// <b>Rule 4: ask, stop, release.</b> A tool that waited for a person would
    /// hold a lease across human latency, and a lease held that long is a lease
    /// that expires and a flight that gets taken over - so every question would
    /// arrive on the takeover path instead of the gate path. The runner does
    /// not depend on the agent obeying the instruction to stop either: the
    /// outcome is decided from the stream, so an agent that asks and then keeps
    /// working is recorded accurately rather than taken at its word.
    /// </remarks>
    private static string Asked(JsonElement id, JsonElement arguments)
    {
        var question = Text(arguments, QuestionArgument);

        // AN ERROR RESULT RATHER THAN A PROTOCOL ERROR, the same as the tool
        // beside it: the call reached the tool and the tool refused it, which
        // is something the agent can read and fix. And an empty question is
        // worse than none - it opens a gate a person cannot answer.
        return question is null
            ? Content(id, isError: true,
                $"Refused: a question needs words in it. Say what you were doing, what the "
              + "choices are, and what you could not tell from the work itself. Nothing was "
              + "recorded.")
            : Content(id, isError: false,
                "Recorded. Your part is done: stop now and say what you did and what you were "
              + "left with. This grants nothing and changes nothing - a person reads the "
              + "question and answers it, and the work comes back to you with their answer.");
    }

    private static string? Text(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object
        && arguments.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text
        && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static string Content(JsonElement id, bool isError, string text) =>
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

            if (isError)
            {
                writer.WriteBoolean("isError", true);
            }

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

    /// <summary>
    /// One JSON-RPC message, as one line.
    /// </summary>
    /// <remarks>
    /// <c>Utf8JsonWriter</c> rather than a serializer, because this binary is
    /// published AOT and there is no model to reflect over anyway - the shape
    /// is the protocol's, not ours. It also escapes every value, which matters:
    /// the reason a receipt echoes came from an agent.
    /// </remarks>
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
