using System.Text.Json;

namespace Gg.Runner.Execution;

/// <summary>
/// Where the agent wrote its own record of the session, read out of the stream
/// it also wrote.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two files, and neither is a superset of the other.</b> gg captures the
/// agent's output stream; the agent separately keeps a session file of its own.
/// gg's holds the teardown — <c>background_tasks_changed</c>,
/// <c>task_updated: {status: "killed"}</c> — and drops the initial message.
/// The agent's holds the composed prompt, the full message content and every
/// tool input, and holds none of the teardown. Diagnosing GG-309 needed both.
/// </para>
/// <para>
/// <b>Read from the stream, never reconstructed.</b> The <c>system/init</c>
/// record states the session's id and, in <c>memory_paths.auto</c>, the
/// directory the agent is keeping it under. Both are the agent's own words about
/// its own layout, which is the only honest source for a path that is not ours
/// to define.
/// </para>
/// <para>
/// <b>Pure, and it touches no disk.</b> A function of the stream and a home
/// directory, for the reason <see cref="TranscriptDigest"/> is: what has to be
/// right is the derivation, and whether a file is there is the caller's
/// question.
/// </para>
/// </remarks>
public static class ClaudeSession
{
    /// <summary>
    /// The session file this stream describes, or null when it describes none.
    /// </summary>
    /// <remarks>
    /// <b>Null is ordinary and is not a failure.</b> An attended session, the
    /// move-bound probe and a sweep all reach an executor differently, and a
    /// stream that never announced itself names no session. Returning a derived
    /// path for a file nobody wrote would be a reference that cannot be
    /// followed, which this repository treats as a bug rather than a gap.
    /// </remarks>
    public static string? FileIn(string recorded, string home)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(home);

        foreach (var line in recorded.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed[0] != '{')
            {
                continue;
            }

            JsonDocument document;

            try
            {
                document = JsonDocument.Parse(trimmed);
            }
            catch (JsonException)
            {
                // A PARTIAL LINE IS NOT AN ERROR HERE. The stream is captured as
                // it arrives and a torn last line is what a killed process
                // leaves; the init record is the first one and is long past.
                continue;
            }

            using (document)
            {
                var root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object
                    || !Text(root, "type").Equals("system", StringComparison.Ordinal)
                    || !Text(root, "subtype").Equals("init", StringComparison.Ordinal))
                {
                    continue;
                }

                if (Text(root, "session_id") is not { Length: > 0 } session)
                {
                    // ANNOUNCED WITHOUT AN ID IS NOT A SESSION WE CAN NAME, and
                    // guessing one would be the unfollowable reference above.
                    return null;
                }

                return Path.Combine(DirectoryFor(root, home), session + ".jsonl");
            }
        }

        return null;
    }

    /// <summary>
    /// The directory name the agent gives a working tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Somebody else's rule, recorded rather than designed.</b> Every
    /// character that is not a letter, a digit or a dash becomes a dash, so a
    /// leading slash becomes a leading dash and <c>/.cache</c> becomes
    /// <c>--cache</c>. It is deliberately NOT
    /// <c>TranscriptStore</c>'s own <c>Safe</c>, which keeps underscores: that
    /// one is ours and may change when we like, and this one must not drift from
    /// what the agent actually does.
    /// </para>
    /// <para>
    /// <b>The fallback, not the first answer.</b> <c>memory_paths.auto</c> names
    /// the directory outright and is preferred; this exists for a build that
    /// stops emitting it, so losing that member degrades to a derivation rather
    /// than to no session at all.
    /// </para>
    /// </remarks>
    public static string ProjectSlug(string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(workingDirectory);

        return new([.. workingDirectory.Select(
            c => char.IsAsciiLetterOrDigit(c) || c == '-' ? c : '-')]);
    }

    /// <summary>
    /// The project directory: the agent's own answer where it gives one.
    /// </summary>
    private static string DirectoryFor(JsonElement root, string home)
    {
        // THE AGENT'S OWN WORDS FIRST. memory_paths.auto is
        // `<home>/.claude/projects/<slug>/memory/`, so its parent is the
        // directory the session sits in - and taking it means a change to that
        // layout reaches us as a different string rather than as a wrong guess.
        if (root.TryGetProperty("memory_paths", out var paths)
            && paths.ValueKind == JsonValueKind.Object
            && Text(paths, "auto") is { Length: > 0 } auto
            && Path.GetDirectoryName(auto.TrimEnd('/', '\\')) is { Length: > 0 } project)
        {
            return project;
        }

        return Path.Combine(home, ".claude", "projects", ProjectSlug(Text(root, "cwd")));
    }

    /// <summary>A string member, or empty. Never null, so every caller reads the same way.</summary>
    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
