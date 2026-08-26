namespace Gg.Contracts;

/// <summary>
/// What a named Airspace document may be called, and where it sits in a tree.
/// </summary>
/// <remarks>
/// <para>
/// <b>One computation, because a name is now also a path.</b> ADR-0016 renders the
/// estate as files, so a name has to survive being a path component and has to be
/// recoverable from one. Both repositories consume this method rather than carrying
/// a rule each: a second implementation is the manifest hazard wearing a different
/// coat - two computations that agree today and drift the first time one of them is
/// fixed.
/// </para>
/// <para>
/// <b>The vocabulary narrows here, deliberately.</b> ADR-0014 opened layer names
/// because architects create them, so they cannot be an enum; that stays true. What
/// changes is the alphabet: an architect may coin any name they like out of the
/// characters a file path can hold. The measurement that justified it is on the
/// record - twelve candidate names declared against a live control plane, eleven
/// accepted, <c>../../etc/passwd</c> among them.
/// </para>
/// <para>
/// <b>Case is refused at the name because the filesystem will not refuse it.</b>
/// <c>Payments</c> and <c>payments</c> are two streams, two version counters and two
/// topology rows, and one file on macOS and Windows. Every comparison in the system
/// is ordinal and a stream id is a hash over the name's bytes, so nothing downstream
/// can recover from the collision - which makes the name the only place to stop it.
/// </para>
/// <para>
/// <b>What this does not decide is which names are reserved.</b> <c>root</c> is
/// valid as a shape and undeclarable as a rule; so is <c>flight</c>, which is the
/// stored layer tag, and so is every shipped work kind. Reservedness belongs where
/// declaring happens, because a reader spelling <c>root@v7</c> still has to be able
/// to spell it.
/// </para>
/// </remarks>
public static class AirspaceNames
{
    /// <summary>
    /// The longest a name may be. A path component, not a sentence.
    /// </summary>
    public const int MaxLength = 64;

    /// <summary>
    /// Which directory renders which role. The one table, read both ways, so the
    /// mapping cannot disagree with its own inverse.
    /// </summary>
    private static readonly (string Role, string Directory)[] Tree =
    [
        (Roles.WorkKind, "work-kinds"),
        (Roles.Narrowing, "narrowings"),
        (Roles.Strategy, "strategies"),
    ];

    /// <summary>The file a tenant's floor renders to. Never inside a directory.</summary>
    private const string RootFile = "root.yaml";

    private const string Extension = ".yaml";

    /// <summary>Why this cannot be a name, or null when it can.</summary>
    public static string? Invalid(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (string.IsNullOrWhiteSpace(name))
        {
            return "An envelope name is blank. A blank cannot be declared, selected or "
                 + "pinned, so admitting it would record nothing anybody can reach.";
        }

        if (name.Contains('@', StringComparison.Ordinal))
        {
            return $"'{name}' contains '@', which is the separator in a qualified version "
                 + "like payments@v4 - a name carrying one makes every pin that names it "
                 + "unparseable.";
        }

        if (name.Contains('\n', StringComparison.Ordinal)
         || name.Contains('\r', StringComparison.Ordinal))
        {
            return "An envelope name spans more than one line, which is a paste accident "
                 + "rather than a name.";
        }

        if (name.Length > MaxLength)
        {
            return $"An envelope name is {name.Length} characters and the limit is "
                 + $"{MaxLength}. The name is a path component in the working copy, not "
                 + "a description of what the document does.";
        }

        foreach (var c in name)
        {
            if (char.IsAsciiLetterUpper(c))
            {
                return $"'{name}' contains {Describe(c)}, and a name is lower case because "
                     + "the filesystem it renders to is not case sensitive - 'Payments' "
                     + "and 'payments' would be two documents and one file, and nothing "
                     + "downstream could tell which one it read.";
            }

            if (!char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c) && c != '-')
            {
                return $"'{name}' contains {Describe(c)}, which a file path cannot hold. "
                     + "A name is rendered as a path component in the working copy, so it "
                     + "is lower-case letters, digits and dashes.";
            }
        }

        if (!char.IsAsciiLetterLower(name[0]) && !char.IsAsciiDigit(name[0]))
        {
            return $"'{name}' begins with {Describe(name[0])}. A name begins with a letter "
                 + "or a digit, because a leading dash reads as an option and a leading dot "
                 + "hides the file.";
        }

        var last = name[^1];
        if (!char.IsAsciiLetterLower(last) && !char.IsAsciiDigit(last))
        {
            return $"'{name}' ends with {Describe(last)}. A name ends with a letter or a "
                 + "digit, so that two names cannot differ only by punctuation nobody sees.";
        }

        return null;
    }

    /// <summary>Where a document of this role and name renders in the tree.</summary>
    /// <remarks>
    /// The separator is the tree's own rather than the running machine's: a working
    /// copy is a git repository, git holds forward slashes everywhere, and a path
    /// built with the platform separator would produce a second file for the same
    /// document the moment somebody on another platform pulled it.
    /// </remarks>
    public static string PathFor(string role, string name)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(name);

        if (string.Equals(role, Roles.Root, StringComparison.Ordinal))
        {
            return RootFile;
        }

        foreach (var (known, directory) in Tree)
        {
            if (string.Equals(role, known, StringComparison.Ordinal))
            {
                return $"{directory}/{name}{Extension}";
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(role), role,
            "This role has no place in the rendered tree, so pull cannot write it. A role "
          + "that reaches here is one somebody added to the vocabulary without deciding "
          + "where its documents live.");
    }

    /// <summary>
    /// The document a path names, or null when the path is not one.
    /// </summary>
    /// <remarks>
    /// A working copy is a directory somebody also keeps notes in. Anything pull did
    /// not write is not a document, and apply must not invent a stream for it - so
    /// this answers null rather than guessing, including for a file whose stem is a
    /// name this estate would refuse.
    /// </remarks>
    public static (string Role, string Name)? NameFrom(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (string.Equals(path, RootFile, StringComparison.Ordinal))
        {
            return (Roles.Root, Roles.Root);
        }

        var parts = path.Split('/');
        if (parts.Length != 2 || !parts[1].EndsWith(Extension, StringComparison.Ordinal))
        {
            return null;
        }

        var stem = parts[1][..^Extension.Length];
        if (Invalid(stem) is not null)
        {
            return null;
        }

        foreach (var (role, directory) in Tree)
        {
            if (string.Equals(parts[0], directory, StringComparison.Ordinal))
            {
                return (role, stem);
            }
        }

        return null;
    }

    /// <summary>
    /// A character as a refusal can print it. Whitespace and control characters
    /// name themselves rather than vanishing into the sentence around them.
    /// </summary>
    private static string Describe(char c) => c switch
    {
        '\t' => "'\\t'",
        '\n' => "'\\n'",
        '\r' => "'\\r'",
        _ when char.IsControl(c) => $"'\\u{(int)c:x4}'",
        _ => $"'{c}'",
    };
}
