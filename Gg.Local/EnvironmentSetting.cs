namespace Gg.Local;

/// <summary>
/// One environment variable this program reads, and what it decides.
/// </summary>
/// <remarks>
/// <para>
/// <b>DECLARED, NEVER SWEPT.</b> A list built by walking the process
/// environment would put whatever else a person exports — cloud keys, tokens,
/// a colleague's credentials — onto a screen they may be sharing, and into the
/// state dump. Every entry here is a variable the code actually reads, named by
/// the place that reads it.
/// </para>
/// <para>
/// <b>Here because both halves need it.</b> The composition root builds these
/// — it is already the one place that reads the environment — and the console
/// renders them. That is <c>Gg.Local</c>'s charter exactly: local
/// configuration, readable by a runner and a console alike.
/// </para>
/// <para>
/// <b>The reason is not documentation.</b> A name and a value with no
/// consequence attached is a line a person has to go and look up, which is
/// what they were doing before they opened this.
/// </para>
/// </remarks>
public sealed record EnvironmentSetting
{
    /// <summary>The variable, as it is spelled in a shell.</summary>
    public required string Name { get; init; }

    /// <summary>What it is set to, or null when it is not set.</summary>
    /// <remarks>
    /// <b>Null is shown, not hidden.</b> The variable worth looking at is
    /// usually the one that is not set — which a list of only what IS set
    /// cannot tell anybody.
    /// </remarks>
    public string? Value { get; init; }

    /// <summary>What this decides, in one line, from the person's side.</summary>
    public required string Why { get; init; }

    /// <summary>Which source answered. One of <see cref="SettingSources"/>.</summary>
    /// <remarks>
    /// <b>Shown, because precedence a person cannot see is a trap.</b> Three
    /// places can answer and the reader has no way to tell which did — so an
    /// edit to the file that changed nothing reads as a broken file rather than
    /// as a variable winning.
    /// </remarks>
    public required string Source { get; init; }

    /// <summary>
    /// The file's value, when an environment variable is overriding it.
    /// </summary>
    /// <remarks>
    /// <b>The line that answers "why did my edit do nothing".</b> Null unless
    /// the file really is being overridden, so nothing is said where there is
    /// nothing to say.
    /// </remarks>
    public string? Shadowed { get; init; }

    /// <summary>Whether this one cannot be put in the file at all.</summary>
    /// <remarks>
    /// <b>Said rather than left out.</b> The three path roots decide where the
    /// file itself lives, so a value inside it could not be read before it was
    /// needed; <c>GG_STATE_DUMP</c> is a thing a person turns on for one run.
    /// Omitting them would leave somebody hunting the file for a key that
    /// cannot exist.
    /// </remarks>
    public bool EnvironmentOnly { get; init; }
}
