using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Gg.Runner.Tests;

/// <summary>One member the runner supplies and nothing reads.</summary>
internal sealed record Unread
{
    public required string Type { get; init; }

    public required string Member { get; init; }

    /// <summary>Where it is declared.</summary>
    public required string File { get; init; }

    /// <summary>
    /// Whether a test reads it, which is a different sentence from nothing at all.
    /// </summary>
    /// <remarks>
    /// <b>Reached by one test</b> and <b>reached by nothing</b> are two states,
    /// and collapsing them loses the more interesting one: a member only a test
    /// reads was written to be checked and never to be used, which is a
    /// different mistake from a member nobody ever wanted.
    /// </remarks>
    public required bool ReadByATest { get; init; }
}

/// <summary>Findings, and the members the scan cannot decide about.</summary>
internal sealed record UnreadAnalysis
{
    public required IReadOnlyList<Unread> Findings { get; init; }

    /// <summary>
    /// Members whose name is read on some other type.
    /// </summary>
    /// <remarks>
    /// <b>Reported rather than dropped</b>, because a guard that quietly loses a
    /// finding is the family it catches. This one really does lose one:
    /// <c>PoolCapabilities.Provider</c> has no reader anywhere, and
    /// <c>.Provider</c> is read on <c>LeaseRepoRef</c>, so it lands here.
    /// </remarks>
    public required IReadOnlyList<string> Undecidable { get; init; }
}

/// <summary>
/// Members of a supplied type that no production consumer reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>Textual, because there is no alternative.</b> The only
/// <c>Microsoft.CodeAnalysis</c> reference in either repository is an analyzer
/// with <c>PrivateAssets="all"</c>, so a syntax tree is not available. This
/// joins the source scans on both sides of the wire, and inherits their
/// discipline: every blind spot is asserted rather than discovered.
/// </para>
/// <para>
/// <b>It takes the source as a map</b> so the same scan can be pointed at a
/// commit through <c>git show</c>. That is the whole of how retro-detection
/// works.
/// </para>
/// <para>
/// <b>Scoped to the runner's own declarations, and that scoping is the
/// finding.</b> Run across <c>Gg.Contracts</c> as well it reports seventy-five,
/// because a wire contract's members are read by the OTHER repository, which
/// this scan cannot see. Seventy-five findings is a list nobody reads. The
/// contract's own unread members need a scan that can see both sides, and that
/// is not this one.
/// </para>
/// </remarks>
internal static class UnreadMembers
{
    /// <summary>A readable property. Fields and methods are not in scope.</summary>
    private static readonly Regex Member = new(
        @"public\s+(?:required\s+)?(?:[\w<>?,\[\]\. ]+?)\s+(?<name>\w+)\s*\{\s*get;",
        RegexOptions.Compiled);

    /// <summary>A type declaration, which is what a member belongs to.</summary>
    private static readonly Regex Type = new(
        @"(?:public|internal)\s+(?:sealed\s+|abstract\s+|partial\s+|static\s+)*"
      + @"(?:record|class|interface)\s+(?<name>\w+)",
        RegexOptions.Compiled);

    /// <summary>
    /// A member the serializer reads.
    /// </summary>
    /// <remarks>
    /// <c>NewProposal.Head</c> is the body this slice authenticated: nothing in
    /// C# reads it and the source-generated serializer does. A scan that could
    /// not tell would report the wire shape of every request as dead.
    /// </remarks>
    private static readonly Regex Serialized = new(@"\[JsonPropertyName\(", RegexOptions.Compiled);

    public static string Key(Unread finding) =>
        $"{finding?.Type}.{finding?.Member}";

    /// <summary>
    /// What this finding is, in the sentence that distinguishes it.
    /// </summary>
    /// <remarks>
    /// <b>Two states, and the difference is what to do about them.</b> A member
    /// only a test reads was written to be CHECKED and never to be used — the
    /// declaration is real and the consumer never arrived. A member nothing
    /// reads at all was written and forgotten. One sentence for both would be
    /// the collapse this slice's own refusal split exists to undo.
    /// </remarks>
    public static string Diagnose(Unread finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        return finding.ReadByATest
            ? $"{Key(finding)} is read by one test and by nothing in production - declaration "
            + "a test checks rather than a value anything asks for."
            : $"{Key(finding)} is read by nothing at all, test or production.";
    }

