using System.Net;
using System.Text.RegularExpressions;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// The pool proxy admits a build into the host's own registry and nothing else,
/// and the scope probe proves it before any build is decided.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice forty-one, S41.3-04, rule 17 - found by step 0.</b> The maintainer
/// reaches the daemon only through <c>gg-pool-proxy</c>, which admitted
/// <c>gg-pool-*</c> containers and answered 403 to everything else, a build
/// and a push included. That proxy is the boundary the scope probe proves on
/// every pass, so a build is not a new call through an existing door: it widens
/// the scope the maintainer holds. It gets exactly two allowances, and the probe
/// asks for the reach just outside them and requires the refusal.
/// </para>
/// <para>
/// <b>Why the arguments matter here and nowhere else.</b>
/// <c>ScopeProxyReachTests</c> reads paths, because until now the path was the
/// whole of a rule. A build's path is always <c>/build</c> - where it may go is
/// in <c>t</c>, and whether it fetches a context from somewhere is in
/// <c>remote</c> - so this reads the conditions of that one block as well.
/// nginx does not decode <c>$arg_</c> values, so a tag arrives percent-encoded
/// and the rule has to admit that spelling too.
/// </para>
/// </remarks>
public class TheBuildScopeIsProbedTests
{
    private static string Config()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(Path.Combine(directory!.FullName, "scripts", "pool-proxy", "nginx.conf"));
    }

    /// <summary>The body of the first regex location whose matcher matches the path.</summary>
    private static string? BlockFor(string path)
    {
        var source = Config();
        foreach (Match opening in Regex.Matches(source, @"location\s+(?<kind>~\*?|=)?\s*(?<matcher>\S+)\s*\{"))
        {
            var depth = 1;
            var index = opening.Index + opening.Length;
            var start = index;
            while (index < source.Length && depth > 0)
            {
                if (source[index] == '{') { depth++; }
                else if (source[index] == '}') { depth--; }
                index++;
            }

            var matcher = opening.Groups["matcher"].Value;
            var hit = opening.Groups["kind"].Value.StartsWith('~')
                ? Regex.IsMatch(path, matcher)
                : string.Equals(path, matcher, StringComparison.Ordinal);
            if (hit)
            {
                return source[start..Math.Max(start, index - 1)];
            }
        }

        return null;
    }

    /// <summary>Whether nginx would proxy this request, honouring the block's argument conditions.</summary>
    private static bool Admits(string pathAndQuery)
    {
        var parts = pathAndQuery.Split('?', 2);
        var body = BlockFor(parts[0]);
        if (body is null || !body.Contains("proxy_pass", StringComparison.Ordinal))
        {
            return false;
        }

        var query = (parts.Length > 1 ? parts[1] : "")
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => p.Length > 1 ? p[1] : "", StringComparer.Ordinal);

        // if ($arg_x !~ regex) { return 403; }  - refuse unless the argument matches.
        foreach (Match rule in Regex.Matches(body, @"if\s*\(\s*\$arg_(?<arg>\w+)\s*!~\s*(?<re>[^)]+?)\s*\)\s*\{\s*return 403;"))
        {
            var value = query.GetValueOrDefault(rule.Groups["arg"].Value, "");
            if (!Regex.IsMatch(value, rule.Groups["re"].Value.Trim()))
            {
                return false;
            }
        }

        // if ($arg_x != "") { return 403; }  - refuse when the argument is present at all.
        foreach (Match rule in Regex.Matches(body, @"if\s*\(\s*\$arg_(?<arg>\w+)\s*!=\s*""""\s*\)\s*\{\s*return 403;"))
        {
            if (query.GetValueOrDefault(rule.Groups["arg"].Value, "") is { Length: > 0 })
            {
                return false;
            }
        }

        return true;
    }

    [Test]
    public async Task A_build_into_the_hosts_own_registry_is_admitted()
    {
        foreach (var admitted in (string[])
                 [
                     "/v1.43/build?t=127.0.0.1:5000/gg-member:1c26776e2b1f&dockerfile=Dockerfile",
                     "/v1.43/build?t=127.0.0.1%3A5000%2Fgg-member%3A1c26776e2b1f&dockerfile=Dockerfile",
                     "/build?t=127.0.0.1:5000/gg-member-browser:abc",
                 ])
        {
            await Assert.That(Admits(admitted)).IsTrue()
                .Because($"'{admitted}' is the build the maintainer makes; refusing it is a pool "
                       + "that can never build.");
        }
    }

    [Test]
    public async Task A_build_tagged_anywhere_else_is_refused()
    {
        foreach (var refused in (string[])
                 [
                     "/v1.43/build?t=docker.io/someone/else:latest",
                     "/v1.43/build?t=gg-scope-probe/outside:x",
                     "/v1.43/build?t=127.0.0.1:5001/gg-member:x",
                     "/v1.43/build",
                 ])
        {
            await Assert.That(Admits(refused)).IsFalse()
                .Because($"'{refused}' puts an image somewhere a pool does not pin from - rule 11.");
        }
    }

    [Test]
    public async Task A_build_that_would_fetch_its_own_context_is_refused()
    {
        // REMOTE IS A CONTEXT THE DAEMON FETCHES ITSELF - a git url or a tarball
        // - which would put the recipe's source outside everything the runner
        // resolved and reported.
        await Assert.That(Admits(
                "/v1.43/build?t=127.0.0.1:5000/gg-member:x&remote=https://example.invalid/r.git"))
            .IsFalse();
    }

    [Test]
    public async Task A_push_of_the_hosts_own_registry_is_admitted_and_nothing_else_is()
    {
        await Assert.That(Admits("/v1.43/images/127.0.0.1:5000/gg-member/push?tag=1c26776e2b1f")).IsTrue();

        foreach (var refused in (string[])
                 [
                     "/v1.43/images/docker.io/someone/else/push?tag=x",
                     "/v1.43/images/gg-member/push?tag=x",
                     "/v1.43/images/json",
                     "/v1.43/images/127.0.0.1:5000/gg-member/json",
                 ])
        {
            await Assert.That(Admits(refused)).IsFalse()
                .Because($"'{refused}' is not the push of an image into the host's registry.");
        }
    }

    [Test]
    public async Task Everything_it_refused_before_it_still_refuses()
    {
        foreach (var refused in (string[])
                 [
                     "/v1.43/containers/somebody-elses/json",
                     "/v1.43/containers/gg-pool-dev-1/archive",
                     "/v1.43/containers/gg-pool-dev-1/exec",
                     "/v1.43/volumes",
                     "/v1.43/networks",
                 ])
        {
            await Assert.That(Admits(refused)).IsFalse();
        }
    }

    // ---- the probe ----

    private sealed class Refusing(Func<HttpRequestMessage, bool> allows) : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.Add($"{request.Method} {request.RequestUri!.PathAndQuery}");
            return Task.FromResult(new HttpResponseMessage(
                allows(request) ? HttpStatusCode.OK : HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{}"),
            });
        }
    }

    private static DockerPoolAdapter Adapter(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://proxy.test") });

    [Test]
    public async Task The_probe_asks_for_a_build_outside_the_registry_and_requires_the_refusal()
    {
        var proxy = new Refusing(_ => false);

        var probe = await Adapter(proxy).ProbeScopeAsync();

        await Assert.That(probe.Held).IsTrue();
        await Assert.That(proxy.Asked.Any(a => a.StartsWith("POST /build?", StringComparison.Ordinal)))
            .IsTrue()
            .Because("a probe that never asked for the build it must be refused has proved nothing "
                   + "about it.");
    }

    [Test]
    public async Task A_proxy_that_lets_a_foreign_build_through_is_a_broken_bound()
    {
        // THE POISON TWIN. Containers refused, builds allowed: the old bound
        // holds and the new one does not, and the probe must say which.
        var proxy = new Refusing(request => request.RequestUri!.AbsolutePath.EndsWith("/build", StringComparison.Ordinal));

        var probe = await Adapter(proxy).ProbeScopeAsync();

        await Assert.That(probe.Held).IsFalse();
        await Assert.That(probe.Diagnosis!).Contains("build");
    }

    [Test]
    public async Task A_proxy_that_lets_a_foreign_push_through_is_a_broken_bound()
    {
        var proxy = new Refusing(request => request.RequestUri!.AbsolutePath.EndsWith("/push", StringComparison.Ordinal));

        var probe = await Adapter(proxy).ProbeScopeAsync();

        await Assert.That(probe.Held).IsFalse();
        await Assert.That(probe.Diagnosis!).Contains("push");
    }
}
