using Gg.Local;

namespace Gg.Console;

/// <summary>What an agent in the mux is doing, as its own hooks say.</summary>
public enum AgentActivity
{
    /// <summary>Nothing said yet - an agent with no hooks, or one that has not started a turn.</summary>
    None,

    /// <summary>A turn is under way.</summary>
    Working,

    /// <summary>The turn is over and the agent waits for the person's next prompt.</summary>
    Waiting,

    /// <summary>The agent is blocked on the person - a permission question.</summary>
    NeedsYou,
}

/// <summary>Claude Code's hooks, telling the mux what each agent is doing.</summary>
/// <remarks>
/// <b>Given for one launch, never installed.</b> The hooks reach Claude Code through
/// <c>--settings</c> on the command line the mux builds, so nothing is written to the person's
/// Claude Code settings and a session started outside gg runs no hook of ours. Each agent is given
/// a state file of its own in <see cref="Variable"/>; a hook writes one word there and the column
/// reads it. A hook can never fail the agent's turn: <c>gg mux mark</c> always exits 0.
/// </remarks>
public static class MuxActivity
{
    /// <summary>The environment variable naming an agent's state file.</summary>
    public const string Variable = "GG_MUX_ACTIVITY";

    /// <summary>Where state files live: beside the mux's session ledger.</summary>
    public static string Folder(string? stateHome = null) => Path.Combine(LocalPaths.StateRoot(stateHome), "mux", "activity");

    /// <summary>What a hook's word, and the JSON Claude Code gave it on stdin, say.</summary>
    public static AgentActivity Interpret(string word, string? hookInput) => word switch
    {
        "working" => AgentActivity.Working,
        "waiting" => AgentActivity.Waiting,
        "notification" => AsksPermission(hookInput) ? AgentActivity.NeedsYou : AgentActivity.Waiting,
        _ => AgentActivity.None,
    };

    /// <summary>
    /// Writes what <paramref name="word"/> says to <paramref name="path"/>, if the path is inside
    /// <paramref name="folder"/>; anything else is silently nothing.
    /// </summary>
    public static void Mark(string? path, string word, string? hookInput, string folder)
    {
        var activity = Interpret(word, hookInput);
        if (path is null || activity == AgentActivity.None || !Inside(path, folder))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + "." + Environment.ProcessId + ".tmp";
            File.WriteAllText(temporary, activity.ToString());
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A MARK IS A HINT: losing one costs a glyph, never the agent's turn.
        }
    }

    /// <summary>What the state file at <paramref name="path"/> says, or nothing.</summary>
    public static AgentActivity Read(string? path)
    {
        if (path is null)
        {
            return AgentActivity.None;
        }

        try
        {
            return Enum.TryParse<AgentActivity>(File.ReadAllText(path).Trim(), out var activity)
                ? activity
                : AgentActivity.None;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return AgentActivity.None;
        }
    }

    /// <summary>The settings JSON for <c>--settings</c>: four hooks, each running <c>gg mux mark</c>.</summary>
    public static string Settings(SelfInvocation self)
    {
        ArgumentNullException.ThrowIfNull(self);

        using var buffer = new MemoryStream();
        using (var json = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartObject("hooks");
            Hook(json, "UserPromptSubmit", null, Command(self, "working"));
            Hook(json, "PreToolUse", "*", Command(self, "working"));
            Hook(json, "Stop", null, Command(self, "waiting"));
            Hook(json, "Notification", null, Command(self, "notification"));
            json.WriteEndObject();
            json.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void Hook(System.Text.Json.Utf8JsonWriter json, string hookEvent, string? matcher, string command)
    {
        json.WriteStartArray(hookEvent);
        json.WriteStartObject();
        if (matcher is not null)
        {
            json.WriteString("matcher", matcher);
        }

        json.WriteStartArray("hooks");
        json.WriteStartObject();
        json.WriteString("type", "command");
        json.WriteString("command", command);
        json.WriteEndObject();
        json.WriteEndArray();
        json.WriteEndObject();
        json.WriteEndArray();
    }

    /// <summary>A shell command line, every word single-quoted, because Claude Code runs it in a shell.</summary>
    private static string Command(SelfInvocation self, string word) =>
        string.Join(' ', new[] { self.Command }.Concat(self.Under("mux", "mark", word)).Select(Quoted));

    private static string Quoted(string word) => "'" + word.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static bool AsksPermission(string? hookInput)
    {
        if (string.IsNullOrWhiteSpace(hookInput))
        {
            return false;
        }

        try
        {
            using var input = System.Text.Json.JsonDocument.Parse(hookInput);
            var root = input.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return false;
            }

            if (root.TryGetProperty("notification_type", out var type) && type.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return type.GetString() == "permission_prompt";
            }

            return root.TryGetProperty("message", out var message)
                && message.ValueKind == System.Text.Json.JsonValueKind.String
                && message.GetString()!.Contains("permission", StringComparison.OrdinalIgnoreCase);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static bool Inside(string path, string folder)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.Ordinal);
    }
}
