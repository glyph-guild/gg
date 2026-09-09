using System.Text.RegularExpressions;

namespace Gg.Client.Tests;

/// <summary>
/// What the doctor tells somebody to do about a setting that is missing.
/// </summary>
/// <remarks>
/// <para>
/// <b>There are two ways to set one now, and advice that names only the older
/// one is advice that ages.</b> A person told <i>"Set GG_CONTROL_PLANE"</i> puts
/// it in a shell profile, which is exactly the thing the configuration file
/// exists to replace — and the next machine they stand up starts from nothing
/// again.
/// </para>
/// <para>
/// <b>The variable is still named</b>, because it still works and because CI and
/// a container are where it belongs. What changes is that it stops being the
/// only thing offered.
/// </para>
/// <para>
/// <b>Source, not reflection.</b> A fix line is a string in a branch, and no
/// type carries it — the argument <c>ConsoleSource</c> makes one project over.
/// </para>
/// </remarks>
public partial class TheDoctorNamesBothWaysToSetAThingTests
{
    private static string DoctorText()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        var root = (directory ?? throw new InvalidOperationException(
            "Gg.sln not found above " + AppContext.BaseDirectory)).FullName;

        return File.ReadAllText(Path.Combine(root, "Gg.Client", "Doctor.cs"));
    }

    /// <summary>Every string in the doctor that tells somebody to set a variable.</summary>
    /// <remarks>
    /// <b>Keyed on the advice, not on <c>Fix =</c>.</b> The first version of
    /// this matched the assignment and missed <c>GG_CONTROL_PLANE</c>, which is
    /// the one a person meets first — it sits in a ternary rather than a plain
    /// assignment. A walk that misses the commonest case is a walk that passes
    /// while the thing it is for is still wrong.
    /// </remarks>
    private static List<string> Advice(string source) =>
        // JOINED FIRST, because a sentence in this file is written as several
        // literals with `+` between them and the walk would otherwise judge the
        // halves. It found `...or set GG_VCS_HOSTS...` as its own line and
        // called it advice that offers nothing else, while the offer was in the
        // fragment above it.
        [.. Literal().Matches(Joined().Replace(source, ""))
            .Select(m => m.Groups[1].Value)
            .Where(s => s.Contains("GG_", StringComparison.Ordinal))
            // CASE-INSENSITIVE, AND THE ANCHOR IS WHY. Rewriting the advice to
            // put the file first turned "Set GG_..." into "...or set GG_...",
            // and an ordinal match went blind to every line it had just been
            // written to judge. The liveness anchor failed rather than the
            // walk quietly passing, which is the whole reason it is there.
            .Where(s => s.Contains("set ", StringComparison.OrdinalIgnoreCase)
                     || s.Contains("correct ", StringComparison.OrdinalIgnoreCase))];

    [Test]
    public async Task The_walk_finds_the_advice_it_is_about_to_judge()
    {
        // THE LIVENESS ANCHOR. A regex that matched nothing would make the test
        // below pass for a doctor whose every line said "export it".
        var advice = Advice(DoctorText());

        await Assert.That(advice).IsNotEmpty()
            .Because("the doctor does advise about variables, which is what this is for.");
        await Assert.That(advice.Any(
            a => a.Contains("GG_CONTROL_PLANE", StringComparison.Ordinal))).IsTrue()
            .Because("the control plane is the advice a person meets first, and it sits in "
                   + "a ternary rather than a plain assignment - the shape the first "
                   + "version of this walk missed.");
    }

    [Test]
    public async Task Advice_about_a_setting_offers_the_file_as_well_as_the_variable()
    {
        // A person following this is standing a machine up. Told only to export
        // something, they put it in a shell profile and the next machine starts
        // from nothing - which is the whole reason the file exists.
        var offenders = Advice(DoctorText())
            .Where(a => !a.Contains("gg config set", StringComparison.Ordinal))
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("these name a variable as the only way to set a thing that now lives "
                   + "in a file too:" + Environment.NewLine
                   + string.Join(Environment.NewLine, offenders));
    }

    [GeneratedRegex("\"((?:[^\"\\\\]|\\\\.)*)\"")]
    private static partial Regex Literal();

    /// <summary>The `" + "` between two halves of one sentence.</summary>
    [GeneratedRegex("\"\\s*\\+\\s*\"")]
    private static partial Regex Joined();
}
