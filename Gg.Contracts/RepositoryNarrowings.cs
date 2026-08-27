namespace Gg.Contracts;

/// <summary>
/// The directory in a service repository under which every document is a
/// narrowing (ADR-0018 § 7).
/// </summary>
/// <remarks>
/// <para>
/// <b>A directory, and the reason is <c>CODEOWNERS</c>.</b> It grants per path,
/// so one file holding two concerns hands one owner both of them — and the whole
/// feature leans on that grant being the enforcement mechanism. One concern per
/// file is what makes it mean anything, and a declaration naming a file is that
/// decision undone by a missing character.
/// </para>
/// <para>
/// <b>This is also the containment, because the permission cannot express
/// one.</b> A forge has no path-scoped read: an installation token that can read
/// one file can read every file. So the bound on what the control plane will
/// ever ask for is this declaration and the check below, and both live where a
/// person authoring the declaration can see them.
/// </para>
/// <para>
/// <b>Null is off; blank is a refusal.</b> Not the same answer, and the
/// difference is load-bearing: null is a decision not to declare, which is every
/// tenant today. Empty would be a declaration of nothing — narrowings at the
/// root of the tree, every file in the repository read as policy — so a client
/// sending <c>""</c> for an absent field would turn the tap on. ADR-0018 § 6's
/// rule at authoring time: absent and empty are different, and neither is
/// silent.
/// </para>
/// <para>
/// <b>Deliberately not <see cref="AirspaceNames"/>.</b> That rule governs a
/// document NAME, rendered as one path component in a working copy. This is a
/// path with separators, inside somebody else's repository, and sharing the
/// computation would refuse <c>.goodgrief/narrowings/</c> on its first
/// character. One computation per kind of name, which is slice thirteen's
/// finding and this file's reason for existing.
/// </para>
/// </remarks>
public static class RepositoryNarrowings
{
    /// <summary>Where the template puts it, and what the docs teach.</summary>
    /// <remarks>
    /// A default rather than a rule: a tenant may declare anywhere inside the
    /// repository, because <c>.goodgrief/</c> also holds the vendored
    /// constitution and one <c>CODEOWNERS</c> entry over the pair is the
    /// collapse § 7 refuses one level down.
    /// </remarks>
    public const string Conventional = ".goodgrief/narrowings/";

    /// <summary>Null when the declaration is legal — or absent — and one diagnosis otherwise.</summary>
    public static string? Invalid(string? declared)
    {
        if (declared is null)
        {
            // OFF. Not a refusal, and the most common answer there is.
            return null;
        }

        if (string.IsNullOrWhiteSpace(declared))
        {
            return "A narrowings directory is a path inside the repository, and this one is "
                 + "blank. Leave the declaration out entirely to keep the layer off - an empty "
                 + "path is not 'off', it is the root of the tree, which would read every file "
                 + "in the repository as policy.";
        }

        foreach (var c in declared)
        {
            if (char.IsControl(c))
            {
                return $"A narrowings directory cannot contain {Describe(c)}. A path that "
                     + "carries one is a path no forge will serve and nobody meant to write.";
            }
        }

        if (declared.Contains('\\', StringComparison.Ordinal))
        {
            return @"A narrowings directory is written with '/' and not '\'. It is normalised "
                 + "nowhere: a forge serves forward slashes and has no opinion about Windows, "
                 + "so accepting a backslash here would mean one thing when it is declared and "
                 + "another when it is fetched.";
        }

        if (declared.StartsWith('/'))
        {
            return "A narrowings directory names a place INSIDE the repository, and "
                 + $"'{declared}' is absolute - which names a place on whatever machine reads "
                 + "it. Declare it relative to the repository root.";
        }

        if (declared.Split('/').Contains("..", StringComparer.Ordinal))
        {
            return $"A narrowings directory cannot contain '..', and '{declared}' does. This "
                 + "declaration is the only bound on what the control plane will ever ask your "
                 + "forge to read - a forge has no path-scoped permission, so a path that can "
                 + "climb out of the directory is a path with no containment at all.";
        }

        if (!declared.EndsWith('/'))
        {
            return $"A narrowings directory ends with '/', and '{declared}' does not. This is a "
                 + "directory and not a file, deliberately: CODEOWNERS grants per path, so two "
                 + "concerns in one file share one owner - and one owner over two concerns is "
                 + "what makes CODEOWNERS decoration rather than the control this layer rests "
                 + "on.";
        }

        return null;
    }

    /// <summary>Whether a fetched path is inside a declared directory.</summary>
    /// <remarks>
    /// <b>The other half, and it is a separate question.</b> The rule above says
    /// what may be DECLARED; this says whether a path the control plane is about
    /// to ask for is under it. Both are needed because the declaration is
    /// checked once at a gate and the containment is checked on every read.
    /// </remarks>
    public static bool Contains(string? declared, string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Invalid(declared) is null
            && declared is { Length: > 0 }
            && path.StartsWith(declared, StringComparison.Ordinal)
            && path.Length > declared.Length
            && !path.Split('/').Contains("..", StringComparer.Ordinal);
    }

    /// <summary>A character named rather than printed, so a refusal reads.</summary>
    private static string Describe(char c) => c switch
    {
        '\n' => "a line break",
        '\r' => "a carriage return",
        '\t' => "a tab",
        _ => $"the control character U+{(int)c:X4}",
    };
}
