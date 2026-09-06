using System.Text;
using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>One optional input, and whether anything ever supplies it.</summary>
internal sealed record Finding
{
    public required string Type { get; init; }

    public required string Method { get; init; }

    public required string Parameter { get; init; }

    public required string File { get; init; }
}

/// <summary>Findings, and the declarations the scan could not decide about.</summary>
internal sealed record InputAnalysis
{
    public required IReadOnlyList<Finding> Findings { get; init; }

    /// <summary>
    /// Declarations whose method name is declared by more than one type.
    /// </summary>
    /// <remarks>
    /// <b>Reported rather than dropped.</b> Calls are matched by name, so one
    /// type's caller counts as evidence about another's parameter and a real
    /// finding disappears silently. A guard that quietly loses a finding is the
    /// family it catches — the same sentence <c>UnreadMembers</c> writes one
    /// scan over.
    /// </remarks>
    public required IReadOnlyList<string> Undecidable { get; init; }
}

/// <summary>
/// Optional parameters no production caller ever supplies.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this catches, and why a caller count would not.</b> A parameter with
/// a default has a production caller — every one of them — and still reaches
/// production as its default for ever. <i>Does anything call this</i> answers
/// yes and means nothing. The console's own browse pane is the instance that
/// prompted this: <c>ConsoleLoop</c> declared <c>IWorkBrowser? browser = null</c>,
/// nothing in <c>Gg.Cli</c> passed one, and the pane answered "No tracker is
/// configured to browse" on a machine whose tracker was configured correctly.
/// </para>
/// <para>
/// <b>Textual, like every other scan in these repositories.</b> The only
/// <c>Microsoft.CodeAnalysis</c> reference here is an analyzer with
/// <c>PrivateAssets="all"</c>, so no syntax tree is available.
/// </para>
/// <para>
/// <b>Its own blanker, and the duplication is stated rather than hidden.</b>
/// <c>Gg.Runner.Tests.UnreadMembers</c> has a better one — it keeps
/// interpolation holes, because a hole is code and its question turns on that.
/// This question does not: an argument list inside a string literal is prose
/// either way. Test projects here reference no other test project, so the
/// alternative to a second blanker is a shared test library nobody has asked
/// for.
/// </para>
/// </remarks>
internal static class UnsuppliedInputs
{
    /// <summary>
    /// Parameters the compiler or the language supplies, which no caller writes.
    /// </summary>
    private static readonly string[] CompilerSupplied =
    [
        "callerMemberName", "callerFilePath", "callerLineNumber",
    ];

    /// <summary>
    /// Names whose default is the point of the parameter, not a gap.
    /// </summary>
    /// <remarks>
    /// A cancellation token threaded through everything is supplied by callers
    /// that have one and omitted by callers that do not, and neither is a
    /// finding. Listing them here rather than exempting them one at a time is
    /// what keeps the exemption list about real decisions.
    /// </remarks>
    private static readonly string[] Idiomatic = ["cancellationToken", "ct", "token"];

