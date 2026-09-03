namespace Gg.Contracts.Tests;

using Gg.Contracts.Description;

/// <summary>
/// The repository registry's wire surface: a name, a provider key, the
/// forge's immutable id, a display path - no credential, no host.
/// </summary>
/// <remarks>
/// <para>
/// <b>Airspace names a repository and stops.</b> Which host a runner sends a
/// customer's credential to is a runner-side resolution; a policy store that
/// contained hosts would make credential destination a policy edit. The
/// provider is a KEY the registrar chose - the runner maps it to a host of
/// its own - and the id is the forge's immutable identifier, because the
/// display path is a label that may drift.
/// </para>
/// <para>
/// <b>Registration is what makes a repository nameable at all</b>, so it
/// widens what every envelope layer beneath can reach - v0 unrestricted and
/// logged, the chart's shape, with the widening-owner question staying where
/// ADR-0016 put it.
/// </para>
/// </remarks>
public class RepositoryRegistrySurfaceTests
{
    [Test]
    public async Task An_entry_is_a_name_a_provider_key_an_immutable_id_and_a_display_path()
    {
        var registered = new RepositoryRegistered
        {
            Name = "payments",
            Provider = "forge.example",
            Id = "F_a1b2c3d4",
            Path = "acme/payments-service",
            Credential = RepositoryCredentialModes.Required,
            RegisteredBy = "kevin",
            RegisteredAt = DateTimeOffset.UnixEpoch,
        };

        await Assert.That(registered.Id).IsEqualTo("F_a1b2c3d4")
            .Because("the id is the forge's immutable identifier; the path is a label that "
                   + "may drift, kept for people.");
        await Assert.That(typeof(RepositoryRegistered).GetProperties().Select(p => p.Name))
            .DoesNotContain("Host")
            .Because("no credential and no host, by design - which host a credential goes "
                   + "to is the runner's resolution, never a policy edit here.");
    }

    [Test]
    public async Task The_wire_declares_the_registry_routes_beside_the_topology()
    {
        await Assert.That(ProtocolSurface.Endpoints.Any(e =>
            e.Method == "POST" && e.Path == "/v1/airspace/repositories")).IsTrue();
        await Assert.That(ProtocolSurface.Endpoints.Any(e =>
            e.Method == "GET" && e.Path == "/v1/airspace/repositories")).IsTrue()
            .Because("both live under /v1/airspace, which is already governed - an undeclared "
                   + "route under it would be an unaudited way to widen what envelopes reach.");

        // `narrowings` joined both in 0.74.0: the directory a repository declares
        // its narrowings under (ADR-0018). Pinned by an exact list rather than a
        // Contains, deliberately - an undeclared member is one the control plane
        // serializes and conformance refuses, and a declared member it does not
        // have is a name it is required to emit and cannot.
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RegisterRepositoryRequest)])
            .IsEquivalentTo((string[])["name", "provider", "id", "path", "credential", "ref", "narrowings"]);
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RepositoryRegistered)])
            .IsEquivalentTo((string[])
            [
                "name", "provider", "id", "path", "credential", "ref", "narrowings",
                "registeredBy", "registeredAt",
            ]);
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RegisteredRepositories)])
            .IsEquivalentTo((string[])["repositories"]);
    }
}
