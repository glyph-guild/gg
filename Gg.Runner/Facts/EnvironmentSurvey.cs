using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Gg.Contracts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Facts;

/// <summary>
/// What ran, and where.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tests passed is not a fact without the environment they passed in.</b>
/// Warm pools are years away and this still belongs in slice one: a laptop is
/// the least reproducible environment in the fleet, which makes recording it
/// more important locally rather than less.
/// </para>
/// <para>
/// Everything here is a hash, a count, a version or a path. There is nothing it
/// could read out of a customer's tree and send: lock files are hashed, and the
/// hash is what answers the question a lock file is being asked - did two runs
/// resolve the same dependencies.
/// </para>
/// </remarks>
public static class EnvironmentSurvey
{
    /// <summary>
    /// Which files count as a dependency lock.
    /// </summary>
    /// <remarks>
    /// A list rather than a pattern, because "anything that looks like a lock
    /// file" would eventually hash something that is not one, and the hash of a
    /// file nobody expected is a fact nobody can interpret.
    /// </remarks>
    private static readonly string[] LockFileNames =
    [
        "package-lock.json", "yarn.lock", "pnpm-lock.yaml", "npm-shrinkwrap.json",
        "Cargo.lock", "go.sum", "poetry.lock", "Pipfile.lock", "uv.lock",
        "Gemfile.lock", "composer.lock", "packages.lock.json", "gradle.lockfile",
    ];

    /// <summary>The variable an orchestrator sets to name the image it started.</summary>
    /// <remarks>
    /// Read rather than derived. Container runtimes disagree about how to
    /// discover your own image and most of the tricks are wrong somewhere; a
    /// value the thing that started us put there is the only one that is right
    /// everywhere, and absent is an honest answer on a laptop.
    /// </remarks>
    public const string ImageDigestVariable = "GG_IMAGE_DIGEST";

    /// <summary>Observes this machine, and the tree if there is one.</summary>
    public static EnvironmentIdentity Observe(
        string? treePath, string provenance, string? imageDigest = null)
    {
        var digest = imageDigest ?? Environment.GetEnvironmentVariable(ImageDigestVariable);

        return new EnvironmentIdentity
        {
            HostFingerprint = Fingerprint(digest),
            ImageDigest = string.IsNullOrWhiteSpace(digest) ? null : digest,
            Locks = treePath is null ? [] : LocksUnder(treePath),
            Tools = Tools(),
            Provenance = provenance,
        };
    }

    /// <summary>
    /// A hash of the stable facts about this environment.
    /// </summary>
    /// <remarks>
    /// Of the ENVIRONMENT, not of the machine: the operating system, the
    /// architecture, how many processors, the runtime, and the image when there
    /// is one. The runner's label already identifies the box; hashing a
    /// hostname would carry an identifier nobody needs into a fact whose whole
    /// purpose is reproducibility.
    /// </remarks>
    private static string Fingerprint(string? imageDigest) =>
        Sha256(string.Join('\n',
        [
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.FrameworkDescription,
            Environment.ProcessorCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            imageDigest ?? "",
        ]));

    /// <summary>
    /// The tools that did the work.
    /// </summary>
    /// <remarks>
    /// git, because git is what put the source on disk and a run nobody can
    /// reproduce is one where nobody recorded which git did it. The service
    /// versions a warm pool would list - a database, a broker - arrive with the
    /// executor, and listing none today is honest rather than incomplete.
    /// </remarks>
    private static IReadOnlyList<ToolVersion> Tools()
    {
        var tools = new List<ToolVersion>
        {
            new() { Name = "runtime", Version = RuntimeInformation.FrameworkDescription },
        };

        try
        {
            var reported = GitInvocation.Plain("--version").RunAsync(Environment.CurrentDirectory)
                .GetAwaiter().GetResult().Trim();
            tools.Add(new ToolVersion { Name = "git", Version = reported });
        }
        catch (InvalidOperationException failure)
        {
            // Recorded as unavailable rather than omitted. A missing entry
            // reads as "nobody looked"; this reads as "it was not there",
            // which is the fact - and it is the fact that explains why the
            // flight materialized nothing.
            tools.Add(new ToolVersion { Name = "git", Version = "unavailable: " + failure.Message });
        }

        return tools;
    }

    /// <summary>Every dependency lock in the tree, as a relative path and a hash.</summary>
    private static IReadOnlyList<LockHash> LocksUnder(string treePath)
    {
        var locks = new List<LockHash>();

        foreach (var file in Directory.EnumerateFiles(treePath, "*", SearchOption.AllDirectories))
        {
            // git's own object store is not a customer's dependency graph.
            if (file.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!LockFileNames.Contains(Path.GetFileName(file), StringComparer.Ordinal))
            {
                continue;
            }

            locks.Add(new LockHash
            {
                // Relative, and with forward slashes: an absolute path names
                // this machine's directory layout, which is a fact about us
                // rather than about the flight.
                Path = Path.GetRelativePath(treePath, file).Replace('\\', '/'),
                Sha256 = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant(),
            });
        }

        return [.. locks.OrderBy(l => l.Path, StringComparer.Ordinal)];
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
