using System.Text;
using System.Text.Json;
using Gg.Client;
using Gg.Local;

namespace Gg.Cli;

/// <summary>
/// <c>gg manage tools</c>: gg's management layer as MCP tools, for an agent a person runs beside
/// gg (owner's call, 2026-10-08).
/// </summary>
/// <remarks>
/// <para>
/// <b>A CHILD, NEVER A CLIENT</b>, for <c>pull_airspace</c>'s reason. Every tool re-execs the
/// <c>gg</c> verb a person would type, with <c>--json</c>, so the credential and the network stay
/// in the process whose job they are, this server holds no session, and gg's refusals come back
/// in gg's own words rather than as whatever a guessed command line printed.
/// </para>
/// <para>
/// <b>Reads are granted at launch; acts never are</b> (<see cref="ManageTool"/>). An act changes
/// what happens to somebody's work, so Claude Code asks the person before each call.
/// </para>
/// <para>
/// <b>No value may read as a flag.</b> Arguments go to the verb as words; one starting with
/// <c>-</c> would be parsed as an option - <c>--all</c>, <c>--hand</c>, <c>--declare-names</c> -
/// so it is refused and nothing runs.
/// </para>
/// </remarks>
public static class ManageToolServer
{
    /// <summary>One argument a tool takes.</summary>
    private sealed record Field(string Name, string Description, bool Required, IReadOnlyList<string>? Choices = null);

    /// <summary>One tool: what it says, what it takes, and the verb it runs.</summary>
    private sealed record Tool(
        string Name, string Description, IReadOnlyList<Field> Fields, Func<Func<string, string?>, IReadOnlyList<string>> Verb);

    private static readonly Field Flight = new("flight", "The flight: its number (GG-42) or its id.", Required: true);

    private static readonly IReadOnlyList<Tool> Tools =
    [
        new(ManageTool.WhoAmI, "Who gg is signed in as, in which tenant, and what they may do. Reads only.",
            [], _ => ["whoami"]),
        new(ManageTool.ListGates,
            "The gates waiting on a person: each flight held for a decision, and what it waits on. Reads only.",
            [], _ => ["gates"]),
        new(ManageTool.ListBoard,
            "The board: nominations - proposed work - waiting to be opened or declined, with each one's id "
          + "and reason. Reads only.",
            [], _ => ["board"]),
        new(ManageTool.ListFlights, "Recent flights, with their numbers, stages and states. Reads only.",
            [], _ => ["flights"]),
        new(ManageTool.ShowFlight, "One flight in full: what it is for, where it is, and what governs it. Reads only.",
            [Flight], a => ["show", a("flight")!]),
        new(ManageTool.FlightLog, "One flight's log: what happened to it, in order. Reads only.",
            [Flight], a => ["log", a("flight")!]),
        new(ManageTool.WhyFlight,
            "Why a flight is where it is - what holds it, or one obligation's verdict when named. Reads only.",
            [Flight, new("obligation", "One obligation to explain, by name.", Required: false)],
            a => a("obligation") is { } obligation ? ["why", a("flight")!, obligation] : ["why", a("flight")!]),
        new(ManageTool.ListRunners, "The fleet's runners: which are up, busy or offline, and what each holds. Reads only.",
            [], _ => ["runners"]),
        new(ManageTool.ListWatches, "The watches that sweep for work, and when each sweeps next. Reads only.",
            [], _ => ["watches"]),

        new(ManageTool.DecideGate,
            "Answer a gate on a flight, as the signed-in person: approved or rejected, with a reason. "
          + "The person is asked before every call - only call it when they have said how to answer.",
            [Flight,
             new("obligation", "The obligation the gate is for, by name, as list_gates shows it.", Required: true),
             new("outcome", "The answer.", Required: true, ["approved", "rejected"]),
             new("reason", "Why, in the person's words.", Required: false)],
            a => a("reason") is { } reason
                ? ["decide", a("flight")!, a("obligation")!, a("outcome")!, reason]
                : ["decide", a("flight")!, a("obligation")!, a("outcome")!]),
        new(ManageTool.AnswerNomination,
            "Open or decline a nomination on the board, as the signed-in person, saying why. Opening "
          + "starts a flight. The person is asked before every call.",
            [new("nomination", "The nomination's id, as list_board shows it.", Required: true),
             new("answer", "Open it, or decline it.", Required: true, ["open", "decline"]),
             new("because", "Why - it is what tells a later reader why this was opened or declined.", Required: true)],
            a => ["board", a("answer")!, a("nomination")!, a("because")!]),
        new(ManageTool.Fly,
            "Open a flight, as the signed-in person: an intent saying what it should achieve, and the "
          + "work kind when the person named one. It takes a number and a fleet agent flies it. "
          + "The person is asked before every call.",
            [new("intent", "What the flight should achieve, in a sentence or a short paragraph.", Required: true),
             new("work_kind", "The work kind, by name.", Required: false)],
            a => a("work_kind") is { } kind
                ? ["fly", a("intent")!, "--work-kind", kind]
                : ["fly", a("intent")!]),
        new(ManageTool.Ground,
            "Ground a flight, as the signed-in person, saying why - it stops the work. The person is "
          + "asked before every call.",
            [Flight, new("because", "Why it is grounded.", Required: true)],
            a => ["ground", a("flight")!, a("because")!]),
        new(ManageTool.DeclareName,
            "Declare a name in the airspace - a work kind, destination, environment and so on - as the "
          + "signed-in person. The person is asked before every call.",
            [new("role", "What the name is: work-kind, destination, environment, watch, …", Required: true),
             new("name", "The name.", Required: true),
             new("under", "The name it sits under, when it has a parent.", Required: false)],
            a => a("under") is { } parent
                ? ["airspace", "name", a("role")!, a("name")!, "--under", parent]
                : ["airspace", "name", a("role")!, a("name")!]),
        new(ManageTool.RetireName,
            "Retire a declared name from the airspace, as the signed-in person. The person is asked "
          + "before every call.",
            [new("name", "The name to retire.", Required: true)],
            a => ["airspace", "retire", a("name")!]),
    ];

