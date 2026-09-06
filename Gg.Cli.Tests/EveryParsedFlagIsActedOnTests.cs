using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// A flag this CLI parses and nothing reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE GUARD THAT WOULD HAVE CAUGHT IT, and the defect it is named for was
/// live for a whole slice.</b> <c>--hand</c> parsed into
/// <c>CliAction.Fly.ByHand</c> and the dispatch arm read <c>fly.Text</c> and
/// never that, so <c>gg fly --hand</c> opened an ordinary fleet flight and
/// handed nobody a terminal — while the console key that spawns it was proven,
/// the arg parsing was proven, and the machinery underneath was proven. Every
/// piece existed; the read did not.
/// </para>
/// <para>
/// <b>Parsing is not acting, and nothing else can tell them apart.</b> An arg
/// test asserts the flag becomes a value. A behaviour test asserts what happens
/// when the value is used. Neither can see a value that is produced and never
/// consumed, which is precisely the gap a flag falls into — and the third time
/// this product has shipped machinery nothing constructs.
/// </para>
/// <para>
/// <b>Narrow on purpose.</b> <c>Gg.Runner.Tests.UnreadMemberTests</c> is the
/// general version and its corpus is the runner alone — widening that was tried
/// and reverted, because it also widens what its history retro-detection must
/// read and the two stopped agreeing. This asks a smaller question of a smaller
/// corpus: every member of every <c>CliAction</c> case, against the project that
/// dispatches them. No history, no exemptions, and nothing shared with its
/// sibling that could drift.
/// </para>
/// </remarks>
public class EveryParsedFlagIsActedOnTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "Gg.Cli", "CliArgs.cs")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("Gg.Cli not found")).FullName;
    }

    /// <summary>Every member of every action the parser can produce.</summary>
    /// <remarks>
    /// Read off <see cref="CliAction"/> itself rather than out of the source, so
    /// a case added without a member being read is caught by the same walk.
    /// </remarks>
    private static IEnumerable<(string Case, string Member)> Parsed() =>
        from action in typeof(CliAction).GetNestedTypes()
        where action.IsSealed && typeof(CliAction).IsAssignableFrom(action)
        from member in action.GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
        where member.DeclaringType == action
        select (action.Name, member.Name);

    [Test]
    public async Task Every_member_the_parser_produces_is_read_by_something_that_acts()
    {
        // THE PARSER'S OWN FILE IS NOT A READER. It constructs these; reading a
        // member there proves only that the value reaches itself, which is what
        // was true of ByHand for a whole slice.
        var acting = string.Join("\n", Directory
            .EnumerateFiles(Path.Combine(Root(), "Gg.Cli"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                       StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                       StringComparison.Ordinal)
                && Path.GetFileName(file) != "CliArgs.cs")
            .Select(File.ReadAllText)
            .Select(Code));

        // A DOTTED READ OR A PROPERTY PATTERN. `fly.ByHand` and
        // `Fly { ByHand: true }` are both a dispatch consulting the value; a
        // pattern is how the switch arm that fixes this actually reads it, and a
        // scan that only knew about dots would have called the fix unread.
        var unread = Parsed()
            .Where(member => !Regex.IsMatch(
                acting,
                @"(\.\s*|\{\s*|,\s*)" + Regex.Escape(member.Member) + @"\s*(\b|:)"))
            .Select(member => $"{member.Case}.{member.Member}")
            .ToList();

        await Assert.That(unread).IsEmpty()
            .Because("a flag the parser produces and the dispatch never reads is a feature "
                   + "that accepts its own arguments and does nothing - which is how "
                   + "`gg fly --hand` opened an ordinary fleet flight for a whole slice while "
                   + "every piece under it was proven. Read it, or stop parsing it. Found: "
                   + string.Join(", ", unread));
    }

    /// <summary>The file with its comments removed.</summary>
    /// <remarks>
    /// <b>THIS GUARD PASSED ON A DOC COMMENT BEFORE THIS LINE EXISTED.</b>
    /// <c>FlyByHandCommand</c>'s remarks say <i>"Program.cs read fly.Text and
    /// never fly.ByHand"</i> — a sentence ABOUT the defect, which satisfied a
    /// scan looking for <c>.ByHand</c> and made the whole assertion vacuous. A
    /// guard that a comment can satisfy is a guard describing itself, which is
    /// the failure this file exists to catch, found in this file.
    /// </remarks>
    private static string Code(string source) =>
        Regex.Replace(
            Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline),
            @"//.*?$", "", RegexOptions.Multiline);

    [Test]
    public async Task The_walk_reaches_the_member_that_was_missed()
    {
        // LIVENESS, and it names the actual case rather than a synthetic one. A
        // walk that found no members would satisfy the assertion above for
        // ever, and this is an absence assertion over a reflected set.
        var parsed = Parsed().Select(m => $"{m.Case}.{m.Member}").ToList();

        await Assert.That(parsed).Contains("Fly.ByHand");
        await Assert.That(parsed).Contains("Fly.Text");
        await Assert.That(parsed).Count().IsGreaterThan(30);
    }
}
