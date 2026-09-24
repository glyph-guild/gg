using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A route is declared when somebody serves it, and not before.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written from a live blockage rather than from a principle.</b>
/// <c>POST /v1/runner/members/{id}/introduction</c> was declared here, contract
/// 0.218.0 was cut for no other reason than to ship the declaration, and the
/// control plane never served it. <c>EndpointSurfaceTests</c> already records
/// the first half of that story in its own words: <i>"declared, merged to main,
/// and went nowhere."</i>
/// </para>
/// <para>
/// <b>What it costs while it sits here.</b> The control plane refuses a declared
/// route nobody serves, so its conformance suite fails the moment its contracts
/// pin moves past 0.217.0 — for any reason, by anybody. An unrelated change
/// discovered this by needing types from a later version: three assertions red,
/// none of them about that change. A declaration with no server does not wait
/// quietly; it freezes the pin for everyone.
/// </para>
/// <para>
/// <b>Nothing is lost by removing it.</b> No caller exists on this side — the
/// runner, the client, the CLI and the console never reach for it — so the
/// declaration was never holding anything up but the other repository. When the
/// door is served, it is re-declared in the same change, which costs one version
/// and strands nobody.
/// </para>
/// <para>
/// <b>Why a test and not just a deletion.</b> The deletion fixes today. This
/// fixes the next one: a route added here without a consumer is caught in the
/// repository that added it, rather than in the repository that has to serve it
/// and did not ask.
/// </para>
/// </remarks>
public class ARouteIsDeclaredWhenItIsServedTests
{
    [Test]
    public async Task No_runner_route_is_declared_that_this_side_never_calls()
    {
        // THE RUNNER SURFACE ONLY, deliberately. A developer route can be
        // declared for a console or a person to call by hand, and a tenant's
        // own tooling may reach one this repository knows nothing about. The
        // runner surface is different: the runner is IN this repository, so a
        // runner route nothing here calls is a route with no caller anywhere.
        var declared = ProtocolSurface.Endpoints
            .Where(e => e.Audience == Audience.Runner)
            .Select(e => e.Path)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var called = CallablePaths();

        var orphaned = declared
            .Where(path => !Exempt.ContainsKey(path))
            .Where(path => !called.Any(c => Matches(c, path)))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        await Assert.That(orphaned).IsEmpty()
            .Because("a runner route this repository declares and never calls has no caller "
                   + "anywhere, and the repository that must serve it cannot be asked by a "
                   + "declaration. Until it is served, the only thing it does is stop that "
                   + "repository moving its pin - which it does for every change, not only "
                   + "the one that added it. Found: " + string.Join(", ", orphaned));
    }

    /// <summary>
    /// Runner routes this repository declares and deliberately does not call,
    /// each with the reason it is not an orphan.
    /// </summary>
    /// <remarks>
    /// <b>A written exemption rather than a narrower rule</b>, because the
    /// exemptions are the interesting part: each one is a route whose caller is
    /// somebody other than gg, and that is worth being able to list.
    /// </remarks>
    private static IReadOnlyDictionary<string, string> Exempt { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // ITS CALLER IS A PERSON WITH A CREDENTIAL, which is the point of
            // it. "The runner protocol's smallest real endpoint: it tells a
            // runner who its actions will be attributed to", present so that
            // "a runner cannot reach the developer surface" is demonstrable
            // rather than vacuous. A route that exists to be reachable is not
            // one gg has to reach.
            ["/v1/runner/hello"] =
                "a credential check, called by whoever holds the credential rather than by gg",
        };

    /// <summary>Every request path this side builds, as written in the source.</summary>
    /// <remarks>
    /// <b>Read from the source rather than from a list somebody maintains</b>,
    /// because a list is the thing that goes stale in exactly the way this test
    /// exists to catch.
    /// </remarks>
    private static IReadOnlyList<string> CallablePaths()
    {
        var root = Root();
        var paths = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     // THE DECLARATION IS NOT A CALL, and leaving this out is
                     // how the first version of this test passed: every route
                     // matched itself, because the surface spells each one as a
                     // literal. A guard that reads its own subject as evidence
                     // is worse than none.
                     && !f.EndsWith("ProtocolSurface.cs", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}Gg.Contracts.Tests{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)))
        {
            foreach (var line in File.ReadLines(file))
            {
                var at = line.IndexOf("\"/v1/", StringComparison.Ordinal);
                if (at < 0)
                {
                    continue;
                }

                var rest = line[(at + 1)..];
                var end = rest.IndexOf('"', StringComparison.Ordinal);
                if (end > 0)
                {
                    paths.Add(rest[..end]);
                }
            }
        }

        return paths;
    }

    /// <summary>
    /// Whether a path written in code is the declared one, allowing for the
    /// interpolation that puts a real id where <c>{id}</c> stands.
    /// </summary>
    private static bool Matches(string written, string declared)
    {
        var segments = declared.Split('/');
        var actual = written.Split('/');

        if (segments.Length != actual.Length)
        {
            return false;
        }

        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i].StartsWith('{'))
            {
                continue;
            }

            if (!string.Equals(segments[i], actual[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Gg.sln is not above the test binary.");
    }
}