    /// <summary>A method or constructor declaration with a parameter list.</summary>
    /// <remarks>
    /// <b>Atomic, and bounded.</b> The modifier alternation under a <c>*</c> and
    /// a lazy parameter list is the classic backtracking shape: the first
    /// version spent most of a minute on this repository. <c>(?&gt;…)</c> stops
    /// the engine reconsidering a modifier it has already taken, and a
    /// parameter list longer than this is not one.
    /// </remarks>
    private static readonly Regex Declaration = new(
        @"\b(?:public|internal|protected)\s+(?>(?:static|sealed|override|virtual|async|partial)\s+)*"
      + @"(?>[\w<>,\[\]\?\.]+\s+)?(?<name>\w+)\s*\((?<params>[^;{]{0,4000}?)\)\s*(?:=>|\{|$)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>A type declaration, so a finding can name what declared it.</summary>
    /// <remarks>
    /// <b>No <c>\s</c> inside the modifier alternation, and the group is
    /// atomic.</b> With whitespace as one branch of an alternation under a
    /// <c>*</c>, the engine can split a run of spaces in exponentially many
    /// ways before failing on a line that was never a type declaration - and
    /// this scan spent fifty seconds there, on two megabytes of source that
    /// should take under a second.
    /// </remarks>
    private static readonly Regex TypeName = new(
        @"(?m)^[ \t]*(?:\[[^\]]*\][ \t]*)*"
      + @"(?>(?:public|internal|private|protected|sealed|static|abstract|partial|readonly|file|ref)[ \t]+)*"
      + @"(?:record[ \t]+)?(?:class|record|struct|interface)[ \t]+(?<name>\w+)",
        RegexOptions.Compiled);

    /// <summary>
    /// Comments and string bodies blanked, so prose cannot report a supply.
    /// </summary>
    internal static string Noise(string source)
    {
        var kept = new StringBuilder(source.Length);
        var i = 0;

        while (i < source.Length)
        {
            if (source.AsSpan(i).StartsWith("//"))
            {
                var end = source.IndexOf('\n', i);
                end = end < 0 ? source.Length : end;
                kept.Append(' ', end - i);
                i = end;
                continue;
            }

            if (source.AsSpan(i).StartsWith("/*"))
            {
                var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
                kept.Append(' ', end - i);
                i = end;
                continue;
            }

            if (source[i] == '"'
                || (source[i] is '$' or '@' && i + 1 < source.Length && source[i + 1] is '"' or '$' or '@'))
            {
                var end = EndOfLiteral(source, i);
                kept.Append(' ', end - i);
                i = end;
                continue;
            }

            kept.Append(source[i]);
            i++;
        }

        return kept.ToString();
    }

    /// <summary>Past the end of a literal beginning at <paramref name="start"/>.</summary>
    private static int EndOfLiteral(string source, int start)
    {
        var i = start;
        var verbatim = false;

        while (i < source.Length && source[i] is '$' or '@')
        {
            verbatim |= source[i] == '@';
            i++;
        }

        if (i >= source.Length || source[i] != '"')
        {
            return start + 1;
        }

        // A RAW LITERAL ENDS ON ITS OWN FENCE. Three or more quotes open one and
        // the same count closes it, and escapes do not apply inside - so the
        // ordinary scan below would run to the end of the file.
        var fence = 0;
        while (i + fence < source.Length && source[i + fence] == '"')
        {
            fence++;
        }

        if (fence >= 3)
        {
            var close = source.IndexOf(new string('"', fence), i + fence, StringComparison.Ordinal);
            return close < 0 ? source.Length : close + fence;
        }

        i++;
        while (i < source.Length)
        {
            if (verbatim)
            {
                if (source[i] == '"')
                {
                    if (i + 1 < source.Length && source[i + 1] == '"')
                    {
                        i += 2;
                        continue;
                    }

                    return i + 1;
                }
            }
            else
            {
                if (source[i] == '\\')
                {
                    i += 2;
                    continue;
                }

                if (source[i] == '\n')
                {
                    return i;
                }

                if (source[i] == '"')
                {
                    return i + 1;
                }
            }

            i++;
        }

        return source.Length;
    }

    /// <summary>Where each type declaration begins, in order.</summary>
    /// <remarks>
    /// COMPUTED ONCE PER FILE. Asking "which type is this position in" by
    /// re-matching every type declaration in the file, for every method
    /// declaration in the file, is quadratic in the file - and over this
    /// repository it was the whole of a three-and-a-half minute scan.
    /// </remarks>
    private static IReadOnlyList<(int Index, string Name)> Types(string text) =>
        [.. TypeName.Matches(text).Select(m => (m.Index, m.Groups["name"].Value))];

    /// <summary>Which type a position in a file sits inside.</summary>
    private static string TypeAt(IReadOnlyList<(int Index, string Name)> types, int index)
    {
        var last = "";
        foreach (var (at, name) in types)
        {
            if (at > index)
            {
                break;
            }

            last = name;
        }

        return last;
    }

    /// <summary>The optional parameters one declaration carries, in order.</summary>
    private static IReadOnlyList<(string Name, int Position)> Optional(string parameters)
    {
        var found = new List<(string, int)>();
        var depth = 0;
        var start = 0;
        var position = 0;
        var pieces = new List<string>();

        for (var i = 0; i <= parameters.Length; i++)
        {
            if (i == parameters.Length || (parameters[i] == ',' && depth == 0))
            {
                pieces.Add(parameters[start..Math.Min(i, parameters.Length)]);
                start = i + 1;
                continue;
            }

            depth += parameters[i] switch { '(' or '<' or '[' => 1, ')' or '>' or ']' => -1, _ => 0 };
        }

        foreach (var piece in pieces)
        {
            var equals = piece.IndexOf('=');
            if (equals > 0 && piece[equals - 1] is not ('=' or '!' or '<' or '>'))
            {
                var before = piece[..equals].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (before.Length >= 2)
                {
                    found.Add((before[^1], position));
                }
            }

            position++;
        }

        return found;
    }

    /// <summary>What every call supplies, for each method name asked about.</summary>
    /// <remarks>
    /// <para>
    /// <b>A DECLARATION IS NOT A CALL, and telling them apart by the LINE was
    /// wrong.</b> The first version skipped a match whose line carried an access
    /// modifier - which discards a real call any time one shares a line with a
    /// declaration, and the poison twin below is a single line containing both.
    /// The declaration's own name positions are known exactly, having just been
    /// matched, so they are passed in and compared rather than guessed at.
    /// </para>
    /// <para>
    /// <b>ONE PASS FOR ALL OF THEM.</b> Asking per method meant re-scanning every
    /// file once per declaration and took nearly four minutes over this
    /// repository - slow enough to be the reason somebody stops running the
    /// suite, which is how a guard dies.
    /// </para>
    /// </remarks>
    private static Dictionary<string, (int MostPositional, HashSet<string> Named)> CallsFor(
        IReadOnlyDictionary<string, string> blanked,
        IReadOnlyDictionary<string, HashSet<int>> declarations,
        IReadOnlySet<string> names)
    {
        var supplied = names.ToDictionary(
            n => n,
            _ => (MostPositional: 0, Named: new HashSet<string>(StringComparer.Ordinal)),
            StringComparer.Ordinal);

        // NOT `(?<![\w.])`, WHICH WAS THE FIRST VERSION AND EXCLUDED ALMOST EVERY
        // CALL. A member access is how a method is nearly always reached -
        // `new A().M(…)`, `_store.M(…)` - so a lookbehind rejecting `.` counted
        // only bare calls and reported the rest of the repository as unsupplied.
        // What the lookbehind is for is a longer identifier ENDING in this name,
        // and `\w` alone does that.
        var anyCall = new Regex(@"(?<!\w)(?<name>\w+)\s*\(", RegexOptions.Compiled);
        var namedArgument = new Regex(@"(?<![\w.])(?<name>\w+)\s*:(?!:)", RegexOptions.Compiled);

        foreach (var (file, text) in blanked)
        {
            var declared = declarations.TryGetValue(file, out var set) ? set : [];

            foreach (Match m in anyCall.Matches(text))
            {
                var name = m.Groups["name"].Value;
                if (!supplied.TryGetValue(name, out var so_far) || declared.Contains(m.Groups["name"].Index))
                {
                    continue;
                }

                var open = m.Index + m.Length - 1;
                var close = MatchingParen(text, open);
                if (close < 0)
                {
                    continue;
                }

                var arguments = text[(open + 1)..close];
                foreach (Match n in namedArgument.Matches(arguments))
                {
                    so_far.Named.Add(n.Groups["name"].Value);
                }

                supplied[name] = (Math.Max(so_far.MostPositional, Arity(arguments)), so_far.Named);
            }
        }

        return supplied;
    }

    /// <summary>How many arguments a list carries, commas at depth zero.</summary>
    private static int Arity(string arguments)
    {
        if (arguments.Trim().Length == 0)
        {
            return 0;
        }

        var depth = 0;
        var count = 1;
        foreach (var c in arguments)
        {
            depth += c switch { '(' or '<' or '[' or '{' => 1, ')' or '>' or ']' or '}' => -1, _ => 0 };
            if (c == ',' && depth == 0)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>The index of the paren closing the one at <paramref name="open"/>.</summary>
    private static int MatchingParen(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '(')
            {
                depth++;
            }
            else if (text[i] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>What the scan finds, and what it could not decide.</summary>
    /// <remarks>
    /// <b>Each file is blanked once and each method looked up once.</b> The
    /// first version re-blanked every file for every declaration it examined,
    /// which took seven minutes over this repository - slow enough that the
    /// guard would have been the reason somebody stopped running the suite.
    /// </remarks>
    internal static InputAnalysis Analyse(IReadOnlyDictionary<string, string> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var blanked = sources.ToDictionary(s => s.Key, s => Noise(s.Value), StringComparer.Ordinal);
        var declarations = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        var declaredBy = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var found = new List<(string File, string Type, string Method, IReadOnlyList<(string Name, int Position)> Optional)>();

        foreach (var (file, text) in blanked)
        {
            var indices = new HashSet<int>();
            var types = Types(text);
            foreach (Match m in Declaration.Matches(text))
            {
                var method = m.Groups["name"].Value;
                var type = TypeAt(types, m.Index);

                indices.Add(m.Groups["name"].Index);

                if (!declaredBy.TryGetValue(method, out var declarers))
                {
                    declaredBy[method] = declarers = new HashSet<string>(StringComparer.Ordinal);
                }

                declarers.Add(type);

                var optional = Optional(m.Groups["params"].Value);
                if (optional.Count > 0)
                {
                    found.Add((file, type, method, optional));
                }
            }

            declarations[file] = indices;
        }

        var findings = new List<Finding>();
        var undecidable = new List<string>();
        var supplied = CallsFor(
            blanked, declarations, found.Select(f => f.Method).ToHashSet(StringComparer.Ordinal));

        foreach (var (file, type, method, optional) in found)
        {
            var calls = supplied[method];

            foreach (var (name, position) in optional)
            {
                if (Idiomatic.Contains(name, StringComparer.Ordinal)
                    || CompilerSupplied.Contains(name, StringComparer.Ordinal))
                {
                    continue;
                }

                if (calls.Named.Contains(name) || calls.MostPositional > position)
                {
                    continue;
                }

                if (declaredBy.TryGetValue(method, out var types) && types.Count > 1)
                {
                    undecidable.Add($"{type}.{method}({name})");
                    continue;
                }

                findings.Add(new Finding
                {
                    Type = type,
                    Method = method,
                    Parameter = name,
                    File = file,
                });
            }
        }

        return new InputAnalysis { Findings = findings, Undecidable = undecidable };
    }

    internal static string Key(Finding finding) =>
        $"{finding.Type}.{finding.Method}({finding.Parameter})";

    /// <summary>Every production source file, keyed by path.</summary>
    internal static IReadOnlyDictionary<string, string> Production()
    {
        var root = RepoRoot();
        var projects = new[] { "Gg.Cli", "Gg.Console", "Gg.Runner", "Gg.Local", "Gg.Client", "Gg.Contracts" };

        return projects
            .Select(p => Path.Combine(root, p))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToDictionary(f => Path.GetRelativePath(root, f), File.ReadAllText, StringComparer.Ordinal);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("no Gg.sln above " + AppContext.BaseDirectory);
    }
}
