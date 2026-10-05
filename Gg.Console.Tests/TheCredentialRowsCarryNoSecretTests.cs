using System.Reflection;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Nothing the console carries about credentials is a secret, an envelope or key
/// material.
/// </summary>
/// <remarks>
/// <para>
/// <b>S60.2-05, and <c>AppState</c> is why it is a criterion rather than an
/// assumption.</b> That record is serialized by a source generator, written to
/// disk whenever <c>GG_STATE_DUMP</c> is set, and put into a diagnostics bundle
/// somebody sends us. Anything it can hold is something that can end up in a
/// support ticket.
/// </para>
/// <para>
/// <b>Locators go in and values do not.</b> A locator is a reference — the
/// control plane already holds it, `gg credential list` prints it on purpose, and
/// a person needs it to know which file is being talked about. The value, the
/// envelope and any key material are a different class of thing, and the shapes
/// below are checked so that no later member can quietly be one.
/// </para>
/// <para>
/// <b>Mirrors <c>CredentialContainmentTests</c> rather than inventing a rule.</b>
/// Same banned member names, same closed set of allowed shapes — because this is
/// the same question that test asks of the wire, asked of the model instead.
/// </para>
/// </remarks>
public class TheCredentialRowsCarryNoSecretTests
{
    private static readonly string[] Banned =
        ["token", "secret", "password", "passphrase", "bearer", "apikey", "privatekey",
         "accesskey", "ciphertext", "envelope", "wrapped"];

    private static readonly Type[] Allowed =
    [
        typeof(string), typeof(int), typeof(bool), typeof(DateTimeOffset),
        typeof(int?), typeof(bool?), typeof(DateTimeOffset?),
    ];

    /// <summary>
    /// Whether a member's type is one a value could not hide in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>WIDENED DELIBERATELY for <c>CredentialRow.Holders</c>, which is a list
    /// of records.</b> The first version allowed only primitives and lists of
    /// primitives, which is right for a flat row and wrong the moment a row
    /// carries a sub-row. <c>CredentialContainmentTests</c> permits the same thing
    /// on the wire — contract types and lists of them — so this is the model
    /// catching up with the rule the wire already has, not a new exemption.
    /// </para>
    /// <para>
    /// <b>And it is recursive rather than a free pass for records.</b> A record is
    /// allowed only when every one of ITS members is allowed, so a holder type
    /// that grew a <c>byte[]</c>, or a member reaching a contract envelope, is
    /// still refused — one level down or five. "It is a record" must not become
    /// the sentence that lets a secret into a diagnostics bundle.
    /// </para>
    /// </remarks>
    private static bool IsAllowed(Type type)
    {
        if (Allowed.Contains(type))
        {
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            return IsAllowed(type.GetGenericArguments()[0]);
        }

        // OURS ONLY. A type from another assembly - a contract envelope, say -
        // is not something this test gets to vouch for by walking it.
        if (type.Assembly != typeof(CredentialRow).Assembly)
        {
            return false;
        }

        var members = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        return members.Length > 0 && members.All(m => IsAllowed(m.PropertyType));
    }

    [Test]
    public async Task No_member_of_a_row_is_named_for_a_secret()
    {
        var offenders = new List<string>();

        foreach (var type in (Type[])[typeof(CredentialRow), typeof(CredentialAtRest)])
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var name = property.Name.ToLowerInvariant();

                if (Banned.Any(b => name.Contains(b, StringComparison.Ordinal)))
                {
                    offenders.Add($"{type.Name}.{property.Name}");
                }
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("a member named for a secret is a member that will eventually hold one, and "
                   + "this record reaches a diagnostics bundle. Found: "
                   + string.Join(", ", offenders));
    }

    [Test]
    public async Task No_member_of_a_row_is_a_shape_a_value_could_hide_in()
    {
        var offenders = new List<string>();

        foreach (var type in (Type[])[typeof(CredentialRow), typeof(CredentialAtRest)])
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!IsAllowed(property.PropertyType))
                {
                    offenders.Add($"{type.Name}.{property.Name} ({property.PropertyType.Name})");
                }
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("byte[] is not on the allowlist for the same reason the wire's is not, and a "
                   + "contract type reached from here would drag a whole envelope into the model. "
                   + "Found: " + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_shape_check_would_see_a_byte_array_if_one_were_there()
    {
        // POISON TWIN. "No offenders" is also what a walk over an allowlist that
        // accepts everything returns, and this file would look diligent either
        // way.
        await Assert.That(IsAllowed(typeof(byte[]))).IsFalse();
        await Assert.That(IsAllowed(typeof(string))).IsTrue();
        await Assert.That(IsAllowed(typeof(IReadOnlyList<string>))).IsTrue();

        // THE WIDENING, AND ITS LIMIT. A list of our own flat records passes; a
        // contract type does not, even though it is also a record, because this
        // test does not get to vouch for another assembly's shape.
        await Assert.That(IsAllowed(typeof(IReadOnlyList<CredentialHolder>))).IsTrue();
        await Assert.That(IsAllowed(typeof(SealedCredential))).IsFalse();
        await Assert.That(IsAllowed(typeof(IReadOnlyList<SealedCredential>))).IsFalse();

        // AND ONE LEVEL DOWN. "It is a record" must not be the sentence that lets
        // a secret into a diagnostics bundle.
        await Assert.That(IsAllowed(typeof(HidesBytesInARecord))).IsFalse()
            .Because("a record whose own member is a byte[] is exactly what a recursive check "
                   + "exists to refuse, and a non-recursive one would wave it through.");
    }

    /// <summary>A planted record with a shape a value could hide in.</summary>
    private sealed record HidesBytesInARecord(string Locator, byte[] Material);

    [Test]
    public async Task The_model_carries_the_resting_shapes_and_they_survive_a_dump()
    {
        // THE DUMP IS THE POINT. A member the generator cannot serialize is one
        // that silently vanishes from a diagnostics bundle - and the resting word
        // is the only thing in this pane that is about THIS machine, so it is the
        // one nobody could reconstruct from the control plane afterwards.
        var state = new AppState
        {
            CredentialResting =
            [
                new CredentialAtRest("local:acme/widgets", CredentialResting.Sealed, []),
                new CredentialAtRest("local:acme/legacy", CredentialResting.Plaintext, []),
            ],
        };

        var dumped = AppStateJson.Serialize(state);

        await Assert.That(dumped).Contains("local:acme/widgets");
        await Assert.That(dumped).Contains(CredentialResting.Plaintext);

        var back = AppStateJson.Deserialize(dumped);

        await Assert.That(CredentialsAtRest.RestingOf(back.CredentialResting ?? [], "local:acme/legacy"))
            .IsEqualTo(CredentialResting.Plaintext);
    }
}
