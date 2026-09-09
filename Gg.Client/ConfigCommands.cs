using Gg.Local;

namespace Gg.Client;

/// <summary>A configuration change gg would not make, and why.</summary>
/// <remarks>
/// <b>Named, for the reason the envelope verbs already have named refusals.</b>
/// The command line catches it and prints the sentence; an
/// <c>ArgumentOutOfRangeException</c> reaching the top prints a stack trace and
/// the parameter's name, which is a crash wearing a diagnosis. Found by running
/// the verb rather than by a test, because the test asserted the throw and the
/// harness above it was what turned one into an answer.
/// </remarks>
public sealed class ConfigurationRefused(string why) : Exception(why);

/// <summary>What is in force on this machine, and where the file is.</summary>
/// <remarks>
/// <b>The path is part of the answer.</b> A person asking what is configured is
/// usually one step from asking where to change it, and computing the path
/// themselves means knowing the <c>XDG_CONFIG_HOME</c> rule.
/// </remarks>
public sealed record ConfigurationView
{
    public required string Path { get; init; }

    public required IReadOnlyList<EnvironmentSetting> Settings { get; init; }

    /// <summary>Whether a control plane may change what this machine does.</summary>
    /// <remarks>
    /// <b>On the view rather than in the settings list, because it is not one of
    /// them.</b> It has no environment variable by design — a variable would be
    /// a second way to turn it on, and one a container image could carry — so it
    /// never reaches the page that lists variables, and without this it reached
    /// no surface at all.
    /// </remarks>
    public bool AcceptsOffered { get; init; }

}

/// <summary>Whether a document is one, and what it looks like written out.</summary>
/// <remarks>
/// <b>The same three parts <c>EnvelopeValidation</c> carries</b>, for the same
/// reasons: whether it is valid, what is wrong when it is not, and what gg
/// would write — so a person can see before applying what their file is about
/// to become.
/// </remarks>
public sealed record ConfigurationValidation
{
    public required bool Valid { get; init; }

    public string? Diagnosis { get; init; }

    public string? Canonical { get; init; }
}

/// <summary>
/// The config verbs: show, validate, init, set.
/// </summary>
/// <remarks>
/// <para>
/// <b>None of them contacts anything.</b> Configuration is a fact about this
/// machine, so every one of these works with no session and no network — which
/// is the property <c>gg envelope validate</c> already has and the reason it is
/// the verb people reach for offline.
/// </para>
/// <para>
/// <b>They return a <see cref="VerbResult"/> like every other verb</b>, so
/// <c>--json</c> and the rendered form are two views of one document rather
/// than two implementations that agree today.
/// </para>
/// </remarks>
public static class ConfigCommands
{
    /// <summary>Everything in force, and the file it would be written to.</summary>
    public static VerbResult Show(
        IReadOnlyList<EnvironmentSetting> settings,
        string? path = null,
        Configuration? file = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new VerbResult.ConfigShown(new ConfigurationView
        {
            Path = path ?? ConfigurationFile.DefaultPath(),
            Settings = settings,
            AcceptsOffered = file?.AcceptOffered is true,
        });
    }

    /// <summary>Whether this text is a configuration. Contacts nothing.</summary>
    public static VerbResult Validate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var parsed = ConfigurationFile.Parse(text);

        return new VerbResult.ConfigValidated(new ConfigurationValidation
        {
            Valid = parsed.Configuration is not null,
            Diagnosis = parsed.Diagnosis,
            Canonical = parsed.Configuration is { } configuration
                ? ConfigurationFile.Render(configuration)
                : null,
        });
    }

    /// <summary>
    /// Writes a file seeded from what is in force, when there is not one.
    /// </summary>
    /// <remarks>
    /// <b>It refuses to overwrite.</b> A person running this twice is a person
    /// who forgot they had run it, and the second run replacing their edits
    /// would be the worst possible answer to that.
    /// </remarks>
    public static VerbResult Init(Configuration seed, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(seed);

        var at = path ?? ConfigurationFile.DefaultPath();

        if (File.Exists(at))
        {
            throw new ConfigurationRefused(
                $"'{at}' is already there. gg will not overwrite it - read it with `gg config "
              + "show`, or change one value with `gg config set`.");
        }

        if (Configuration.Validate(seed) is { } refused)
        {
            throw new ConfigurationRefused(refused);
        }

        ConfigurationFile.Write(seed, at);

        return new VerbResult.ConfigValidated(new ConfigurationValidation
        {
            Valid = true,
            Canonical = ConfigurationFile.Render(seed),
        });
    }

    /// <summary>
    /// Changes one setting, leaving the rest of the document alone.
    /// </summary>
    /// <remarks>
    /// <b>Validated before anything is written</b>, so the file on disk is
    /// always readable by the next run. A set that wrote a value
    /// <see cref="Configuration.Validate"/> refuses would leave a document gg
    /// then declines to read — and the person who typed it would have no way to
    /// tell that from a file it had never written.
    /// </remarks>
    public static VerbResult Set(string? path, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var at = path ?? ConfigurationFile.DefaultPath();
        var read = ConfigurationFile.Read(at);

        if (read.Diagnosis is { } unreadable)
        {
            throw new ConfigurationRefused(
                $"'{at}' cannot be read, so changing one value in it would lose the rest: "
              + unreadable);
        }

        // BY KEY, WHICH IS WHAT A PERSON READING THE FILE SEES. The variable is
        // the other spelling and is accepted too, because the page and the
        // doctor both name it and somebody will paste one.
        var member = Configuration.Members.FirstOrDefault(
            m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase)
              || string.Equals(m.Variable, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new ConfigurationRefused(
                $"'{key}' is not a setting. These are: "
              + string.Join(", ", Configuration.Members.Select(m => m.Key)) + ".");

        var changed = member.With(read.Configuration ?? new Configuration(), value);

        if (Configuration.Validate(changed) is { } refused)
        {
            throw new ConfigurationRefused(refused);
        }

        ConfigurationFile.Write(changed, at);

        return new VerbResult.ConfigValidated(new ConfigurationValidation
        {
            Valid = true,
            Canonical = ConfigurationFile.Render(changed),
        });
    }
}
