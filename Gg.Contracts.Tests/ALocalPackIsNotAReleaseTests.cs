using System.Diagnostics;
using System.Reflection;

namespace Gg.Contracts.Tests;

/// <summary>
/// A package built on somebody's machine must not be able to occupy a version
/// number that a release published.
/// </summary>
/// <remarks>
/// <para>
/// WRITTEN AFTER IT HAPPENED. A branch that had not merged was packed locally
/// at 0.114.0 and landed in ~/.nuget/packages/glyphguild.gg.contracts/0.114.0.
/// NuGet prefers the global cache over a downloaded asset, so the control plane
/// silently compiled against a contract nobody had published - it carried a
/// member from the unmerged branch - and then demanded an endpoint the deployed
/// API did not have. Every number in sight said 0.114.0. The two assemblies
/// differed by 512 bytes.
/// </para>
/// <para>
/// So a pack is local unless it says otherwise: <c>ContractRelease</c> is the
/// signal, the publish workflow is the only place that sets it, and a local
/// pack gets a prerelease label that no release can ever wear. SemVer sorts a
/// prerelease BELOW its release, so a local pack cannot win a version range
/// either, and the timestamp makes each one distinct - a stale cache entry
/// cannot shadow the pack you just built.
/// </para>
/// <para>
/// The two versions are read out of MSBuild rather than out of the csproj text,
/// because what a consumer restores is the evaluated property and nothing else.
/// A test that read the XML would pass while the value the SDK computes went
/// somewhere else entirely.
/// </para>
/// </remarks>
public class ALocalPackIsNotAReleaseTests
{
    [Test]
    public async Task A_pack_from_a_working_copy_cannot_claim_a_released_version()
    {
        var declared = Evaluate("Version");
        var packed = Evaluate("PackageVersion");

        await Assert.That(packed).IsNotEqualTo(declared)
            .Because($"a pack from here would publish {packed}, which is exactly the number "
                   + "the release publishes. Nothing downstream could tell the two apart, and "
                   + "the one in the NuGet cache wins.");

        await Assert.That(packed).StartsWith(declared + "-local.")
            .Because($"a local pack is labelled a prerelease of the version it was built from, "
                   + $"so it sorts below the release and reads as what it is. Found: {packed}");
    }

    [Test]
    public async Task The_release_pack_still_publishes_the_declared_version_exactly()
    {
        var declared = Evaluate("Version");
        var released = Evaluate("PackageVersion", release: true);

        await Assert.That(released).IsEqualTo(declared)
            .Because("the published package is the declared contract version, unadorned. "
                   + "A mechanism that labelled the release too would move the number the "
                   + "ledger holds to account and the control plane pins.");
    }

    [Test]
    public async Task The_workflow_that_publishes_the_contract_is_the_one_that_asks_for_a_release()
    {
        var workflow = Path.Combine(RepoRoot().FullName, ".github", "workflows", "publish-contracts.yml");
        var packs = File.ReadAllLines(workflow)
            .Where(line => line.Contains("dotnet pack", StringComparison.Ordinal))
            .ToList();

        await Assert.That(packs.Count).IsEqualTo(1)
            .Because($"the release channel is one pack step; found {packs.Count} in {workflow}");

        await Assert.That(packs[0]).Contains("-p:ContractRelease=true", StringComparison.Ordinal)
            .Because("this is the only pack that may claim the declared version, and it has to "
                   + "say so. Without it the release would publish a -local label. Found: "
                   + packs[0].Trim());
    }

    [Test]
    public async Task The_version_the_assembly_reports_is_the_declared_one_either_way()
    {
        // The label is on the PACKAGE, not on the assembly: the ledger guard in
        // ContractSurfaceTests reads AssemblyInformationalVersion, and a suffix
        // there would fail every local build with a version the ledger has no
        // entry for. What the fix needs is that a consumer cannot RESTORE a
        // local build under a released number, and the package version is where
        // that is decided.
        var informational = typeof(Vocabulary).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion
            .Split('+')[0];

        await Assert.That(informational).IsEqualTo(Evaluate("Version"))
            .Because("the assembly keeps saying what surface it is; only the package says "
                   + "where it came from.");
    }

    /// <summary>
    /// What MSBuild actually computes for <paramref name="property"/>, with the
    /// release signal set or not.
    /// </summary>
    private static string Evaluate(string property, bool release = false)
    {
        var csproj = Path.Combine(RepoRoot().FullName, "Gg.Contracts", "Gg.Contracts.csproj");
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        info.ArgumentList.Add("msbuild");
        info.ArgumentList.Add(csproj);
        info.ArgumentList.Add($"-getProperty:{property}");
        info.ArgumentList.Add("-nologo");
        if (release)
        {
            info.ArgumentList.Add("-p:ContractRelease=true");
        }

        using var msbuild = Process.Start(info)
            ?? throw new InvalidOperationException("could not start dotnet msbuild");
        var output = msbuild.StandardOutput.ReadToEnd().Trim();
        msbuild.WaitForExit();

        // Loud rather than vacuous: an empty answer would satisfy nothing above
        // by accident, but it would report the wrong reason.
        return msbuild.ExitCode == 0 && output.Length > 0
            ? output
            : throw new InvalidOperationException(
                $"dotnet msbuild -getProperty:{property} failed ({msbuild.ExitCode}): "
                + msbuild.StandardError.ReadToEnd() + output);
    }

    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }
        return dir ?? throw new InvalidOperationException("Gg.sln not found above " + AppContext.BaseDirectory);
    }
}
