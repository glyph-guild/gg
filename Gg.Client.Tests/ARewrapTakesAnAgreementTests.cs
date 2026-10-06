using System.Reflection;
using System.Security.Cryptography;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A rewrap is performed through an agreement, so the thing performing it does
/// not have to be a key this process can hold.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.1-01, and the row the whole slice turns on.</b> ADR-0037 Decision 2
/// says a person's key is what MOVES a credential. It is minted, it is
/// registered, and nothing calls it — <c>PersonKey.Unlock</c>,
/// <c>AgreeWith</c> and <c>PublicHalfOf</c> have no caller outside their own
/// tests — because the push path cannot reach it. That is not an oversight
/// anybody made; it is two type signatures that were designed a step apart.
/// </para>
/// <para>
/// <b>The two seams, and why they cannot meet.</b>
/// <c>CredentialSeal.Rewrap(SealedCredential, ECDiffieHellman, string)</c>
/// demands the key. <c>PersonKey</c> refuses to give one up, and
/// <c>APersonsKeyNeverLeavesItsAdapterTests</c> enforces that refusal over the
/// whole public surface, for a reason worth keeping: .NET cannot construct an
/// <see cref="ECDiffieHellman"/> over a key it does not hold, so a signature
/// demanding one closes the door on a PIV-backed key forever. Slice fifty-nine
/// reached for the machine key instead, which is how a push ended up costing
/// nobody anything.
/// </para>
/// <para>
/// <b>The ADR called this exact failure, in the slice that built it:</b> <i>"the
/// thing that keeps it possible costs one type signature today and is painful to
/// reopen later ... The adapter must own the agreement and return the derived
/// bytes, not the key."</i> <c>Rewrap</c> is that type, it is in
/// <c>Gg.Contracts</c>, and this is the step that reopens it while it is still
/// cheap.
/// </para>
/// <para>
/// <b>The fix is not to weaken the adapter.</b> If anything here needed an
/// <see cref="ECDiffieHellman"/> out of <c>PersonKey</c>, the change would be
/// the wrong one — the ratchet is not amended, widened or exempted, and the
/// first test below says so by asserting it still holds.
/// </para>
/// </remarks>
public class ARewrapTakesAnAgreementTests
{
    private const string Passphrase = "correct horse battery staple";
    private const string Value = "a-token-nobody-should-see";

    private static string APath() =>
        Path.Combine(Path.GetTempPath(), "gg-rewrap-" + Guid.NewGuid().ToString("N"), "person-key");

    private static PersonKey AnUnlockedKey()
    {
        var path = APath();
        PersonKey.Create(path, Passphrase);
        return PersonKey.Unlock(path, Passphrase);
    }

    private static string PublicHalf(ECDiffieHellman key) =>
        Convert.ToBase64String(key.PublicKey.ExportSubjectPublicKeyInfo());

    [Test]
    public async Task The_adapter_still_refuses_to_hand_back_its_key()
    {
        // ASSERTED HERE AS WELL AS IN ITS OWN FILE, because this is the test
        // most likely to be made to pass the wrong way. The obvious shortcut is
        // to give PersonKey an accessor and keep Rewrap's signature; that would
        // turn this file green and close hardware at the same time.
        var offenders = typeof(PersonKey)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(member => (member.Name, Type: member switch
            {
                MethodInfo method => method.ReturnType,
                PropertyInfo property => property.PropertyType,
                FieldInfo field => field.FieldType,
                _ => typeof(void),
            }))
            .Where(member => typeof(ECDiffieHellman).IsAssignableFrom(member.Type))
            .Select(member => member.Name)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("a person's key performs the agreement and hands back bytes. If this is "
                   + "empty and the rest of this file fails, the signature is what is wrong.");
    }