    public static async Task<int> RunAsync(
        TextReader input, TextWriter output, RunVerb run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(run);

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
                // SKIPPED, NEVER FATAL, as the siblings do: a dead server loses the agent its
                // tools for the session.
                continue;
            }

            using (message)
            {
                if (Answer(message.RootElement, run) is { } answer)
                {
                    await output.WriteLineAsync(answer);
                    await output.FlushAsync(cancellationToken);
                }
            }
        }

        return 0;
    }

    private static string? Answer(JsonElement message, RunVerb run)
    {
        var method = message.TryGetProperty("method", out var named) ? named.GetString() : null;

        if (!message.TryGetProperty("id", out var id) || id.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return method switch
        {
            "initialize" => Initialized(id, message),
            "tools/list" => Listed(id),
            "tools/call" => Called(id, message, run),
            _ => Error(id, -32601,
                $"'{method}' is not a method this server has. It has initialize, tools/list and tools/call."),
        };
    }

    private static string Initialized(JsonElement id, JsonElement message) =>
        Write(writer =>
        {
            Envelope(writer, id);
            writer.WriteStartObject("result");
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
            writer.WriteString("name", ManageTool.Server);
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

            foreach (var tool in Tools)
            {
                writer.WriteStartObject();
                writer.WriteString("name", tool.Name);
                writer.WriteString("description", tool.Description);
                writer.WriteStartObject("inputSchema");
                writer.WriteString("type", "object");
                writer.WriteStartObject("properties");
                foreach (var field in tool.Fields)
                {
                    writer.WriteStartObject(field.Name);
                    writer.WriteString("type", "string");
                    writer.WriteString("description", field.Description);
                    if (field.Choices is { } choices)
                    {
                        writer.WriteStartArray("enum");
                        foreach (var choice in choices)
                        {
                            writer.WriteStringValue(choice);
                        }

                        writer.WriteEndArray();
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
                writer.WriteStartArray("required");
                foreach (var field in tool.Fields.Where(f => f.Required))
                {
                    writer.WriteStringValue(field.Name);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

    private static string Called(JsonElement id, JsonElement message, RunVerb run)
    {
        var parameters = message.TryGetProperty("params", out var given) ? given : default;
        var called = parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty("name", out var name)
                ? name.GetString()
                : null;
        var arguments = parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty("arguments", out var supplied)
            && supplied.ValueKind == JsonValueKind.Object
                ? supplied
                : default;

        if (Tools.FirstOrDefault(t => string.Equals(t.Name, called, StringComparison.Ordinal)) is not { } tool)
        {
            return Content(id, isError: true,
                $"'{called}' is not a tool this server has. It has: {string.Join(", ", Tools.Select(t => t.Name))}.");
        }

        string? Value(string field) =>
            arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty(field, out var value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { } text
            && !string.IsNullOrWhiteSpace(text)
                ? text.Trim()
                : null;

        // EVERY REFUSAL BEFORE ANYTHING RUNS: a missing value would run a different verb, a value
        // outside its choices one gg would refuse less clearly, and one starting with '-' a flag.
        foreach (var field in tool.Fields)
        {
            var value = Value(field.Name);
            if (value is null)
            {
                if (field.Required)
                {
                    return Content(id, isError: true,
                        $"Refused: {tool.Name} needs '{field.Name}' - {field.Description} Nothing was run.");
                }

                continue;
            }

            if (value.StartsWith('-'))
            {
                return Content(id, isError: true,
                    $"Refused: '{field.Name}' starts with '-', which gg would read as a flag rather than "
                  + "a value. Nothing was run.");
            }

            if (field.Choices is { } choices && !choices.Contains(value, StringComparer.Ordinal))
            {
                return Content(id, isError: true,
                    $"Refused: '{field.Name}' is one of {string.Join(", ", choices)}; '{value}' is not. "
                  + "Nothing was run.");
            }
        }

        var report = run([.. tool.Verb(Value), "--json"]);

        if (!report.Started)
        {
            return Content(id, isError: true, $"gg did not run: {report.Said}");
        }

        return Content(id, isError: report.ExitCode != 0,
            report.Said is { Length: > 0 } said ? said : "gg said nothing.");
    }

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
            writer.WriteBoolean("isError", isError);
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
