using System.Text.RegularExpressions;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// One fact, one sentence, however many surfaces render it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this closes was found writing the criterion for it.</b> The
/// engine's plan rendered <i>"this work kind cannot produce X, so this rule can
/// never apply to it"</i> and <c>gg why</c> rendered <i>"this kind of work
/// cannot produce X, so this rule can never apply to it"</i>. Two paraphrases of
/// one fact, composed independently in two repositories, agreeing today because
/// somebody typed carefully twice.
/// </para>
/// <para>
/// <b>Why that matters more than it looks.</b> A person reading <c>gg why</c>
/// and a person reading a plan are checking the same claim, and the only thing
/// they can compare is the words. Two spellings is the manifest hazard wearing a
/// different coat — two computations that agree until one of them is fixed.
/// </para>
/// <para>
/// <b>So the sentence moves into the package both sides already reference</b>,
/// beside the vocabulary it names, and neither side is allowed a second copy of
/// it. Asserted by a scan rather than claimed, because a literal is exactly the
/// kind of thing that gets retyped.
/// </para>
/// </remarks>
public class WhyRendersTests
{
    [Test]
    public async Task The_sentence_names_the_family_and_says_it_could_never_apply()
    {
        var sentence = Inapplicability.Because(FactKinds.ChangeManifest);

        await Assert.That(sentence).Contains(FactKinds.ChangeManifest);
        await Assert.That(sentence).Contains("never")
            .Because("'could never apply' is the whole distinction from 'was measured and did "
                   + "not fire', and the second may fire tomorrow.");
    }

    [Test]
    public async Task An_unknown_family_is_refused_rather_than_rendered()
    {
        // The sentence is about a family somebody can look up. Composing one
        // for a name the vocabulary does not carry would produce a reason
        // nobody can check, which is the failure Attribution.Validate already
        // refuses on the wire.
        await Assert.That(() => Inapplicability.Because("no.such.family"))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Nothing_composes_a_second_version_of_it()
    {
        // THE SCAN, and it is the point of the whole file. A second literal
        // agreeing today is two computations of one sentence, and the first fix
        // to either is the day they diverge with nothing noticing.
        var root = RepoRoot();

        var offenders = Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal))
            .Where(f => !string.Equals(
                Path.GetFileName(f), "Inapplicability.cs", StringComparison.Ordinal))
            .Where(f => !string.Equals(
                Path.GetFileName(f), "WhyRendersTests.cs", StringComparison.Ordinal))
            .Where(f => Regex.IsMatch(
                File.ReadAllText(f), @"cannot produce|can never apply"))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("the sentence lives in one place and every surface calls it. Found: "
                   + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_scan_can_actually_fail()
    {
        // Liveness, because the assertion above passes on today's tree and
        // would pass just as well if the pattern matched nothing ever.
        await Assert.That(Regex.IsMatch(
                "this work kind cannot produce change.manifest", @"cannot produce|can never apply"))
            .IsTrue();
        await Assert.That(Regex.IsMatch(
                "var s = Inapplicability.Because(family);", @"cannot produce|can never apply"))
            .IsFalse()
            .Because("the one legitimate shape must not match, or the exemptions would be "
                   + "carrying the whole scan.");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Gg.sln was not found above the test binary.");
    }
}
