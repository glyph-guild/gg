namespace Gg.Local;

/// <summary>Which of the three answered.</summary>
/// <remarks>
/// <b>Four values, because "nobody said anything and there is no default" is a
/// real answer.</b> An unset <c>GG_VCS_HOSTS</c> means no adapters, which is a
/// state a machine is in rather than a value it is missing — folding it into
/// <see cref="Default"/> would say a default exists where none does.
/// </remarks>
public static class SettingSources
{
    /// <summary>An environment variable, which wins.</summary>
    public const string Environment = "environment";

    /// <summary>The configuration file, when the environment is silent.</summary>
    public const string File = "file";

    /// <summary>Built in, when neither said anything.</summary>
    public const string Default = "default";

    /// <summary>Nobody said anything and there is nothing built in.</summary>
    public const string Unset = "unset";

    public static IReadOnlyList<string> All { get; } = [Environment, File, Default, Unset];
}

/// <summary>One setting, resolved, with what it is overriding.</summary>
public sealed record ResolvedSetting
{
    public required string Variable { get; init; }

    /// <summary>What answered, or null when nothing did.</summary>
    public string? Value { get; init; }

    /// <summary>One of <see cref="SettingSources"/>.</summary>
    public required string Source { get; init; }

    /// <summary>
    /// The file's value, when an environment variable is overriding it.
    /// </summary>
    /// <remarks>
    /// <b>The whole reason precedence is safe to have.</b> Without this a person
    /// edits the file, sees nothing change, and has nowhere to find out why.
    /// Null whenever the file is not being overridden — including when the file
    /// said nothing, so "no shadow" and "a shadow of nothing" cannot look alike.
    /// </remarks>
    public string? Shadowed { get; init; }
}

/// <summary>
/// The one resolution: environment, then file, then built-in default.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here rather than in <c>Gg.Cli</c>, because three projects need it.</b>
/// <c>Gg.Console</c> reads <c>EDITOR</c> and <c>GG_TAKE_COMMAND</c> in its own
/// sessions and cannot see <c>Gg.Cli</c>; <c>Gg.Runner</c> reads its own; the
/// composition root reads the rest. A resolution in any one of them would leave
/// the other two reaching a different answer, which is the drift
/// <c>ExecutorConfiguration</c>'s rule already names.
/// </para>
/// <para>
/// <b>The environment wins.</b> Nothing configured today stops working, a
/// one-off override in front of a command keeps working, and a container that
/// is handed variables needs no file at all — which is what keeps a pool member
/// exactly as it is.
/// </para>
/// </remarks>
public static class Settings
{
    /// <summary>What a variable falls back to when nobody set it.</summary>
    /// <remarks>
    /// <b>Gathered here from the call sites that each carried their own.</b>
    /// <c>?? "vi"</c> sat in two files and <c>?? "claude"</c> in two more, so
    /// "what is the default" had four answers and the page could quote none of
    /// them. Most settings have no default and are absent from this table.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> Defaults { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GG_CONTROL_PLANE"] = "http://localhost:5199",
            ["EDITOR"] = "vi",
            ["GG_TAKE_COMMAND"] = "claude",
            ["GG_RUNNER_HOLD_SECONDS"] = "10",
        };

    /// <summary>Whether the file has anywhere to put this one.</summary>
    public static bool CanBeInTheFile(string variable) =>
        Configuration.Members.Any(m => string.Equals(m.Variable, variable, StringComparison.Ordinal));

    /// <summary>The value in force, and where it came from.</summary>
    /// <param name="environment">
    /// How to read a variable. Null reads the process — and the override is here
    /// for the reason <c>LocalPaths</c> states: the environment is
    /// process-global and a suite that runs four-wide cannot have one test
    /// setting it while another reads it.
    /// </param>
    public static ResolvedSetting Resolve(
        string variable,
        Configuration? file = null,
        Func<string, string?>? environment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variable);

        var read = environment ?? System.Environment.GetEnvironmentVariable;

        // BLANK IS NOT SET. `export GG_VCS_HOSTS=` leaves an empty string in the
        // environment, and reading that as a declaration would hand every parser
        // downstream an empty list where the author meant to clear the line.
        var declared = read(variable) is { Length: > 0 } value ? value : null;
        var inTheFile = file is null ? null : Of(file, variable);

        if (declared is not null)
        {
            return new ResolvedSetting
            {
                Variable = variable,
                Value = declared,
                Source = SettingSources.Environment,
                Shadowed = inTheFile,
            };
        }

        if (inTheFile is not null)
        {
            return new ResolvedSetting
            {
                Variable = variable,
                Value = inTheFile,
                Source = SettingSources.File,
            };
        }

        return Defaults.TryGetValue(variable, out var fallback)
            ? new ResolvedSetting
            {
                Variable = variable,
                Value = fallback,
                Source = SettingSources.Default,
            }
            : new ResolvedSetting { Variable = variable, Source = SettingSources.Unset };
    }

    /// <summary>The value in force, for a caller that only wants the string.</summary>
    public static string? Value(
        string variable,
        Configuration? file = null,
        Func<string, string?>? environment = null) =>
        Resolve(variable, file, environment).Value;

    /// <summary>What the file says about one variable, or null.</summary>
    private static string? Of(Configuration file, string variable) =>
        Configuration.Members
            .Where(m => string.Equals(m.Variable, variable, StringComparison.Ordinal))
            .Select(m => m.Get(file))
            .FirstOrDefault();

    /// <summary>
    /// A configuration recording what somebody has actually set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only what the environment declares</b> — not the defaults. Writing a
    /// default into the file would pin it: the machine would keep that value
    /// after the built-in one changed, and nothing on the page would say why it
    /// differed from a fresh machine's. A seeded file is a recording of what a
    /// person chose, and nobody chose a default.
    /// </para>
    /// <para>
    /// <b>Which is what makes seeding safe.</b> Nothing it writes changes a
    /// resolved value, because every value it writes was already answering —
    /// and the environment still wins over it anyway.
    /// </para>
    /// </remarks>
    public static Configuration Seed(Func<string, string?>? environment = null)
    {
        var seeded = new Configuration();

        foreach (var member in Configuration.Members)
        {
            if (Resolve(member.Variable, file: null, environment).Source
                == SettingSources.Environment
             && Resolve(member.Variable, file: null, environment).Value is { Length: > 0 } value)
            {
                seeded = member.With(seeded, value);
            }
        }

        return seeded;
    }

    /// <summary>The configuration with one setting changed.</summary>
    /// <remarks>
    /// <b>Production, not a test seam.</b> <c>gg config set</c> is how a value
    /// is written without hand-editing the document, which the measurement made
    /// the ordinary path: four settings are in play anywhere, so a person
    /// changing one is changing a quarter of their configuration.
    /// </remarks>
    public static Configuration With(Configuration configuration, string variable, string value)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        foreach (var member in Configuration.Members)
        {
            if (string.Equals(member.Variable, variable, StringComparison.Ordinal))
            {
                return member.With(configuration, value);
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(variable),
            variable,
            $"'{variable}' is not a setting the file can carry. It carries: "
          + string.Join(", ", Configuration.Members.Select(m => m.Key)) + ".");
    }
}
