using System.Reflection;
using System.Security.Cryptography;
using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// A person's key performs the agreement and hands back bytes. It never hands
/// back itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>A criterion about a type signature, which is unusual and deliberate.</b>
/// ADR-0037 records that every public entry point on <c>RunnerSeal</c> takes a
/// concrete <see cref="ECDiffieHellman"/>, and that a PIV-backed key **is not
/// one** — .NET cannot construct that object over a key it does not hold. Those
/// signatures are right, because they carry MACHINE keys. If a person's key
/// reaches the push path the same way, hardware is closed, and reopening it
/// means changing the shape of a type both repositories share.
/// </para>
/// <para>
/// <b>It costs nothing today, which is exactly why it needs a test.</b> There
/// is no YubiKey in this slice and none planned; the rule is free to hold and
/// expensive to restore, so the only thing that keeps it is something that
/// fails when somebody reaches for the obvious shortcut — returning the key
/// because the caller needs an agreement and the key is right there.
/// </para>
/// <para>
/// <b>Asserted over the whole public surface rather than over one method</b>,
/// because the shortcut does not have to be called
/// <c>ForOpeningWhatWasSealedToThisPerson</c> to be the same hole.
/// </para>
/// </remarks>
public class APersonsKeyNeverLeavesItsAdapterTests
{
    private const string Passphrase = "correct horse battery staple";

    private static string APath() =>
        Path.Combine(Path.GetTempPath(), "gg-adapter-" + Guid.NewGuid().ToString("N"), "person-key");

    private static PersonKey AnUnlockedKey()
    {
        var path = APath();
        PersonKey.Create(path, Passphrase);
        return PersonKey.Unlock(path, Passphrase);
    }

    [Test]
    public async Task No_member_hands_back_an_ECDiffieHellman()
    {
        var offenders = typeof(PersonKey)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(m => (m.Name, Type: m switch
            {
                MethodInfo method => method.ReturnType,
                PropertyInfo property => property.PropertyType,
                FieldInfo field => field.FieldType,
                _ => typeof(void),
            }))
            .Where(m => typeof(ECDiffieHellman).IsAssignableFrom(m.Type)
                     || typeof(AsymmetricAlgorithm).IsAssignableFrom(m.Type)
                     || m.Type == typeof(ECParameters))
            .Select(m => m.Name)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("a person's key handing itself out closes the hardware door. Found: "
                   + string.Join(", ", offenders));
    }

    [Test]
    public async Task No_member_takes_one_either()
    {
        // THE OTHER DIRECTION, which is the subtler half. A method accepting an
        // ECDiffieHellman would mean somebody outside had already got one from
        // somewhere, and the adapter would be endorsing it.
        var offenders = typeof(PersonKey)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.GetParameters().Any(p => typeof(AsymmetricAlgorithm).IsAssignableFrom(p.ParameterType)))
            .Select(m => m.Name)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("found: " + string.Join(", ", offenders));
    }

    [Test]
    public async Task It_performs_an_agreement_and_returns_bytes()
    {
        using var theirs = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var key = AnUnlockedKey();

        var agreed = key.AgreeWith(
            Convert.ToBase64String(theirs.PublicKey.ExportSubjectPublicKeyInfo()), "a-label");

        await Assert.That(agreed.Length).IsEqualTo(32);
    }

    [Test]
    public async Task The_agreement_is_the_one_the_other_side_computes()
    {
        // A KEY THAT RETURNS 32 BYTES IS NOT ENOUGH; they have to be the RIGHT
        // bytes, or an adapter returning a hash of nothing would pass the test
        // above and seal things nobody can open.
        using var theirs = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var key = AnUnlockedKey();

        var ours = key.AgreeWith(
            Convert.ToBase64String(theirs.PublicKey.ExportSubjectPublicKeyInfo()), "a-label");

        var theirSide = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            theirs.DeriveRawSecretAgreement(
                Imported(key.PublicKey).PublicKey),
            32,
            info: System.Text.Encoding.UTF8.GetBytes("a-label"));

        await Assert.That(ours).IsEquivalentTo(theirSide);
    }

    [Test]
    public async Task A_different_label_is_a_different_agreement()
    {
        using var theirs = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var key = AnUnlockedKey();
        var theirPublic = Convert.ToBase64String(theirs.PublicKey.ExportSubjectPublicKeyInfo());

        await Assert.That(key.AgreeWith(theirPublic, "one"))
            .IsNotEquivalentTo(key.AgreeWith(theirPublic, "two"))
            .Because("labels are what stop one sealed thing opening as another.");
    }

    private static ECDiffieHellman Imported(string publicKey)
    {
        var key = ECDiffieHellman.Create();
        key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
        return key;
    }
}