    /// <summary>
    /// Comments and plain string bodies blanked, interpolation holes kept.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The one detail this whole scan turns on.</b> A comment describing a
    /// read and a literal containing one are prose, and counting them reports a
    /// member as live because somebody wrote about it. But an interpolation hole
    /// is CODE: <c>$"Work the issue at {request.IntentUri}"</c> is the only
    /// channel by which a flight's intent reaches an agent.
    /// </para>
    /// <para>
    /// good-grief's sibling scan blanks whole <c>$"…"</c> literals, correctly
    /// for its own question. Reusing that regex here would erase that read and
    /// report the runner's most load-bearing input as dead, with confidence.
    /// </para>
    /// </remarks>
    private static string Noise(string source)
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

            if (source[i] == '$' && i + 1 < source.Length
                && (source[i + 1] == '"' || (source[i + 1] == '@' && i + 2 < source.Length && source[i + 2] == '"')))
            {
                i = Interpolated(source, i, kept);
                continue;
            }

            if (source[i] == '"' || (source[i] == '@' && i + 1 < source.Length && source[i + 1] == '"'))
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

    /// <summary>Blanks an interpolated literal's text and keeps its holes.</summary>
    private static int Interpolated(string source, int start, StringBuilder kept)
    {
        var verbatim = source[start + 1] == '@';
        var i = start + (verbatim ? 3 : 2);
        kept.Append(' ', i - start);

        var depth = 0;
        while (i < source.Length)
        {
            var c = source[i];

            if (c == '{' && (i + 1 >= source.Length || source[i + 1] != '{'))
            {
                depth++;
                kept.Append(' ');
                i++;
                continue;
            }

            if (c == '}' && depth > 0)
            {
                depth--;
                kept.Append(' ');
                i++;
                continue;
            }

            if (depth > 0)
            {
                // INSIDE THE HOLE, this is code and stays.
                kept.Append(c);
                i++;
                continue;
            }

            if (!verbatim && c == '\\')
            {
                kept.Append(' ', Math.Min(2, source.Length - i));
                i += 2;
                continue;
            }

            kept.Append(' ');
            i++;

            if (c == '"')
            {
                break;
            }
        }

        return i;
    }

    private static int EndOfLiteral(string source, int start)
    {
        var verbatim = source[start] == '@';
        var i = start + (verbatim ? 2 : 1);

        while (i < source.Length)
        {
            if (!verbatim && source[i] == '\\')
            {
                i += 2;
                continue;
            }

            if (source[i] == '"')
            {
                return i + 1;
            }

            i++;
        }

        return source.Length;
    }

    /// <summary>
    /// The attributes attached to the member at <paramref name="declaration"/>.
    /// </summary>
    /// <remarks>
    /// Everything since the previous member ended — a <c>;</c> or a <c>}</c> —
    /// which is where this member's own attributes begin and no earlier.
    /// </remarks>
    private static string AttributesOf(string body, int declaration)
    {
        var previous = body.LastIndexOfAny([';', '}'], Math.Max(0, declaration - 1));

        return previous < 0 ? body[..declaration] : body[previous..declaration];
    }

    /// <summary>Every readable member the given source declares.</summary>
    /// <remarks>
    /// Throws on a source that declares none — an empty answer and a clean
    /// answer look identical from outside, which is the failure this whole
    /// family is about.
    /// </remarks>
    public static IReadOnlyList<Unread> Members(IReadOnlyDictionary<string, string> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var declared = new List<Unread>();

        foreach (var (file, raw) in source)
        {
            var text = Noise(raw);

            foreach (var type in Type.Matches(text).Cast<Match>())
            {
                var next = Type.Match(text, type.Index + type.Length);
                var body = text[type.Index..(next.Success ? next.Index : text.Length)];
                var rawBody = raw.Length >= text.Length ? raw : raw;

                foreach (var member in Member.Matches(body).Cast<Match>())
                {
                    // Serialized members are read by the serializer, and the
                    // window is THIS member's attributes and no further.
                    //
                    // The first version took the preceding two hundred
                    // characters, which spans the member above - so one
                    // serialized member excluded its neighbour, the fixture
                    // declared nothing after exclusion, and the anti-vacuity
                    // throw fired. Caught by the fixture that carries a second,
                    // unserialized member for exactly this reason.
                    if (Serialized.IsMatch(AttributesOf(body, member.Index)))
                    {
                        continue;
                    }

                    declared.Add(new Unread
                    {
                        Type = type.Groups["name"].Value,
                        Member = member.Groups["name"].Value,
                        File = file,
                        ReadByATest = false,
                    });
                }
            }
        }

        return declared.Count > 0
            ? declared
            : throw new InvalidOperationException(
                "This source declares no readable members. A scan that answers 'nothing is "
              + "unread' over an empty declaration list is indistinguishable from one that "
              + "answers it over a clean tree, and that is the failure this scan exists for.");
    }

