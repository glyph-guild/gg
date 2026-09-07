using System.Text.RegularExpressions;

namespace Gg.Console.Tests;

/// <summary>
/// The patched SIPSorcery build is a fork, and a fork nobody is reminded of is
/// a fork forever.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists.</b> Upstream SIPSorcery does not work under Native AOT: the
/// SCTP state cookie is carried as reflection-serialized JSON, the trimmer
/// removes its members, and the association stalls in <c>CookieEchoed</c> — so
/// WebRTC data channels never open. gg publishes Native AOT. The fix is
/// sipsorcery-org/sipsorcery#1817.
/// </para>
/// <para>
/// <b>What this guards is not the fork but the FORGETTING.</b> A pinned
/// prerelease with a comment is easy to read past a year later, by which time
/// nobody remembers whether upstream fixed it. So the pin has to keep saying
/// why, out loud, in a place that fails.
/// </para>
/// <para>
/// <b>And why a prerelease label rather than a renamed package.</b> SemVer
/// sorts <c>10.0.16-gg.1</c> BELOW <c>10.0.16</c>, so this build can never win a
/// version range against a real release — good-grief lost a day to a locally
/// packed version that could, and the assemblies differed by 512 bytes. Keeping
/// the package id means dropping the fork is a one-line change rather than a
/// sweep through every <c>using</c>.
/// </para>
/// </remarks>
public class TheSipsorceryForkIsTemporaryTests
{
    private static string Pinned() =>
        Regex.Match(
            Sources.Read("Directory.Packages.props"),
            @"""SIPSorcery""\s+Version=""([^""]+)""").Groups[1].Value;

    [Test]
    public async Task The_pin_says_it_is_not_upstream()
    {
        var version = Pinned();

        await Assert.That(version).IsNotEmpty()
            .Because("nothing else in this repo knows which SIPSorcery to fetch.");

        // A FORK PIN OR AN UPSTREAM ONE, AND EITHER IS FINE - what must not
        // happen is a fork pin that does not look like one. `10.0.16` would
        // read as upstream while ./packages quietly answered for it.
        if (!version.Contains("-gg.", StringComparison.Ordinal))
        {
            await Assert.That(Sources.Read("nuget.config")).DoesNotContain("sipsorcery-fork")
                .Because("the pin is an upstream release now, so the local folder source is "
                       + "dead weight that can only shadow it. Delete the source and "
                       + "scripts/fetch-sipsorcery.sh with it.");

            return;
        }

        await Assert.That(version).StartsWith("10.0.16-gg.")
            .Because("a fork version that is not a prerelease of the release it patches can "
                   + "win a version range against upstream, which is how a build compiles "
                   + "against something nobody published.");
    }

    [Test]
    public async Task The_pin_names_what_would_let_it_go()
    {
        if (!Pinned().Contains("-gg.", StringComparison.Ordinal))
        {
            return;
        }

        var pins = Sources.Read("Directory.Packages.props");

        await Assert.That(pins).Contains("sipsorcery-org/sipsorcery/pull/1817")
            .Because("a pin whose reason is not one click away is a pin nobody can retire. "
                   + "The PR is what says whether this is still needed.");
    }

    [Test]
    public async Task The_folder_source_answers_for_one_package_only()
    {
        if (!Pinned().Contains("-gg.", StringComparison.Ordinal))
        {
            return;
        }

        var config = Sources.Read("nuget.config");

        await Assert.That(config).Contains("<packageSourceMapping>")
            .Because("without a mapping a local directory is a source for EVERY id in it, so "
                   + "a stray .nupkg would silently shadow nuget.org - which is the same "
                   + "class of failure as the fork itself, arriving by accident.");

        await Assert.That(config).Contains("<package pattern=\"SIPSorcery\" />");
    }

    [Test]
    public async Task What_is_fetched_is_never_committed()
    {
        // The packages folder holds a 34MB binary somebody else built. It is
        // reproducible from the release, so committing it would put a large
        // opaque artifact in the history to no purpose.
        await Assert.That(Sources.Read(".gitignore")).Contains("packages/");
    }
}