    [Test]
    public async Task No_rewrap_demands_a_key_at_all()
    {
        // THE STRUCTURAL HALF. A second overload taking an ECDiffieHellman would
        // leave the old path in place beside the new one, and the old path is
        // the one every existing caller already takes - so it would be built,
        // merged, and still never reached by a person's key.
        var demanding = typeof(CredentialSeal)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(CredentialSeal.Rewrap))
            .Where(method => method.GetParameters()
                .Any(parameter => typeof(ECDiffieHellman).IsAssignableFrom(parameter.ParameterType)))
            .Select(method => method.ToString())
            .ToList();

        await Assert.That(demanding).IsEmpty()
            .Because("a rewrap unwraps thirty-two bytes and wraps them again; doing that needs an "
                   + "agreement, and demanding the key is what shut a person's key out of it.");
    }

    [Test]
    public async Task A_person_rewraps_a_credential_to_a_machine()
    {
        // THE HALF THAT IS THE POINT, and the one a signature change exists for:
        // the person holds the credential, the runner does not, and the person
        // is what moves it.
        var key = AnUnlockedKey();
        using var runner = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var mine = CredentialSeal.Seal(Value, [key.PublicKey]);
        var theirs = CredentialSeal.Rewrap(mine, key, PublicHalf(runner));

        await Assert.That(CredentialSeal.Open(theirs, runner)).IsEqualTo(Value)
            .Because("the machine it was pushed to is the one that opens it, which is the whole "
                   + "act Decision 2 says a person's key performs.");
    }

    [Test]
    public async Task And_the_body_is_carried_across_untouched()
    {
        // DECISION 3 FROM THE OUTSIDE. Sealing again would mint a fresh nonce, so
        // a byte-identical ciphertext is what makes "it never decrypted the
        // credential" checkable by somebody who does not trust the comment.
        var key = AnUnlockedKey();
        using var runner = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var mine = CredentialSeal.Seal(Value, [key.PublicKey]);
        var theirs = CredentialSeal.Rewrap(mine, key, PublicHalf(runner));

        await Assert.That(theirs.Ciphertext).IsEqualTo(mine.Ciphertext)
            .Because("a rewrap moves a wrapped content key and nothing else.");

        await Assert.That(theirs.Version).IsEqualTo(mine.Version)
            .Because("the body was sealed under that version and is still those bytes.");

        await Assert.That(theirs.Wrapped.Count).IsEqualTo(mine.Wrapped.Count + 1)
            .Because("it ADDS a holder: a push must not cost the pusher its own access.");
    }

    [Test]
    public async Task A_person_who_is_not_a_holder_is_refused_before_anything_moves()
    {
        // NOT SEALED TO YOU IS NOT CORRUPT, and that distinction has to survive
        // the new seam - it is the difference between "ask somebody to push it"
        // and an afternoon spent on a disk that was always fine.
        var stranger = AnUnlockedKey();
        using var holder = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var runner = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var notTheirs = CredentialSeal.Seal(Value, [PublicHalf(holder)]);

        await Assert.That(() => CredentialSeal.Rewrap(notTheirs, stranger, PublicHalf(runner)))
            .Throws<CryptographicException>()
            .Because("a key that cannot open this refuses HERE rather than producing an envelope "
                   + "that fails on the recipient's machine, with a diagnosis pointing at them.");
    }

    [Test]
    public async Task The_refusal_names_the_holders_and_never_the_bytes()
    {
        var stranger = AnUnlockedKey();
        using var holder = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var runner = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var notTheirs = CredentialSeal.Seal(Value, [PublicHalf(holder)]);

        var refused = Assert.Throws<CryptographicException>(
            () => CredentialSeal.Rewrap(notTheirs, stranger, PublicHalf(runner)));

        await Assert.That(refused!.Message).DoesNotContain(Value)
            .Because("a refusal about a credential is not a place to print one.");

        await Assert.That(refused.Message).Contains("never pushed here")
            .Because("the sentence that sends somebody to the right place is the one "
                   + "SaidWhenNoHolder already writes, and the new seam must keep reaching it.");
    }
}
