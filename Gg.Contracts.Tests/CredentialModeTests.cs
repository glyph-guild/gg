namespace Gg.Contracts.Tests;

using Gg.Contracts.Description;

/// <summary>
/// A registration may declare that its repository authenticates with nothing,
/// and absence of the declaration still demands a credential.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found by a walk, 2026-08-24:</b> the claim demands a credential
/// reference for every repository on a flight, the runner refuses an empty
/// secret, and the local adapter refuses any secret - so a repository a
/// runner reaches over <c>file://</c> (an air-gapped mirror, a bind-mounted
/// checkout) could not be flown through the product path at all. Declared,
/// not flyable.
/// </para>
/// <para>
/// <b>The tenant declares it; the control plane stays ignorant of keys.</b>
/// The registry deliberately does not know what any provider key means - a
/// key is the registrar's word and the runner's resolution. So the exemption
/// is not "the control plane learns <c>local</c>": it is one more thing the
/// REGISTRAR asserts about the repository, attributed like the rest of the
/// registration. And <b>absence means required</b>, the same rule as the
/// unadmitted push: a registration written before this member existed means
/// exactly what it meant.
/// </para>
/// </remarks>
public class CredentialModeTests
{
    [Test]
    public async Task The_modes_are_a_closed_vocabulary_of_two()
    {
        await Assert.That(RepositoryCredentialModes.All)
            .IsEquivalentTo((string[])[
                RepositoryCredentialModes.Required, RepositoryCredentialModes.None])
            .Because("required and none, and nothing else - a third disposition is a "
                   + "contract version, not a string.");
        await Assert.That(RepositoryCredentialModes.Required).IsEqualTo("required");
        await Assert.That(RepositoryCredentialModes.None).IsEqualTo("none");
    }

    [Test]
    public async Task A_registration_may_declare_the_mode_and_absence_means_required()
    {
        var request = new RegisterRepositoryRequest
        {
            Name = "mirror",
            Provider = "local",
            Id = "F_mirror01",
            Path = "acme/mirror",
        };

        await Assert.That(request.Credential).IsNull()
            .Because("a registration written before this member existed means exactly what "
                   + "it meant: a credential is demanded.");

        await Assert.That(ProtocolSurface.JsonMembers[typeof(RegisterRepositoryRequest)])
            .Contains("credential")
            .Because("the member is declared on the wire, so both repos hold the same shape.");
    }

    [Test]
    public async Task The_registered_echo_says_the_resolved_mode_out_loud()
    {
        // REQUIRED ON THE ECHO, nullable on the request. The reader of a
        // registration must not have to know the defaulting rule to know what
        // was registered - an absent declaration and a declared "required"
        // must read the same on the way OUT, because they are the same fact.
        var registered = new RepositoryRegistered
        {
            Name = "mirror",
            Provider = "local",
            Id = "F_mirror01",
            Path = "acme/mirror",
            Credential = RepositoryCredentialModes.None,
            RegisteredBy = "kevin",
            RegisteredAt = DateTimeOffset.UnixEpoch,
        };

        await Assert.That(registered.Credential).IsEqualTo("none");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RepositoryRegistered)])
            .Contains("credential");
    }
}
