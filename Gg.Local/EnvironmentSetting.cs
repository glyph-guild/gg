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
}
