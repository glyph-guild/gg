using System.Text.RegularExpressions;

namespace Gg.Runner.Tests;

/// <summary>
/// What the scope proxy actually lets through, decided by reading its routing
/// table rather than its comment.
/// </summary>
/// <remarks>
/// <para>
/// <b>The comment and the config disagree.</b> The file's summary says what is
/// allowed <i>"and nothing else"</i>, and lists it by endpoint. The member rule is
/// <c>location ~ ^(/v[0-9.]+)?/containers/gg-pool-</c>, which is **unanchored**,
/// so it matches every sub-path of a member: <c>/exec</c>, <c>/attach</c>, and
/// <c>PUT|GET /archive</c> are proxied today.
/// </para>
/// <para>
/// <b>Why it has not bitten yet, and why that is about to change.</b> Members are
/// empty <c>ubuntu:24.04</c> containers, so reading their filesystem out through
/// <c>/archive</c> discloses nothing. The whole point of the work this precedes is
/// to put a customer's repository inside them. This is the last moment where
/// tightening it is free.
/// </para>
/// <para>
/// <b>Exec is refused today by accident, not by design.</b> Docker's exec is two
/// calls: <c>POST /containers/{id}/exec</c> creates and <c>POST /exec/{id}/start</c>
/// runs. The first matches the member rule and is proxied; only the second falls
/// to the catch-all, because it happens to sit at a different path. A proxy whose
/// safety rests on the shape of somebody else's URL scheme is not enforcing
/// anything.
/// </para>
/// <para>
/// <b>What this test does NOT cover, stated where a reader looks.</b> The create
/// body is unexamined — <c>POST /containers/create</c> filters <c>?name=</c> and
/// nothing else, so <c>HostConfig.Binds</c> or <c>Privileged</c> would pass. Stock
/// nginx cannot read a JSON body; closing that needs a different proxy or a daemon
/// authz plugin. Until then the containment is held by
/// <c>DockerPoolAdapter</c> choosing not to send those keys, which is a convention
/// and not a control. The file's own "HONEST GAP" paragraph says as much.
/// </para>
/// </remarks>
public class ScopeProxyReachTests
{
    /// <summary>One <c>location</c> block: how it matches, and whether it proxies.</summary>
    private sealed record Rule(string Matcher, bool Regex, bool Proxies);

