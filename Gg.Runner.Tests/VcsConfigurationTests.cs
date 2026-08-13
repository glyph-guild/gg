using Gg.Contracts;
using Gg.Runner;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// Which forges this runner serves is configuration, not code.
/// </summary>
/// <remarks>
/// <para>
/// gg is public and distributed, and no forge is named in it. Which one a
/// tenant uses is the control plane's knowledge: the lease carries a provider
/// key, the deployment maps that key to a host, and the adapter speaks git to
/// it. A second forge arrives without this binary changing, which is the same
/// property the identity port has and for the same reason.
/// </para>
/// <para>
/// The consequence worth testing is what happens when the map has no entry: a
/// declared capability gap, named before anything is fetched, rather than a
/// clone that fails at DNS with a message about a host nobody configured.
/// </para>
/// </remarks>
public class VcsConfigurationTests
{
    [Test]
    public async Task A_configured_provider_becomes_an_adapter_for_that_key()
    {
        var adapters = VcsConfiguration.FromEnvironment("forge=forge.example.invalid");

        await Assert.That(adapters.Select(a => a.Provider)).IsEquivalentTo((string[])["forge"]);
        await Assert.That(adapters[0].Capabilities.PullRequestHeadsFromBase).IsTrue();
    }

    [Test]
    public async Task A_forge_that_does_not_publish_pull_request_heads_says_so()
    {
        // The second adapter, arriving without a code change. Its refusal is a
        // declared capability rather than a discovery made at fetch time.
        var adapters = VcsConfiguration.FromEnvironment(
            $"other=other.example.invalid{VcsConfiguration.NoPullRequestHeads}");

        await Assert.That(adapters[0].Capabilities.PullRequestHeadsFromBase).IsFalse();

        var resolution = adapters[0].Resolve("refs/pull/7/head");
        var gap = resolution as RefResolution.Unsupported;

        await Assert.That(gap).IsNotNull();
        await Assert.That(gap!.Capability).IsEqualTo(nameof(VcsCapabilities.PullRequestHeadsFromBase));
        await Assert.That(gap.Diagnosis).Contains("credential")
            .Because("the reason it cannot is that the alternative needs one for the fork, which "
                   + "this design never asks for.");
    }

    [Test]
    public async Task Several_forges_can_be_configured_at_once()
    {
        var adapters = VcsConfiguration.FromEnvironment(
            "one=one.example.invalid, two=two.example.invalid");

        await Assert.That(adapters.Select(a => a.Provider)).IsEquivalentTo((string[])["one", "two"]);
    }

    [Test]
    public async Task No_configuration_means_no_provider_rather_than_a_default()
    {
        // A default forge would be this binary naming one, which is the thing
        // it must not do - and it would be the wrong one for anybody running a
        // self-hosted instance.
        await Assert.That(VcsConfiguration.FromEnvironment("")).IsEmpty();
    }

    [Test]
    public async Task A_malformed_entry_halts_rather_than_being_skipped()
    {
        // Article XI. A runner that quietly served fewer forges than somebody
        // configured would fail on one flight, much later, for a reason nothing
        // connects back to a typo in a variable.
        foreach (var malformed in (string[])["forge", "=host", "forge=", "forge:host"])
        {
            await Assert.That(() => VcsConfiguration.FromEnvironment(malformed))
                .Throws<InvalidOperationException>()
                .Because($"'{malformed}' is not key=host.");
        }
    }

    [Test]
    public async Task A_provider_this_runner_does_not_serve_is_a_capability_gap()
    {
        // The whole point of the configuration being explicit. The flight named
        // a forge; this runner has no host for it; that is answerable, and a
        // DNS failure is not.
        using var trees = new ScratchTreeRoot();
        var workspace = trees.Workspace(VcsConfiguration.FromEnvironment("forge=forge.example.invalid"));

        var refusal = await Assert.That(async () => await workspace.PrepareAsync(
                "flight-1",
                [new LeaseRepoRef { Provider = "somewhere-else", Slug = "acme/widgets", PinnedRef = "refs/heads/main" }],
                new Dictionary<string, string>()))
            .Throws<VcsCapabilityException>();

        await Assert.That(refusal!.Message).Contains("somewhere-else");
        await Assert.That(refusal.Message).Contains(VcsConfiguration.HostsVariable)
            .Because("a diagnosis that does not name the knob is one somebody has to go looking for.");
    }

    [Test]
    public async Task The_https_adapter_refuses_a_bare_commit_rather_than_trying_it()
    {
        // Servers are not obliged to serve an arbitrary sha, and finding out at
        // fetch time turns a declared limitation into a network error.
        var adapters = VcsConfiguration.FromEnvironment("forge=forge.example.invalid");

        await Assert.That(adapters[0].Resolve(new string('a', 40))).IsTypeOf<RefResolution.Unsupported>();
    }

    [Test]
    public async Task No_forge_is_named_in_the_binary_at_all()
    {
        // The property this whole arrangement exists for, and the one the
        // configuration is the price of. Asserted over what the adapters
        // actually declare rather than over the source, because the source scan
        // is a separate test in the contract suite and this one is about
        // whether the defaults are empty.
        await Assert.That(VcsConfiguration.FromEnvironment("")).IsEmpty();
        await Assert.That(new NoWorkspace().SweepOrphans()).IsEqualTo(0);
    }
}
