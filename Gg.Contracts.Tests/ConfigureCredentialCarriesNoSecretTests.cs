using System.Reflection;

namespace Gg.Contracts.Tests;

/// <summary>
/// The one type on this contract that could carry a value stops being able to.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>ConfigureCredentialAsk.Secret</c> was the single exception on the whole
/// wire surface</b>, and its own remark said so: <i>"CredentialContainmentTests
/// refuses a member called this on every type it scans, which is exactly the
/// guard that should fire if anybody ever adds this one to that list."</i> It
/// was safe structurally — no endpoint names it transitively, and the channel
/// has its own serializer context — but safe-by-arrangement is weaker than
/// safe-by-shape, and ADR-0037 Decision 3 removes the need for the arrangement.
/// </para>
/// <para>
/// <b>What replaces it is a sealed envelope, which the recipient's key opens
/// and nothing else does.</b> So the console that sends it never holds the
/// value, the relay that brokers the introduction never could, and the type
/// itself has nowhere to put one.
/// </para>
/// <para>
/// <b>This is a BREAKING change to the one member a runner reads</b>, so the
/// protocol floor moves with it rather than the contract version alone. A
/// runner one version behind has no arm for an envelope and drops the message —
/// which <c>SendACredential.SaidWhenNothingCameBack</c> already names as one of
/// its three causes.
/// </para>
/// </remarks>
public class ConfigureCredentialCarriesNoSecretTests
{
    private static readonly string[] SecretShapedWords =
        ["token", "secret", "password", "passphrase", "bearer", "apikey", "accesskey"];

    private static IEnumerable<PropertyInfo> MembersOf(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != "EqualityContract");

    [Test]
    public async Task It_carries_a_locator_and_an_envelope_and_nothing_else()
    {
        await Assert.That(MembersOf(typeof(ConfigureCredentialAsk)).Select(p => p.Name).Order().ToList())
            .IsEquivalentTo(new[] { "Envelope", "Locator" });
    }

    [Test]
    public async Task The_envelope_is_the_sealed_type_rather_than_a_string()
    {
        await Assert.That(typeof(ConfigureCredentialAsk).GetProperty("Envelope")!.PropertyType)
            .IsEqualTo(typeof(SealedCredential));
    }

    [Test]
    public async Task No_member_is_named_like_a_value()
    {
        var offenders = MembersOf(typeof(ConfigureCredentialAsk))
            .Where(p => SecretShapedWords.Any(w => p.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Select(p => p.Name)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("found: " + string.Join(", ", offenders));
    }

    [Test]
    public async Task Nothing_reachable_from_it_can_hold_a_plaintext_value()
    {
        // WALKED, rather than checked one level deep. The envelope is a contract
        // type with its own members, and a value hidden one hop down is the
        // thing a shallow check is for.
        var seen = new HashSet<Type>();
        var queue = new Queue<Type>([typeof(ConfigureCredentialAsk)]);
        var offenders = new List<string>();

        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            if (!seen.Add(type))
            {
                continue;
            }

            foreach (var member in MembersOf(type))
            {
                if (SecretShapedWords.Any(w =>
                        member.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
                {
                    offenders.Add($"{type.Name}.{member.Name}");
                }

                var next = member.PropertyType;
                if (next.IsGenericType && next.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
                {
                    next = next.GetGenericArguments()[0];
                }

                if (next.Namespace == typeof(CredentialReference).Namespace && next.IsClass)
                {
                    queue.Enqueue(next);
                }
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("found: " + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_answer_still_says_only_what_was_done()
    {
        // UNCHANGED, and asserted so it stays that way. A runner that answered
        // with what it was handed would put the credential back on a channel a
        // person is watching - which remains the one failure this path cannot
        // have, envelope or not.
        await Assert.That(MembersOf(typeof(ConfiguredCredential)).Select(p => p.Name).Order().ToList())
            .IsEquivalentTo(new[] { "Locator", "Written" });
    }
}
