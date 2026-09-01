using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A declaration selects the adapters, and production is what reads it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two adapters were written for a forge that spells repositories
/// differently, tested, documented — and could never be reached.</b>
/// <see cref="PathScopedGitVcsAdapter"/> and
/// <see cref="RefNamedDestinationAdapter"/> exist because the first adapter of
/// each pair recorded that its path shapes were <i>one convention</i>. The
/// <c>adapterFor</c> parameter was then added to both <c>FromEnvironment</c>
/// methods so such an adapter <i>could be dispatched to and never registered</i>
/// — and it has only ever been passed from a test. <c>Gg.Cli</c> calls both
/// methods without it, so every runner anybody ran built the other pair.
/// </para>
/// <para>
/// <b>So the selection belongs in the DEFAULT, not in the caller.</b> Making
/// <c>Gg.Cli</c> choose would put forge knowledge in the binary, which is the
/// one thing these classes say must not happen: <i>no provider is named in this
/// binary; the class is named for a shape, the key comes from configuration</i>.
/// The seam stays exactly what its own documentation says it is — a way for a
/// test to substitute — and the environment decides which shape a runner serves.
/// </para>
/// </remarks>
public class DeclaredConventionTests
{
    private static IVcsAdapter Reading(string hosts) =>
        VcsConfiguration.FromEnvironment(hosts).Single();

    private static IDestinationAdapter Landing(string hosts) =>
        DestinationConfiguration.FromEnvironment(
            api => new HttpClient { BaseAddress = new Uri(api) },
            apis: "forge=https://api.example.com/",
            hosts: hosts).Single();

    // ---- what the declaration now buys ----

    [Test]
    public async Task A_path_scoped_declaration_selects_the_read_adapter_for_that_shape()
    {
        await Assert.That(Reading("forge=forge.example.com/an-org!pathscoped"))
            .IsTypeOf<PathScopedGitVcsAdapter>()
            .Because("the adapter existed, was tested, and could be reached only from a test - "
                   + "which is registered-is-not-invoked on the class written to fix exactly that.");
    }

    [Test]
    public async Task The_same_declaration_selects_the_landing_adapter_for_that_shape()
    {
        await Assert.That(Landing("forge=forge.example.com/an-org!pathscoped"))
            .IsTypeOf<RefNamedDestinationAdapter>()
            .Because("one forge's spelling is one declaration. A deployment that had to state the "
                   + "same fact twice would eventually state it once.");
    }

    [Test]
    public async Task The_url_it_builds_is_the_one_that_forge_takes()
    {
        // The difference the whole pair exists for, asserted through the public
        // method the adapter exposes on purpose - `a difference asserted through
        // a private path is one somebody has to take on trust`.
        var adapter = (PathScopedGitVcsAdapter)Reading("forge=forge.example.com/an-org!pathscoped");

        var target = new RepoTarget
        {
            Provider = "forge",
            Slug = "a-project/a-repository",
            PinnedRef = "refs/heads/main",
        };

        await Assert.That(adapter.CloneUrlFor(target))
            .IsEqualTo("https://forge.example.com/an-org/a-project/_git/a-repository")
            .Because("the organisation is deployment knowledge and lives in the host; a flight "
                   + "names <project>/<repository> and nothing more.");
    }

    // ---- and what it must not cost ----

    [Test]
    public async Task A_runner_told_nothing_gets_exactly_what_it_always_did()
    {
        await Assert.That(Reading("forge=forge.example.com")).IsTypeOf<HttpsGitVcsAdapter>();
        await Assert.That(Landing("forge=forge.example.com")).IsTypeOf<HttpsDestinationAdapter>();
    }

    [Test]
    public async Task The_older_suffix_still_selects_the_older_pair()
    {
        // `!nopr` is a capability, not a spelling. It must not drift into
        // selecting a different adapter now that a sibling suffix does.
        await Assert.That(Reading("forge=forge.example.com!nopr")).IsTypeOf<HttpsGitVcsAdapter>();
        await Assert.That(Landing("forge=forge.example.com!nopr")).IsTypeOf<HttpsDestinationAdapter>();
    }

    [Test]
    public async Task The_local_provider_is_still_answered_before_any_convention()
    {
        // A filesystem root is not a host and no suffix changes that. If a
        // convention ever took precedence here, a runner configured for a bare
        // repository on disk would start talking to a forge.
        await Assert.That(VcsConfiguration.FromEnvironment("local=/tmp/roots!pathscoped").Single())
            .IsTypeOf<LocalVcsAdapter>();
    }

    [Test]
    public async Task An_explicit_factory_still_wins_over_the_default()
    {
        // The seam is what its documentation says it is. Moving selection into
        // the default must not quietly disable substitution, or every test that
        // plants an adapter starts asserting the default instead.
        var adapters = VcsConfiguration.FromEnvironment(
            "forge=forge.example.com!pathscoped",
            adapterFor: (provider, host, capabilities) =>
                new HttpsGitVcsAdapter(provider, host, capabilities));

        await Assert.That(adapters.Single()).IsTypeOf<HttpsGitVcsAdapter>();
    }
}
