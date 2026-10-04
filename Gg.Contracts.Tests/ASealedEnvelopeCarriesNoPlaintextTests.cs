using System.Reflection;
using System.Security.Cryptography;

namespace Gg.Contracts.Tests;

/// <summary>
/// Nothing on the sealed envelope can hold a plaintext value, and its
/// ciphertext is a shape the credential path already allows.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asserted over the TYPE, not over anybody's care.</b> This is
/// <c>CredentialContainmentTests</c>' discipline applied to a type that is not
/// yet reachable from its roots: the envelope reaches the credential path in
/// step 5, when <c>ConfigureCredentialAsk</c> stops carrying a
/// <c>Secret</c>. Until then the existing walk cannot see it, and a type that
/// arrives unguarded and is connected up later is a type nobody ever checks.
/// </para>
/// <para>
/// <b><c>byte[]</c> is deliberately not an option.</b>
/// <c>CredentialContainmentTests</c>' allowed shapes are a closed set — string,
/// int, bool, DateTimeOffset, nullables of those, contract types and
/// <c>IReadOnlyList</c> of those — and widening it is an argued amendment
/// rather than a convenience taken while adding a field. So ciphertext is
/// base64 in a <c>string</c>, which costs a third more bytes on a wire that
/// carries a token, and buys a guard that keeps working.
/// </para>
/// <para>
/// <b>The round-trip assertion is the one that could actually catch a leak.</b>
/// Checking member names catches a member called <c>Secret</c>; it does not
/// catch a correctly-named member that happens to contain the value. Sealing a
/// known string and looking for it in everything the envelope serialises is
/// what covers that.
/// </para>
/// </remarks>
public class ASealedEnvelopeCarriesNoPlaintextTests
{
    /// <summary>
    /// The same words <c>CredentialContainmentTests</c> refuses.
    /// </summary>
    /// <remarks>
    /// Repeated rather than shared because that list is private to a test in
    /// this assembly and a test helper is not wire surface. If the two ever
    /// disagree the envelope is the stricter of them, which is the safe
    /// direction for a duplicate to drift.
    /// </remarks>
    private static readonly string[] SecretShapedWords =
        ["token", "secret", "password", "passphrase", "bearer", "apikey", "privatekey", "accesskey"];

    private static IEnumerable<PropertyInfo> MembersOf(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != "EqualityContract");

    [Test]
    public async Task No_member_of_the_envelope_is_named_like_a_secret()
    {
        var offenders = new[] { typeof(SealedCredential), typeof(WrappedContentKey) }
            .SelectMany(t => MembersOf(t).Select(p => (Type: t, Property: p)))
            .Where(m => SecretShapedWords.Any(w =>
                m.Property.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Select(m => $"{m.Type.Name}.{m.Property.Name}")
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("found: " + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_ciphertext_is_a_string_rather_than_a_shape_the_allowlist_does_not_name()
    {
        await Assert.That(typeof(SealedCredential).GetProperty("Ciphertext")!.PropertyType)
            .IsEqualTo(typeof(string));

        await Assert.That(typeof(WrappedContentKey).GetProperty("Wrapped")!.PropertyType)
            .IsEqualTo(typeof(string));
    }

    [Test]
    public async Task Every_member_is_a_shape_the_credential_path_already_allows()
    {
        static bool Allowed(Type type) =>
            type == typeof(string)
            || type == typeof(int)
            || type == typeof(bool)
            || type == typeof(DateTimeOffset)
            || (Nullable.GetUnderlyingType(type) is { } inner && Allowed(inner))
            || type.Namespace == typeof(CredentialReference).Namespace && type.IsClass
            || (type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
                && Allowed(type.GetGenericArguments()[0]));

        var offenders = new[] { typeof(SealedCredential), typeof(WrappedContentKey) }
            .SelectMany(t => MembersOf(t).Select(p => (Type: t, Property: p)))
            .Where(m => !Allowed(m.Property.PropertyType))
            .Select(m => $"{m.Type.Name}.{m.Property.Name} ({m.Property.PropertyType.Name})")
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("found: " + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_envelope_carries_exactly_the_members_it_is_declared_to_have()
    {
        // A CLOSED SET, so a fifth member is a decision somebody makes here
        // rather than a field that appears. This is the assertion that fails
        // when a later step reaches for "just one more string".
        await Assert.That(MembersOf(typeof(SealedCredential)).Select(p => p.Name).Order().ToList())
            .IsEquivalentTo(new[] { "Ciphertext", "Version", "Wrapped" });

        await Assert.That(MembersOf(typeof(WrappedContentKey)).Select(p => p.Name).Order().ToList())
            .IsEquivalentTo(new[] { "Holder", "Wrapped" });
    }

    [Test]
    public async Task The_sealed_value_appears_in_nothing_the_envelope_exposes()
    {
        using var holder = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        const string Value = "a-token-nobody-should-find";

        var envelope = CredentialSeal.Seal(
            Value, [Convert.ToBase64String(holder.PublicKey.ExportSubjectPublicKeyInfo())]);

        var everything = string.Join(
            "\n",
            [envelope.Version.ToString(), envelope.Ciphertext,
             .. envelope.Wrapped.SelectMany(w => new[] { w.Holder, w.Wrapped })]);

        await Assert.That(everything).DoesNotContain(Value);

        // AND NOT BASE64 OF IT EITHER, which is the shape a lazy "encode it"
        // would produce and which a plain substring check would walk past.
        await Assert.That(everything)
            .DoesNotContain(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Value)));
    }

    [Test]
    public async Task The_envelope_is_pinned_and_registered_like_every_other_wire_type()
    {
        foreach (var type in new[] { typeof(SealedCredential), typeof(WrappedContentKey) })
        {
            await Assert.That(type.GetCustomAttribute<PinnedIdAttribute>()).IsNotNull()
                .Because($"{type.Name} crosses the wire and a rename must not change its identity.");

            await Assert.That(Vocabulary.Types.Contains(type)).IsTrue()
                .Because($"{type.Name} must be registered or the build fails.");
        }
    }
}
