using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// One declaration, read the same way by both sides of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>`GG_VCS_HOSTS` is parsed twice, by two methods that do not share a
/// line.</b> <see cref="VcsConfiguration.FromEnvironment"/> reads it to build
/// read adapters, and <see cref="DestinationConfiguration"/> reads the same
/// variable to find the git host a proposal's branch went to. Each strips
/// <c>!nopr</c> with its own copy of the same three lines.
/// </para>
/// <para>
/// <b>That duplication is a defect waiting for its second suffix.</b> A suffix
/// added to one and not the other does not fail where it was added: reading
/// keeps working, every test about reading passes, and the first PUSH goes to a
/// url with <c>!something</c> inside it. So these assertions are written on the
/// landing side first, which is the side that would have shipped it.
/// </para>
/// <para>
/// A suffix declares how a forge differs from the default convention. It never
/// names a forge — the key comes from configuration and the class is named for a
/// shape, which is the rule <see cref="PathScopedGitVcsAdapter"/> states for
/// itself and this file inherits.
/// </para>
/// </remarks>
public class HostDeclarationTests
{
    /// <summary>Builds the destination side, reporting the host the factory was handed.</summary>
    private static string? HostHandedToTheLandingSide(string hosts)
    {
        string? captured = null;

        DestinationConfiguration.FromEnvironment(
            api => new HttpClient { BaseAddress = new Uri(api) },
            apis: "forge=https://api.example.com/",
            hosts: hosts,
            adapterFor: (provider, host, client) =>
            {
                captured = host;
                return new HttpsDestinationAdapter(provider, host, client);
            });

        return captured;
    }

    /// <summary>Builds the read side, reporting the host the factory was handed.</summary>
    private static string? HostHandedToTheReadingSide(string hosts)
    {
        string? captured = null;

        VcsConfiguration.FromEnvironment(
            hosts,
            adapterFor: (provider, host, capabilities) =>
            {
                captured = host;
                return new HttpsGitVcsAdapter(provider, host, capabilities);
            });

        return captured;
    }

    [Test]
    public async Task The_landing_side_strips_a_suffix_the_reading_side_knows()
    {
        // THE SIDE THAT WOULD HAVE SHIPPED IT. A push composes its url from
        // this host, so a suffix left in place here is a request to
        // `https://forge.example.com!pathscoped/...` - which fails at DNS, on a
        // flight, long after the variable was written.
        await Assert.That(HostHandedToTheLandingSide("forge=forge.example.com!pathscoped"))
            .IsEqualTo("forge.example.com")
            .Because("both sides read GG_VCS_HOSTS, so a suffix either side understands has to be "
                   + "understood by both - and today each keeps its own copy of the stripping.");
    }

    [Test]
    public async Task The_reading_side_strips_it_too()
    {
        await Assert.That(HostHandedToTheReadingSide("forge=forge.example.com!pathscoped"))
            .IsEqualTo("forge.example.com");
    }

    [Test]
    public async Task The_suffix_that_already_existed_still_works_on_both_sides()
    {
        // The regression this refactor most threatens: `!nopr` is the shipped
        // suffix and every configured runner that declares one depends on it.
        await Assert.That(HostHandedToTheReadingSide("forge=forge.example.com!nopr"))
            .IsEqualTo("forge.example.com");
        await Assert.That(HostHandedToTheLandingSide("forge=forge.example.com!nopr"))
            .IsEqualTo("forge.example.com");
    }

    [Test]
    public async Task Two_suffixes_are_stripped_whichever_order_they_are_written_in()
    {
        // Redundant together - a path-scoped forge declares no base heads of its
        // own accord - but a deployment that writes both should not get a url
        // with half a suffix in it, and neither order is more correct than the
        // other to somebody typing a variable.
        await Assert.That(HostHandedToTheLandingSide("forge=forge.example.com!nopr!pathscoped"))
            .IsEqualTo("forge.example.com");
        await Assert.That(HostHandedToTheLandingSide("forge=forge.example.com!pathscoped!nopr"))
            .IsEqualTo("forge.example.com");
    }

    [Test]
    public async Task A_host_that_merely_contains_a_bang_is_left_alone()
    {
        // Only a RECOGNISED suffix is stripped. Trimming any trailing `!…` would
        // quietly rewrite a host nobody meant as a declaration.
        await Assert.That(HostHandedToTheReadingSide("forge=forge.example.com!wat"))
            .IsEqualTo("forge.example.com!wat")
            .Because("an unrecognised suffix is part of the host, not a flag - and a host that "
                   + "does not resolve is a better failure than a flag silently ignored.");
    }
}
