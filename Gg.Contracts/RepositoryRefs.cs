namespace Gg.Contracts;

/// <summary>
/// What a declared ref has to look like to mean one thing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fully qualified, because a bare name is a question.</b>
/// <c>refs/heads/main</c> and <c>refs/tags/main</c> are two different commits,
/// and a repository may carry both. Accepting <c>main</c> would mean choosing a
/// namespace on the registrar's behalf and being silently wrong on the
/// repositories where it matters most.
/// </para>
/// <para>
/// <b>Shape only, and deliberately not existence.</b> Whether a ref resolves is
/// the forge's answer and a runner's question; this repository is public, names
/// no forge, and reaches none. What can be decided here is whether somebody
/// wrote something that could only ever mean one thing — so that is all this
/// decides, and it says so rather than implying the ref was checked.
/// </para>
/// <para>
/// <b>Every namespace a flight already uses.</b> Branches, tags and pull refs
/// alike: a flight pins <c>refs/pull/n/head</c> on the BASE repository, which is
/// what makes a fork need no credential of its own, so a validator that knew
/// only about branches would break forks.
/// </para>
/// </remarks>
public static class RepositoryRefs
{
    /// <summary>The prefix every real ref carries.</summary>
    public const string Prefix = "refs/";

    /// <summary>
    /// A diagnosis if this cannot be a ref, or null.
    /// </summary>
    /// <remarks>
    /// <b>Null in and null out.</b> Declaring no ref is silence, not a
    /// malformed value: it is what every repository registered before this
    /// member existed says, and diagnosing it would refuse all of them at once.
    /// </remarks>
    public static string? Validate(string? declared)
    {
        if (declared is null)
        {
            return null;
        }

        if (declared.Length == 0 || declared.Trim().Length != declared.Length)
        {
            return "A ref is written exactly as the forge spells it, so leading or trailing "
                 + "whitespace is a typo rather than a name. Write it as 'refs/heads/main'.";
        }

        if (!declared.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return $"'{declared}' is not a ref, it is a name that could be several. A branch and "
                 + $"a tag called '{declared}' are two different commits, and resolving which one "
                 + "was meant is not something this can do on your behalf. Write the whole ref: "
                 + $"'refs/heads/{declared}' for a branch, 'refs/tags/{declared}' for a tag.";
        }

        if (declared.Length == Prefix.Length)
        {
            return "'refs/' names no ref by itself. Write the whole one, e.g. 'refs/heads/main'.";
        }

        return null;
    }
}
