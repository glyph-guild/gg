using System.Text.RegularExpressions;

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

    /// <summary>
    /// A provider name, where a provider name can actually start.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The bare substring match forbade the English word "central".</b> It
    /// contains <c>entra</c>, as do <i>concentrate</i>, <i>decentralised</i> and
    /// every other word built on the same root — so the guard failed a build
    /// over a doc comment, which teaches whoever hits it to reword rather than
    /// to think about the boundary this exists to protect. A guard that cries
    /// wolf is a guard people learn to route around.
    /// </para>
    /// <para>
    /// <b>Narrowed at the front only, and that is deliberate.</b> Every real
    /// reference starts where a word starts — <c>Entra</c>, <c>EntraAdapter</c>,
    /// <c>entra_client_id</c>, <c>"entra"</c> — so requiring a non-letter before
    /// it loses nothing. The END stays open, because <c>githubToken</c> and
    /// <c>OktaOptions</c> are exactly what this hunts and a trailing boundary
    /// would let both through. A word that merely BEGINS with a provider name
    /// still trips, and should: without a dictionary that is indistinguishable
    /// from the real thing.
    /// </para>
    /// </remarks>
    private static bool Names(string text, string provider) =>
        Regex.IsMatch(text, $@"(?<![A-Za-z]){Regex.Escape(provider)}", RegexOptions.IgnoreCase);

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
                if (Names(text, provider))
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

    [Test]
    public async Task The_narrowing_still_catches_every_shape_a_leak_takes()
    {
        // THE HALF THAT MATTERS. The scan above is an absence, and narrowing an
        // absence check is exactly the move that quietly turns a guard off - so
        // the shapes a real leak takes are named here and asserted to still
        // trip. All of them start where a word starts, which is why the front
        // boundary costs nothing.
        foreach (var leak in (string[])
                 ["entra", "Entra", "ENTRA", "EntraAdapter", "entra_client_id",
                  "\"entra\"", "var x = Entra.Thing;", "// use entra here",
                  "githubToken", "OktaOptions", "https://gitlab.example",
                  "auth0Domain", "bitbucket-server"])
        {
            var caught = ProviderNames.Any(p => Names(leak, p));

            await Assert.That(caught).IsTrue()
                .Because($"'{leak}' names a provider and the guard did not see it, which is the "
                       + "failure the front-boundary narrowing must not have introduced.");
        }
    }

    [Test]
    public async Task Ordinary_english_no_longer_fails_a_build()
    {
        // THE POISON TWIN, in the other direction: these are words this
        // repository is entitled to use, and every one of them failed a build
        // until the match required a word boundary at the front.
        foreach (var innocent in (string[])
                 ["the product's central claim", "concentrate", "decentralised",
                  "a concentration of flights"])
        {
            var caught = ProviderNames.Any(p => Names(innocent, p));

            await Assert.That(caught).IsFalse()
                .Because($"'{innocent}' names no provider, and a guard that fails a build over "
                       + "it teaches people to reword rather than to think about the boundary.");
        }
    }
}
