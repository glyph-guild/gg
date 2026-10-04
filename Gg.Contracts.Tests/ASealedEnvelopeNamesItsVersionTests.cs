namespace Gg.Contracts.Tests;

/// <summary>
/// A sealed credential says which version of the format it is, and one this
/// build does not know is refused by name rather than misread.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice fifty-nine, rule 1.</b> A format that cannot say which version it
/// is, is one that can never be fixed — every later change would have to be
/// guessed at from the bytes, and a guess that is wrong decrypts garbage rather
/// than refusing. The version is a member from the first row written, before
/// there is a second version to need it.
/// </para>
/// <para>
/// <b>Named rather than merely refused.</b> A credential sealed by a newer gg
/// and opened by an older one is a real situation on a fleet that updates in
/// its own time, and the person who meets it can act on *"sealed under version
/// 2, and this gg knows 1"* in a way they cannot act on *"could not open"*. It
/// is the same reason <c>CredentialReference.Validate</c> answers a sentence
/// rather than a bool.
/// </para>
/// <para>
/// <b>The version is not a secret and may be said.</b> Rule 8 forbids repeating
/// the INPUT — the ciphertext, the wrapped key — and a format number is neither.
/// </para>
/// </remarks>
public class ASealedEnvelopeNamesItsVersionTests
{
    private static SealedCredential At(int version) => new()
    {
        Version = version,
        Ciphertext = "Zm9v",
        Wrapped = [new WrappedContentKey { Holder = "a-holder", Wrapped = "YmFy" }],
    };

    [Test]
    public async Task The_current_version_is_accepted()
    {
        await Assert.That(SealedCredential.Validate(At(SealedCredentialVersions.Current))).IsNull();
    }

    [Test]
    public async Task A_version_this_build_does_not_know_is_refused()
    {
        await Assert.That(SealedCredential.Validate(At(SealedCredentialVersions.Current + 1)))
            .IsNotNull();
    }

    [Test]
    public async Task The_refusal_names_the_version_it_found_and_the_one_it_knows()
    {
        var refused = SealedCredential.Validate(At(7));

        await Assert.That(refused).Contains("7")
            .Because("the person meeting this has to know what they are holding.");

        await Assert.That(refused).Contains(SealedCredentialVersions.Current.ToString())
            .Because("and what this gg can open, which is the half that tells them to update.");
    }

    [Test]
    public async Task The_refusal_never_repeats_the_envelope()
    {
        // RULE 8. A refusal about a version is one of the few places the whole
        // envelope is in scope and in reach, and an error that helpfully echoed
        // what it could not open would print ciphertext into a console, a flight
        // log and a shell history at once.
        var refused = SealedCredential.Validate(new SealedCredential
        {
            Version = 9,
            Ciphertext = "c3VwZXItc2VjcmV0LWNpcGhlcnRleHQ",
            Wrapped = [new WrappedContentKey { Holder = "a-holder", Wrapped = "d3JhcHBlZC1rZXk" }],
        });

        await Assert.That(refused).DoesNotContain("c3VwZXItc2VjcmV0LWNpcGhlcnRleHQ");
        await Assert.That(refused).DoesNotContain("d3JhcHBlZC1rZXk");
    }

    [Test]
    public async Task A_version_below_the_first_is_refused_rather_than_treated_as_absent()
    {
        // ZERO IS WHAT AN UNSET INT LOOKS LIKE, which is exactly the value a
        // document or a deserializer produces when the member never arrived.
        // Accepting it would make "no version" mean "version one" forever.
        await Assert.That(SealedCredential.Validate(At(0))).IsNotNull();
    }

    [Test]
    public async Task Every_version_this_build_knows_validates()
    {
        foreach (var version in SealedCredentialVersions.All)
        {
            await Assert.That(SealedCredential.Validate(At(version))).IsNull()
                .Because($"version {version} is declared as known and must therefore open.");
        }
    }
}
