namespace Gg.Contracts.Tests;

/// <summary>
/// This repository is public and distributed. Nothing in it may name an
/// identity provider.
/// </summary>
/// <remarks>
/// The control plane brokers the device flow precisely so that gg stays
/// provider-neutral: when a second adapter ships, this binary does not change,
/// not even a flag. A provider name appearing here is the first sign that
/// property has been given away.
/// </remarks>
public class ProviderNeutralityTests
{
    private static readonly string[] ProviderNames =
        ["github", "entra", "okta", "auth0", "gitlab", "bitbucket"];

    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }
        return dir ?? throw new InvalidOperationException("Gg.sln not found above " + AppContext.BaseDirectory);
    }

    [Test]
    public async Task NoSourceFileNamesAnIdentityProvider()
    {
        var root = RepoRoot().FullName;
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            // Generated build output is not source we author, and it carries
            // the repository's own hosting URL rather than a protocol choice.
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains(".whizbang-generated", StringComparison.Ordinal)
                // This file necessarily contains the names it hunts for.
                || Path.GetFileName(file) == "ProviderNeutralityTests.cs")
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (var provider in ProviderNames)
            {
                if (text.Contains(provider, StringComparison.OrdinalIgnoreCase))
                {
                    offenders.Add($"{Path.GetRelativePath(root, file)}: '{provider}'");
                }
            }
        }

        var detail = offenders.Count == 0 ? "" : " Found: " + string.Join("; ", offenders);
        await Assert.That(offenders).IsEmpty()
            .Because("gg talks only to the control plane; a provider name here means that boundary "
                   + "has leaked into a public binary." + detail);
    }
}
