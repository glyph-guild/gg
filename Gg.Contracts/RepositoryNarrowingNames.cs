namespace Gg.Contracts;

/// <summary>
/// <c>&lt;repository&gt;/&lt;filename&gt;</c> — what a narrowing living in a
/// service repository is called (ADR-0018 § 7).
/// </summary>
/// <remarks>
/// <para>
/// <b>Namespaced, and the namespace is the whole point.</b>
/// <c>.goodgrief/narrowings/pci.yaml</c> in <c>payments</c> is
/// <c>payments/pci</c>, never the tenant's <c>pci</c> — because without it a
/// team shadows a compliance regime by choosing a filename, which is a widening
/// performed by a merge.
/// </para>
/// <para>
/// <b>A third kind of name, so a third computation.</b> Slice thirteen's rule:
/// one computation per KIND of name, rather than one for everything spelled
/// <i>name</i>. <see cref="AirspaceNames"/> governs one path component in a
/// working copy and would refuse every name here on its first slash;
/// <c>RepositoryRegistry.Invalid</c> governs a forge slug and would accept
/// things no name can hold.
/// </para>
/// <para>
/// <b>The halves are held to different rules, deliberately.</b> The left is a
/// registry key — a slug, which may carry slashes — and the right is a file
/// stem a person chose, which is held to the estate rule so that
/// <c>payments/PCI</c> and <c>payments/pci</c> cannot be two names for one
/// regime on a case-insensitive filesystem.
/// </para>
/// </remarks>
public static class RepositoryNarrowingNames
{
    /// <summary>The separator, and it is the LAST one.</summary>
    /// <remarks>
    /// A registry key may contain a slash and a document name may not, so
    /// splitting on the last one is what makes the mapping injective over keys
    /// that are slugs. Splitting on the first would make <c>acme/widgets/pci</c>
    /// parse as repository <c>acme</c>, document <c>widgets/pci</c> — a document
    /// name that cannot exist, silently.
    /// </remarks>
    private const char Separator = '/';

    /// <summary>Null when this key can be a name's left half, or one diagnosis.</summary>
    public static string? Invalid(string? repositoryKey)
    {
        if (string.IsNullOrWhiteSpace(repositoryKey))
        {
            return "A repository key is what the left half of a narrowing's name is made "
                 + "from, and this one is blank.";
        }

        if (repositoryKey.Contains('@', StringComparison.Ordinal))
        {
            // THE ONE THE REGISTRY LETS THROUGH. It refuses only blank and
            // newlines, deliberately, because a slug is not an estate name. But
            // a version is qualified `name@vN`, so a key carrying an `@`
            // composes to `payments@v1/pci`, and `payments@v1/pci@v3` is a name
            // nothing can parse a version out of.
            return $"'{repositoryKey}' contains '@', which separates a name from its version. "
                 + "A repository whose key carries one cannot have its narrowings named, "
                 + "because the name and the version would be indistinguishable. Re-register "
                 + "it under a key without one.";
        }

        foreach (var c in repositoryKey)
        {
            if (char.IsControl(c))
            {
                return $"'{repositoryKey}' contains a control character, which a name cannot "
                     + "hold.";
            }
        }

        if (repositoryKey.StartsWith(Separator) || repositoryKey.EndsWith(Separator))
        {
            return $"'{repositoryKey}' begins or ends with '{Separator}', so the name it "
                 + "composes to would have an empty half.";
        }

        return null;
    }

    /// <summary>
    /// The name a document in a repository is called, or null when there is not
    /// one.
    /// </summary>
    /// <param name="repositoryKey">The registry key, never the forge path.</param>
    /// <param name="document">The file's stem, held to the estate name rule.</param>
    public static string? Compose(string? repositoryKey, string? document) =>
        Invalid(repositoryKey) is null
        && document is not null
        && AirspaceNames.Invalid(document) is null
            ? repositoryKey + Separator + document
            : null;

    /// <summary>What a composed name was made from.</summary>
    /// <remarks>
    /// The inverse of <see cref="Compose"/>, and it has to be exact: a flight's
    /// <c>envelope-version</c> carries these, so a name that does not parse back
    /// is a flight nobody can say what governed.
    /// </remarks>
    public static bool TryParse(string? composed, out string repository, out string document)
    {
        repository = string.Empty;
        document = string.Empty;

        if (composed is null)
        {
            return false;
        }

        var at = composed.LastIndexOf(Separator);
        if (at <= 0 || at == composed.Length - 1)
        {
            return false;
        }

        var left = composed[..at];
        var right = composed[(at + 1)..];

        if (Invalid(left) is not null || AirspaceNames.Invalid(right) is not null)
        {
            return false;
        }

        repository = left;
        document = right;
        return true;
    }
}