    /// <summary>Findings and undecidables, over the given source.</summary>
    public static UnreadAnalysis Analyse(IReadOnlyDictionary<string, string> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var declared = Members(source);
        var text = source.Values.Select(Noise).ToList();

        // Which types declare each name, so a name on two types is undecidable
        // rather than reported: this scan matches on `.Name` and cannot tell
        // whose it is.
        var owners = declared
            .GroupBy(d => d.Member, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(d => d.Type).Distinct(StringComparer.Ordinal).Count(),
                StringComparer.Ordinal);

        var findings = new List<Unread>();
        var undecidable = new List<string>();

        // Read lazily, and only when there is something to ask about: a planted
        // fixture has no test corpus behind it, and loading one for every call
        // would make the plants depend on this repository's own tests.
        var inTests = new Lazy<IReadOnlyList<string>>(() =>
            TestsAvailable() ? [.. TestSource().Values.Select(Noise)] : []);

        foreach (var member in declared)
        {
            var read = new Regex(@"\.\s*" + Regex.Escape(member.Member) + @"\b");

            if (text.Exists(read.IsMatch))
            {
                continue;
            }

            if (owners[member.Member] > 1)
            {
                undecidable.Add(Key(member));
                continue;
            }

            findings.Add(member with
            {
                ReadByATest = inTests.Value.Any(read.IsMatch),
            });
        }

        // A name read on ANOTHER type is undecidable too, and that is the case
        // that loses PoolCapabilities.Provider: it has no reader, and .Provider
        // is read on LeaseRepoRef.
        var shared = declared
            .Where(d => owners[d.Member] > 1)
            .Select(Key)
            .ToHashSet(StringComparer.Ordinal);

        return new UnreadAnalysis
        {
            Findings = [.. findings.Where(f => !shared.Contains(Key(f)))],
            Undecidable = [.. undecidable.Concat(shared).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
        };
    }

    public static IReadOnlyList<Unread> Scan(IReadOnlyDictionary<string, string> source) =>
        Analyse(source).Findings;

    // ---- the corpus ----

    /// <summary>Whether a repository is around to have tests in.</summary>
    private static bool TestsAvailable()
    {
        try
        {
            return Directory.Exists(RepoRoot());
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "Gg.Contracts", "fact-vocabulary.json")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("repository root not found")).FullName;
    }

    private static bool Production(string path) =>
        path.EndsWith(".cs", StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    /// <summary>The runner's own source, as it stands.</summary>
    public static IReadOnlyDictionary<string, string> RunnerSource() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "Gg.Runner"), "*.cs", SearchOption.AllDirectories)
            .Where(Production)
            .ToDictionary(f => f, File.ReadAllText, StringComparer.Ordinal);

    /// <summary>
    /// The test source, which is what makes <i>one test</i> a sentence.
    /// </summary>
    /// <remarks>
    /// Every test project, not only the runner's: a member declared in the
    /// runner and read by a CLI test is still read by a test, and scoping this
    /// to one project would report it as wanted by nobody.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> TestSource() =>
        Directory.EnumerateDirectories(RepoRoot(), "*.Tests")
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
            .Where(Production)
            .ToDictionary(f => f, File.ReadAllText, StringComparer.Ordinal);

    /// <summary>The runner's source at a commit, without touching the tree.</summary>
    /// <remarks>
    /// No checkout, nothing to restore if it fails halfway — this runs beside
    /// the rest of the suite, and a test that moved the tree would take them
    /// with it.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> RunnerSourceAt(string commit)
    {
        var listed = Git("ls-tree", "-r", "--name-only", commit, "Gg.Runner/")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(f => f.EndsWith(".cs", StringComparison.Ordinal)
                     && !f.Contains("/obj/", StringComparison.Ordinal)
                     && !f.Contains("/bin/", StringComparison.Ordinal));

        return listed.ToDictionary(f => f, f => Git("show", $"{commit}:{f}"), StringComparer.Ordinal);
    }

    private static string Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = RepoRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var git = Process.Start(info)!;
        var output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();

        return git.ExitCode == 0
            ? output
            : throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} failed: {git.StandardError.ReadToEnd()}");
    }
}