    private static string ConfigPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            (directory ?? throw new InvalidOperationException("Gg.sln not found above the test binary"))
                .FullName,
            "scripts", "pool-proxy", "nginx.conf");
    }

    /// <summary>
    /// The routing table, in file order.
    /// </summary>
    /// <remarks>
    /// Only blocks that <c>proxy_pass</c> count as reachable; a block that returns
    /// 403 is the refusal, not a route.
    /// </remarks>
    private static IReadOnlyList<Rule> Rules()
    {
        var source = File.ReadAllText(ConfigPath());

        var rules = new List<Rule>();

        // BRACE-COUNTED, not `[^}]*`. The create block holds a nested
        // `if (...) { return 403; }`, so a body match that stopped at the first
        // close brace read it as having no proxy_pass and called a working route
        // refused. The parser has to understand one level of nesting or it
        // reports the opposite of the truth.
        foreach (Match opening in Regex.Matches(
            source, @"location\s+(?<kind>~\*?|=)?\s*(?<matcher>\S+)\s*\{"))
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

            var body = source[start..Math.Max(start, index - 1)];

            rules.Add(new Rule(
                opening.Groups["matcher"].Value,
                opening.Groups["kind"].Value.StartsWith('~'),
                body.Contains("proxy_pass", StringComparison.Ordinal)));
        }

        return rules;
    }

    /// <summary>Whether nginx would proxy this path, by first matching regex.</summary>
    private static bool Reaches(string path)
    {
        var withoutQuery = path.Split('?')[0];

        foreach (var rule in Rules())
        {
            var hit = rule.Regex
                ? Regex.IsMatch(withoutQuery, rule.Matcher)
                : string.Equals(withoutQuery, rule.Matcher, StringComparison.Ordinal);

            if (hit)
            {
                return rule.Proxies;
            }
        }

        return false;
    }

    [Test]
    public async Task The_table_really_was_read()
    {
        // THE LIVENESS ANCHOR. A parse that found no rules would make every
        // refusal assertion below vacuously true, and this file would report a
        // locked-down proxy while reading an empty list.
        var rules = Rules();

        await Assert.That(rules.Count).IsGreaterThan(3);
        await Assert.That(rules.Any(r => r.Proxies)).IsTrue();
        await Assert.That(rules.Any(r => !r.Proxies))
            .IsTrue()
            .Because("the catch-all refusal is what the scope probe is measuring.");
    }

    [Test]
    public async Task The_operations_the_adapter_actually_performs_are_reachable()
    {
        // The other anchor: tightening must not break the five calls
        // DockerPoolAdapter makes. If these stop reaching, the pool stops working
        // and the refusal tests below would still pass.
        foreach (var allowed in (string[])
                 [
                     "/v1.43/containers/json",
                     "/v1.43/containers/create?name=gg-pool-dev-1",
                     "/v1.43/containers/gg-pool-dev-1/json",
                     "/v1.43/containers/gg-pool-dev-1/start",
                     "/v1.43/containers/gg-pool-dev-1/stop",
                     "/v1.43/containers/gg-pool-dev-1",
                     "/v1.43/images/json?filters=%7B%22reference%22%3A...",
                     "/v1.43/images/127.0.0.1:5000/gg-member:1c26776e2b1f",
                 ])
        {
            await Assert.That(Reaches(allowed)).IsTrue()
                .Because($"'{allowed}' is one of the calls the adapter makes.");
        }
    }

    [Test]
    public async Task Reading_a_members_filesystem_out_is_refused()
    {
        // The one that matters once members hold a customer's repository.
        // GET copies the tree out; PUT writes into it.
        foreach (var refused in (string[])
                 [
                     "/v1.43/containers/gg-pool-dev-1/archive?path=/",
                     "/containers/gg-pool-dev-1/archive?path=/work",
                 ])
        {
            await Assert.That(Reaches(refused)).IsFalse()
                .Because("a member is about to hold customer source, and this endpoint moves "
                       + "a container's whole filesystem in and out.");
        }
    }

    [Test]
    public async Task Running_code_in_a_member_is_refused_at_the_first_call()
    {
        // Not merely at the second one. Today exec-create is proxied and only
        // exec-start falls to the catch-all, so the refusal depends on Docker
        // splitting exec across two paths rather than on this file.
        foreach (var refused in (string[])
                 [
                     "/v1.43/containers/gg-pool-dev-1/exec",
                     "/v1.43/containers/gg-pool-dev-1/attach",
                     "/exec/abc123/start",
                 ])
        {
            await Assert.That(Reaches(refused)).IsFalse()
                .Because("the proxy exists so the runner cannot do everything the daemon can, "
                       + "and running arbitrary code in a managed container is the whole of it.");
        }
    }

    [Test]
    public async Task The_surface_is_exactly_what_the_adapter_calls()
    {
        // MINIMAL BY INTENT. The old comment advertised `wait` and nothing has
        // ever called it. A future need should widen this file deliberately and
        // fail here first, rather than inherit reach nobody asked for.
        await Assert.That(Reaches("/v1.43/containers/gg-pool-dev-1/wait")).IsFalse()
            .Because("DockerPoolAdapter calls eight endpoints and this is not one of them.");
    }

    [Test]
    public async Task A_container_outside_the_pool_prefix_is_refused()
    {
        // The property the scope probe measures at every session start.
        foreach (var refused in (string[])
                 [
                     "/v1.43/containers/somebody-elses/json",
                     "/v1.43/containers/gg-scope-probe-abc/json",
                     "/v1.43/volumes",
                 ])
        {
            await Assert.That(Reaches(refused)).IsFalse();
        }
    }

    /// <summary>
    /// The reclaim widened this file, and it is admitted by the same shape the
    /// build and push are: the host's own registry, and nowhere else.
    /// </summary>
    [Test]
    public async Task An_image_outside_the_hosts_own_registry_cannot_be_removed()
    {
        foreach (var refused in (string[])
                 [
                     "/v1.43/images/sha256:7249a4ca005782263b53b7d560c1178bd7127ee0a0330",
                     "/v1.43/images/ubuntu:24.04",
                     "/v1.43/images/docker.io/library/postgres:17",
                     "/v1.43/images/prune",
                 ])
        {
            await Assert.That(Reaches(refused)).IsFalse()
                .Because("this runner may remove what this runner pushed. An id is a bare "
                       + "digest and names nothing a routing table can check, which is why "
                       + "the adapter deletes by tag rather than by id.");
        }
    }

    /// <summary>
    /// WHAT THE PATH MODEL ABOVE CANNOT SEE, asserted on the file's own text.
    /// <see cref="Reaches"/> reads location matchers and whether a block proxies;
    /// it does not evaluate <c>if</c>. Both reclaim rules carry one, and without
    /// them this file would have admitted every image on the host - so they are
    /// held here rather than left to a reader.
    /// </summary>
    [Test]
    public async Task The_reclaim_rules_carry_the_guards_the_path_model_cannot_evaluate()
    {
        var source = File.ReadAllText(ConfigPath());

        await Assert.That(source).Contains("if ($arg_filters !~")
            .Because("an unfiltered /images/json lists every image on the host; the adapter "
                   + "only ever asks for one repository's, so only that is admitted.");
        await Assert.That(source).Contains("if ($request_method != DELETE)")
            .Because("the removal rule's pattern also matches a push path, and the push rule "
                   + "only wins because it sits above it - this is the belt.");
        await Assert.That(source).Contains("if ($arg_force != \"\")")
            .Because("without force the daemon refuses an image a container is using, which is "
                   + "the guard that keeps a running member's image. It is never sent, and it "
                   + "is refused here too.");
    }
}
