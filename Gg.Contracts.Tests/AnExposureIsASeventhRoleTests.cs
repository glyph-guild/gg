using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// An exposure is a declarable role: where a flight's served port may appear,
/// declared by the tenant and owned by the tenant.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0027.</b> GG-268, the first <c>ui-preview</c> flight, published its
/// preview by downloading a tunnel client, opening a quick tunnel and putting
/// the resulting URL in prose. Every part of that is improvised, and two parts
/// cannot be fixed by improvising better: the hostname is random and changes
/// every run, so no identity provider can ever be told to expect it; and no
/// document declares the address, so nothing governs it and nothing releases it.
/// </para>
/// <para>
/// <b>An exposure is the tenant's, and ours is only the first one.</b> The role
/// exists so that the address a preview appears at is declared where every other
/// tenant decision is declared, against a credential the tenant owns, on a domain
/// the tenant owns. There is deliberately no default: a tenant that has declared
/// no exposure cannot publish a preview and is told which document to write. A
/// built-in fallback would be the hosted product arriving by the back door, and
/// the path everybody takes is the only path that stays correct.
/// </para>
/// <para>
/// <b>A seventh role costs forty-two places and ten of them are silent</b>,
/// measured before this was written by tracing <c>fleet-profile</c> and
/// cross-checking <c>watch</c>. This file is the first of them, and it is
/// deliberately the smallest: the vocabulary, the directory, and the round trip.
/// The count guards in <c>StrategyContainmentTests</c> and
/// <c>EnvelopeLayerTests</c> argue next, as they have for every role since the
/// fourth, and <c>EstateRenderTests</c> will then refuse a role it cannot render.
/// </para>
/// <para>
/// <b>The noun.</b> <c>exposure</c> rather than <c>preview</c>, which is already
/// taken by the work kind that needs one, and rather than <c>tunnel</c>, which
/// names one provider's mechanism rather than the thing being declared. What the
/// document says is where something running privately becomes reachable — the
/// exposure — and a Cloudflare tunnel is one way to arrange that.
/// </para>
/// </remarks>
public class AnExposureIsASeventhRoleTests
{
    [Test]
    public async Task The_vocabulary_knows_it()
    {
        await Assert.That(Roles.All).Contains(Roles.Exposure);

        await Assert.That(Roles.Exposure).IsEqualTo("exposure")
            .Because("the ADR's own noun, spelled as it spells it. A role whose word differs "
                   + "from the document that describes it is two vocabularies.");
    }

    [Test]
    public async Task It_renders_into_its_own_directory()
    {
        await Assert.That(AirspaceNames.PathFor(Roles.Exposure, "jdapp"))
            .IsEqualTo("exposures/jdapp.yaml")
            .Because("a role with no rendering is a document pull cannot write, which is the "
                   + "estate silently missing a class of policy.");
    }

    [Test]
    public async Task A_path_in_that_directory_names_an_exposure()
    {
        var named = AirspaceNames.NameFrom("exposures/jdapp.yaml");

        await Assert.That(named).IsNotNull();
        await Assert.That(named!.Value.Role).IsEqualTo(Roles.Exposure);
        await Assert.That(named.Value.Name).IsEqualTo("jdapp");

        await Assert.That(AirspaceNames.RoleOfDirectory("exposures/jdapp.yaml"))
            .IsEqualTo(Roles.Exposure)
            .Because("the location decides the rules a document is read by - which is what "
                   + "catches an exposure copied into `strategies/`, a legal document of the "
                   + "wrong type that would otherwise parse and validate.");
    }

    [Test]
    public async Task The_round_trip_holds_for_every_role_including_this_one()
    {
        // DISCOVERED FROM THE VOCABULARY rather than listed, so an eighth role is
        // held to this too without anybody remembering to add it.
        foreach (var role in Roles.All)
        {
            var name = role == Roles.Root ? "root" : "a-name";
            var path = AirspaceNames.PathFor(role, name);

            await Assert.That(AirspaceNames.NameFrom(path)!.Value.Role).IsEqualTo(role)
                .Because($"'{role}' renders to '{path}', so that path has to read back as it");
        }
    }
}
