using System.Text.RegularExpressions;
using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// No user-facing text claims a move was refused, because nothing refused one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Article XII, not a missing feature.</b> The seed a person reads when taking a
/// flight over said <i>"moves refused"</i>, and the value behind it is computed as
/// <c>tools the agent reached for that the envelope did not declare</c> - which the
/// agent then <b>used</b>, because the allow-list passed to the executor does not
/// bind. So the label asserted that the system stopped something it in fact allowed:
/// a false statement about what an agent did, in a record somebody relies on.
/// </para>
/// <para>
/// <b>The weaker true thing, plainly.</b> The system cannot distinguish <i>used</i>
/// from <i>refused</i> today, so it says the one it can support - and
/// <c>gg doctor</c> carries a line saying moves are not enforced, because silent
/// degradation writes a line.
/// </para>
/// </remarks>
public class MovesWordingTests
{
    [Test]
    public async Task No_user_facing_text_claims_a_move_was_refused()
    {
        // The scan is over what a PERSON reads: rendered text and the labels beside
        // it. The wire field keeps its name - renaming a member on a pinned fact type
        // is a vocabulary event - and every place it is displayed says the true thing.
        var claiming = new Regex(
            @"""[^""]*moves? (refused|blocked|withheld|denied)[^""]*""" +
            @"|""[^""]*(refused|blocked|denied) moves?[^""]*""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // COMMENTS STRIPPED FIRST. The files that fixed this label explain what it
        // used to say, and a scan that read those explanations as offences would make
        // recording the reason impossible - which is the opposite of what this is for.
        var offenders = Sources()
            .Where(f => claiming.IsMatch(Code(f)))
            .Select(f => Path.GetFileName(f)!)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("nothing refused a move, so no label may say one was. Found: "
                   + string.Join(", ", offenders));

        // The scan can see one, so the emptiness above means something.
        await Assert.That(claiming.IsMatch("Section(text, \"moves refused\", x);")).IsTrue();
    }

    private static string Code(string file) =>
        string.Join('\n', File.ReadAllLines(file)
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("///", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("*", StringComparison.Ordinal)));

    private static IEnumerable<string> Sources()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = dir ?? throw new InvalidOperationException("Gg.sln not found");

        return new[] { "Gg.Client", "Gg.Cli", "Gg.Console", "Gg.Runner" }
            .SelectMany(project => Directory.EnumerateFiles(
                Path.Combine(root.FullName, project), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal));
    }
}
