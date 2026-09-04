using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// Which links a runner's declared hosts actually serve.
/// </summary>
/// <remarks>
/// <para>
/// <b>Contract 0.86.0 named this and left it open:</b> <i>"FlightRepos.From
/// reads only a URI's AbsolutePath and the registry matches on that path alone,
/// so a link at any host resolves to whichever registered entry shares its
/// path."</i> A link at <c>anywhere.invalid/acme/widgets</c> resolves to the
/// registered <c>acme/widgets</c> and a flight opens against it.
/// </para>
/// <para>
/// <b>It is not an oversight — it is a consequence of a boundary.</b> The
/// registry deliberately holds no host: <i>"which host a runner sends a
/// customer's credential to is a runner-side resolution; a policy store that
/// contained hosts would make credential destination a policy edit."</i> The
/// control plane cannot check a host because it must not know one.
/// </para>
/// <para>
/// <b>So it is checked HERE, where the mapping already lives.</b>
/// <c>GG_VCS_HOSTS</c> is the runner's own declaration of which provider key
/// reaches which host — the exact thing the guard says belongs runner-side —
/// and reading it is what lets a link from somewhere nobody declared be refused
/// before any source is fetched.
/// </para>
/// </remarks>
public class ALinkComesFromAServedHostTests
{
    private static IReadOnlyList<HostDeclaration> Declared(string raw) =>
        [.. HostDeclaration.ParseAll(raw, "GG_VCS_HOSTS")];

    private static HostDeclaration One(string raw) => Declared(raw)[0];

    [Test]
    public async Task A_bare_host_serves_every_link_at_that_host()
    {
        var declaration = One("forge=forge.invalid");

        await Assert.That(declaration.Serves(new Uri("https://forge.invalid/acme/widgets"))).IsTrue();
        await Assert.That(declaration.Serves(new Uri("https://forge.invalid/anything/else"))).IsTrue();
    }

    [Test]
    public async Task A_host_at_a_different_name_is_not_served()
    {
        // THE HOLE, as one line. This is the comparison nothing was making.
        var declaration = One("forge=forge.invalid");

        await Assert.That(declaration.Serves(new Uri("https://anywhere.invalid/acme/widgets")))
            .IsFalse()
            .Because("a link that merely shares a path with something registered is not a link "
                   + "to it, and acting on one fetches somebody else's repository.");
    }

    [Test]
    public async Task A_prefix_scopes_the_organisation_above_the_path()
    {
        // The spelling GG_VCS_HOSTS already uses, and the reason it exists: a
        // forge that puts an organisation above the repository path serves two
        // different tenants' repositories from one host.
        var declaration = One("forge=forge.invalid/acme");

        await Assert.That(declaration.Serves(new Uri("https://forge.invalid/acme/widgets/x"))).IsTrue();
        await Assert.That(declaration.Serves(new Uri("https://forge.invalid/other/widgets/x")))
            .IsFalse();
    }

    [Test]
    public async Task A_host_is_compared_without_regard_to_case()
    {
        // Hosts are case-insensitive, and so is the organisation segment on
        // every forge this has met. A REFUSAL that fired on a capital letter
        // would be one nobody could act on, and being too strict is the
        // dangerous direction for a refusal.
        var declaration = One("forge=Forge.Invalid/Acme");

        await Assert.That(declaration.Serves(new Uri("https://forge.invalid/acme/widgets"))).IsTrue();
    }

    [Test]
    public async Task The_suffixes_a_declaration_carries_do_not_change_what_it_serves()
    {
        // !pathscoped and !nopr describe how a forge SPELLS things. They are
        // stripped before the host is compared, or a deployment that declared
        // one would silently serve nothing.
        var declaration = One("forge=forge.invalid/acme!pathscoped");

        await Assert.That(declaration.Serves(new Uri("https://forge.invalid/acme/widgets"))).IsTrue();
    }

    // ---- what the runner does with it ----

    [Test]
    public async Task A_link_from_a_host_the_provider_serves_is_not_refused()
    {
        await Assert.That(HostDeclaration.Unserved(
                "forge", "https://forge.invalid/acme/widgets/pull/1",
                Declared("forge=forge.invalid/acme")))
            .IsNull();
    }

    [Test]
    public async Task A_link_from_a_host_the_provider_does_not_serve_is_refused_by_name()
    {
        var why = HostDeclaration.Unserved(
            "forge", "https://anywhere.invalid/acme/widgets/pull/1",
            Declared("forge=forge.invalid/acme"));

        await Assert.That(why).IsNotNull();
        await Assert.That(why!).Contains("anywhere.invalid");
        await Assert.That(why).Contains("forge")
            .Because("naming the host and the provider is the difference between a diagnosis and "
                   + "a refusal somebody has to go and reproduce.");
    }

    [Test]
    public async Task A_provider_this_runner_declares_nothing_for_is_not_refused_here()
    {
        // ABSENCE IS NOT A MISMATCH. A provider with no declaration is a
        // capability gap the vcs adapter reports in its own words; refusing it
        // here would be a second, worse copy of that message - and would ground
        // flights on a runner that had simply not been told about a forge.
        await Assert.That(HostDeclaration.Unserved(
                "another", "https://forge.invalid/acme/widgets",
                Declared("forge=forge.invalid/acme")))
            .IsNull();
    }

    [Test]
    public async Task A_flight_with_no_link_is_never_refused_for_where_it_came_from()
    {
        // A ticket names a provider and an id and no link; a sentence names
        // nothing at all. Neither has a host to check.
        await Assert.That(HostDeclaration.Unserved("forge", null, Declared("forge=forge.invalid")))
            .IsNull();
        await Assert.That(HostDeclaration.Unserved("forge", "not a uri", Declared("forge=forge.invalid")))
            .IsNull();
    }

    // ---- and which tracker can read a link ----

    [Test]
    public async Task The_provider_serving_a_link_is_the_one_that_declared_its_host()
    {
        // This is what gives a uri work-item flight a tool: the reader is keyed
        // on a provider, and a link carries none until this answers.
        await Assert.That(HostDeclaration.ProviderFor(
                "https://forge.invalid/acme/_workitems/edit/18120",
                Declared("forge=forge.invalid/acme,other=other.invalid")))
            .IsEqualTo("forge");
    }

    [Test]
    public async Task Two_declarations_serving_one_link_answer_nothing()
    {
        // Article XI. Two providers answering to one link is a configuration
        // question for a person, and picking one would be the guess this whole
        // design exists to avoid.
        await Assert.That(HostDeclaration.ProviderFor(
                "https://forge.invalid/acme/widgets",
                Declared("one=forge.invalid,two=forge.invalid/acme")))
            .IsNull();
    }

    [Test]
    public async Task A_link_nobody_declared_a_host_for_answers_nothing()
    {
        await Assert.That(HostDeclaration.ProviderFor(
                "https://anywhere.invalid/x/y", Declared("forge=forge.invalid")))
            .IsNull();
    }
}
