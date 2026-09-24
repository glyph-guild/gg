namespace Gg.Contracts.Tests;

/// <summary>
/// An exposure's credentials pattern names where a secret is. It never carries
/// one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 14, applied one document over.</b> A fleet profile already refuses a
/// bare value in its credentials for the reason that matters here too: an
/// airspace document is git-tracked and readable by everyone who can read the
/// airspace, so a token pasted into it is a token published to the tenant.
/// </para>
/// <para>
/// <b>Refused where it is written, not where it is used.</b> A pattern that is
/// not a reference fails on the machine that was going to dial with it — a
/// member, hours later, with a diagnosis nobody reads. The document's author is
/// the only person who can fix it, and apply is the only moment they are
/// looking.
/// </para>
/// <para>
/// <b>Both schemes, because both are real here.</b> A resident runner reads
/// <c>local:</c> off its own disk. A pool member cannot: its credential store
/// starts empty and nothing can fill it, so <c>keyvault://</c> — read by the
/// identity the member inherits — is the only way one serves a preview at all.
/// </para>
/// </remarks>
public class AnExposureNamesItsSecretTests
{
    private static Exposure With(string credentials) => new()
    {
        Kind = ExposureKinds.CloudflareTunnel,
        Inventory = new ExposureInventory
        {
            Size = 8,
            Hostnames = "jdapp-{slot}.goodgrief.dev",
            Credentials = credentials,
        },
    };

    [Test]
    public async Task A_local_reference_is_accepted()
    {
        await Assert.That(Exposure.Validate(With("local:exposure/jdapp-{slot}"))).IsNull();
    }

    [Test]
    public async Task A_vault_reference_is_accepted()
    {
        await Assert.That(Exposure.Validate(
                With("keyvault://ggdev.vault.example/jdapp-{slot}")))
            .IsNull()
            .Because("it is the only scheme a pool member can resolve. Nothing delivers a "
                   + "secret to a member - its store starts empty, no field of the create body "
                   + "could carry one, and anything placed by hand dies with the container.");
    }

    [Test]
    public async Task A_bare_value_is_refused_rather_than_guessed_at()
    {
        var refusal = Exposure.Validate(With("eyJhIjoiYnJlYWNoIiwidCI6Int7c2xvdH19In0"));

        await Assert.That(refusal).IsNotNull()
            .Because("an airspace document is git-tracked and readable by everyone who can read "
                   + "the airspace, so a token pasted here is a token published to the tenant.");
        await Assert.That(refusal!).DoesNotContain("eyJhIjoiYnJlYWNoIiwidCI6Int7c2xvdH19In0")
            .Because("and the refusal must not repeat it, in case it was one - the sentence "
                   + "travels to a console, a flight log and somebody's terminal history.");
    }

    [Test]
    public async Task An_unknown_scheme_is_refused_and_the_refusal_names_what_is_available()
    {
        var refusal = Exposure.Validate(With("vault://ggdev/jdapp-{slot}"));

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("keyvault://")
            .Because("a refusal that does not name what IS available leaves somebody guessing "
                   + "at a vocabulary they cannot see.");
    }

    [Test]
    public async Task The_hostname_pattern_is_not_a_reference_and_is_left_alone()
    {
        // A HOSTNAME IS NOT A CREDENTIAL, and applying the scheme rule to it
        // would refuse every exposure ever written. The two patterns sit beside
        // each other and only one of them names a secret.
        await Assert.That(Exposure.Validate(With("local:exposure/jdapp-{slot}"))).IsNull();
    }
}
