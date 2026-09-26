using Gg.Runner.Facts;

namespace Gg.Runner.Tests;

/// <summary>
/// A dependency's own lock file is not this project's dependency graph.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on GG-327.</b> Nine locks were fingerprinted for one flight and
/// six of them were vendored — <c>src/JDX.Web/node_modules/better-opn/yarn.lock</c>,
/// <c>combined-stream/yarn.lock</c>, <c>tablesort/_site/package-lock.json</c> and
/// three more. The three that matter are the repository's own.
/// </para>
/// <para>
/// <b>Why it is a defect rather than noise.</b> The point of this fact is that two
/// flights of one repository can be compared: the same locks, the same hashes,
/// the same dependency graph. A fingerprint that includes whatever lock files a
/// dependency happened to ship moves when an unrelated package publishes, so two
/// flights of the same commit can disagree — and it grows without bound with the
/// tree.
/// </para>
/// <para>
/// <b>The same argument the <c>.git</c> skip already makes</b>, one directory
/// over: <i>"git's own object store is not a customer's dependency graph."</i>
/// Neither is a vendored package's.
/// </para>
/// </remarks>
public class AVendoredLockIsNotOursTests
{
    private static DirectoryInfo Tree()
    {
        var root = Directory.CreateTempSubdirectory("gg-locks-");

        File.WriteAllText(Path.Combine(root.FullName, "package-lock.json"), "{\"ours\":1}");

        var web = root.CreateSubdirectory("src").CreateSubdirectory("web");
        File.WriteAllText(Path.Combine(web.FullName, "package-lock.json"), "{\"ours\":2}");

        // A dependency that shipped its own, which is ordinary and very common.
        var vendored = web.CreateSubdirectory("node_modules").CreateSubdirectory("better-opn");
        File.WriteAllText(Path.Combine(vendored.FullName, "yarn.lock"), "# theirs");

        return root;
    }

    [Test]
    public async Task Only_the_repositorys_own_locks_are_fingerprinted()
    {
        var tree = Tree();

        try
        {
            var seen = EnvironmentSurvey
                .Observe(tree.FullName, provenance: "fresh")
                .Locks.Select(l => l.Path)
                .Order(StringComparer.Ordinal)
                .ToList();

            await Assert.That(seen).IsEquivalentTo(
                new[] { "package-lock.json", "src/web/package-lock.json" })
                .Because("a fingerprint that moves when an unrelated package publishes its own "
                       + "lock file cannot be compared across two flights of one commit, which "
                       + "is the only thing this fact is for. Found: " + string.Join(", ", seen));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }
}
