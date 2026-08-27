using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The narrowings declaration is declared on the wire, in both directions.
/// </summary>
/// <remarks>
/// <para>
/// <b>A member on a wire type is not on the wire until it is declared here.</b>
/// <c>ProtocolSurface.JsonMembers</c> is the only thing holding the two
/// repositories together on member names — they cannot reference each other, so
/// the control plane's conformance test compares what it serializes against
/// this list and nothing else.
/// </para>
/// <para>
/// <b>Which is exactly how this was found.</b> The member was added to both
/// records, the registry, the schema and the door, and the control plane's suite
/// answered <i>"got: credential, id, name, narrowings, path, provider"</i>
/// against a declaration of five. The guard worked; this is the half of it that
/// lives where the declaration does.
/// </para>
/// </remarks>
public class NarrowingsDeclaredTests
{
    [Test]
    public async Task The_request_declares_the_narrowings_member()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RegisterRepositoryRequest)])
            .Contains("narrowings");
    }

    [Test]
    public async Task The_answer_declares_it_too()
    {
        // Both directions, because a tap a tenant can turn on and cannot read
        // back is one they cannot check.
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RepositoryRegistered)])
            .Contains("narrowings");
    }

    [Test]
    public async Task Every_member_of_both_types_is_declared()
    {
        // The general form, so the next member added to either record fails
        // here rather than in the other repository's conformance suite - which
        // is where this one surfaced, one repository and one release away from
        // the line that needed changing.
        foreach (var type in (Type[])[typeof(RegisterRepositoryRequest), typeof(RepositoryRegistered)])
        {
            var declared = ProtocolSurface.JsonMembers[type];
            var actual = type.GetProperties()
                .Select(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..])
                .ToList();

            await Assert.That(actual.Except(declared, StringComparer.Ordinal)).IsEmpty()
                .Because($"{type.Name} carries a member nothing declares, so the control "
                       + "plane cannot serialize it without failing conformance.");
            await Assert.That(declared.Except(actual, StringComparer.Ordinal)).IsEmpty()
                .Because($"{type.Name} declares a member it does not have, which is a name "
                       + "the control plane is required to emit and cannot.");
        }
    }
}
