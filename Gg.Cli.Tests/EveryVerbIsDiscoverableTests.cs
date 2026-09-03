using System.Text.RegularExpressions;

namespace Gg.Cli.Tests;

/// <summary>
/// A verb the parser accepts and the usage never mentions is a feature nobody
/// can find.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>gg gates</c>, <c>gg why</c> and <c>gg decide</c> shipped and were absent
/// from the usage output.</b> Those three are the entire approval path: a flight
/// stops on a human obligation, and the way to see it, understand it and open it
/// was discoverable only by reading the source. A person whose flight is blocked
/// and who types <c>gg</c> was told everything except the answer.
/// </para>
/// <para>
/// <b>Written as a walk rather than as three assertions, because three
/// assertions would have passed on the day this list was last correct.</b> The
/// usage list is maintained by hand and the parser is edited somewhere else, so
/// the two drift silently and only ever in one direction. Naming the three
/// missing verbs would fix today and guard nothing.
/// </para>
/// <para>
/// <b>The source is the subject.</b> The verbs are the first literal of each
/// top-level match arm, which is exactly what a person types, and reading them
/// from the file is the only way to know the set is complete - a reflective walk
/// over <c>CliAction</c> would find the types a verb produces, not the words it
/// is spelled with, and several types share a word.
/// </para>
/// </remarks>
public class EveryVerbIsDiscoverableTests
{
    /// <summary>
    /// Words that are options or arguments rather than verbs somebody types
    /// first.
    /// </summary>
    /// <remarks>
    /// Kept as data with the reason attached, so a future addition has to be
    /// argued rather than quietly appended.
    /// </remarks>
    private static readonly Dictionary<string, string> NotVerbs = new(StringComparer.Ordinal)
    {
        ["--version"] = "an option, and the usage names `gg version` for the same thing",
        ["-v"] = "the short form of the same option",
    };

    private static string SourcePath()
    {
        // Up from the test binary to the repository, the way the packaging
        // guard already finds a csproj.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException(
                "The repository root was not found above the test binary, so the parser's source "
              + "cannot be read. This guard reads the source deliberately; see the remarks.")
            : Path.Combine(directory.FullName, "Gg.Cli", "CliArgs.cs");
    }

    /// <summary>Every word a top-level match arm dispatches on.</summary>
    private static IReadOnlyList<string> VerbsThePaserAccepts()
    {
        var source = File.ReadAllText(SourcePath());

        // The first string literal of a list pattern: `["fly", …`. Anchored to
        // the arm's indentation so a nested pattern elsewhere is not mistaken
        // for a top-level verb.
        return [.. Regex.Matches(source, @"^\s{12}\[""(?<verb>[a-z][a-z-]*)""",
                    RegexOptions.Multiline)
                .Select(m => m.Groups["verb"].Value)
                .Distinct(StringComparer.Ordinal)
                .Where(v => !NotVerbs.ContainsKey(v))
                .OrderBy(v => v, StringComparer.Ordinal)];
    }

    [Test]
    public async Task The_walk_actually_finds_the_verbs()
    {
        // THE LIVENESS ANCHOR, and this file needs it more than most: a regex
        // that matched nothing would make every assertion below vacuously true,
        // and the guard would report success while reading an empty set.
        var verbs = VerbsThePaserAccepts();

        await Assert.That(verbs.Count).IsGreaterThan(10)
            .Because("a walk that found nothing would pass this whole file while guarding "
                   + "nothing at all.");
        await Assert.That(verbs).Contains("fly");
        await Assert.That(verbs).Contains("flights");
    }

    [Test]
    public async Task Every_verb_the_parser_accepts_appears_in_the_usage()
    {
        var usage = ((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message;

        var undiscoverable = VerbsThePaserAccepts()
            .Where(verb => !usage.Contains($"gg {verb}", StringComparison.Ordinal))
            .ToList();

        await Assert.That(undiscoverable).IsEmpty()
            .Because("a verb that works and is not in the usage is a feature only somebody "
                   + "reading the source can find. gg gates, gg why and gg decide are the "
                   + "whole approval path, and that is exactly what a person whose flight "
                   + "just stopped needs to be told.");
    }

    [Test]
    public async Task The_approval_path_is_named_together()
    {
        // The three by name as well as by the walk. The walk proves nothing is
        // missing; this says WHICH three mattered enough to open an issue, so
        // deleting them from the usage fails with a sentence rather than with a
        // set difference.
        var usage = ((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message;

        foreach (var verb in (string[])["gg gates", "gg why", "gg decide"])
        {
            await Assert.That(usage).Contains(verb)
                .Because("a flight stops on a human obligation and this is how somebody sees "
                       + "it, understands it, and opens it.");
        }
    }
}
