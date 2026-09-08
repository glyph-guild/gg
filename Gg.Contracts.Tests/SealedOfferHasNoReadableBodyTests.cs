using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// What the control plane relays, it cannot read.
/// </summary>
/// <remarks>
/// <para>
/// <b>The precedent is <c>EvidenceReference</c>, whose remark is unambiguous:
/// "there is deliberately no field a body could travel in."</b> This is the same
/// sentence about a different body. A sealed offer that grew a
/// <c>Candidates</c> member "for diagnostics" would be the relay reading the
/// thing it exists not to read, and it would arrive as a helpful addition.
/// </para>
/// <para>
/// <b>What is inside, so the reason is not abstract.</b> ICE candidates are the
/// customer's private subnet addresses, host ports and egress IP. This platform
/// already declined to carry a hashed <i>hostname</i> —
/// <c>EnvironmentIdentity.HostFingerprint</c> calls it "an identifier nobody
/// needs" — and a raw private address is more identifying than that.
/// </para>
/// </remarks>
public class SealedOfferHasNoReadableBodyTests
{
    private static readonly Type[] Relayed = [typeof(RunnerSealedOffer), typeof(RunnerSealedAnswer)];

    [Test]
    public async Task Nothing_relayed_carries_a_readable_body()
    {
        // A DENY LIST OF SHAPES RATHER THAN NAMES. `Sdp`, `Candidates` and
        // `Fingerprint` are what somebody would add; what they have in common is
        // being text or a structure, and the sealed member is bytes.
        var readable = Relayed
            .SelectMany(t => t.GetProperties().Select(p => (Type: t, Property: p)))
            .Where(x => x.Property.Name is not ("Sealed" or "Capability" or "RunnerId"))
            .Select(x => $"{x.Type.Name}.{x.Property.Name}")
            .ToList();

        await Assert.That(readable).IsEmpty()
            .Because("a member that is not the capability, the runner's name or the sealed "
                   + "bytes is a member the relay could read. Found: "
                   + string.Join(", ", readable));
    }

    [Test]
    public async Task The_sealed_member_is_bytes_rather_than_text()
    {
        // BYTES, because text invites a look. A base64 string would carry the
        // same information and would read, in a log or a debugger, as something
        // somebody could helpfully decode.
        foreach (var type in Relayed)
        {
            var body = type.GetProperty("Sealed");

            await Assert.That(body).IsNotNull()
                .Because($"{type.Name} has nothing to relay.");
            await Assert.That(body!.PropertyType).IsEqualTo(typeof(byte[]));
        }
    }

    [Test]
    public async Task The_declaration_names_exactly_those_members()
    {
        // THE THIRD REGISTRATION AGREES WITH THE TYPE. A member the type has and
        // the declaration does not is one outside the fingerprint - which is
        // where an added `Candidates` would live most comfortably.
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerSealedOffer)])
            .IsEquivalentTo(new[] { "capability", "sealed" });
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerSealedAnswer)])
            .IsEquivalentTo(new[] { "runnerId", "sealed" });
    }

    [Test]
    public async Task The_answer_carries_no_capability()
    {
        // THE OTHER DIRECTION IS NOT A SECOND GRANT. The runner is answering an
        // introduction it already verified; a capability travelling backwards
        // would be a grant nobody asked for and nobody would think to revoke.
        await Assert.That(typeof(RunnerSealedAnswer).GetProperties().Select(p => p.Name))
            .DoesNotContain("Capability");
    }

    [Test]
    public async Task The_scan_would_notice_a_body_that_was_really_there()
    {
        // The poison twin: every assertion above passes on a type with no
        // properties at all, and an empty Relayed array is how that happens.
        await Assert.That(Relayed.SelectMany(t => t.GetProperties())).IsNotEmpty();

        var planted = typeof(Leaky).GetProperties()
            .Where(p => p.Name is not ("Sealed" or "Capability" or "RunnerId"))
            .Select(p => p.Name)
            .ToList();

        await Assert.That(planted).Contains("Candidates")
            .Because("the filter has to be able to find the member somebody would add, or "
                   + "finding none on the real types says nothing.");
    }

    private sealed record Leaky
    {
        public required byte[] Sealed { get; init; }

        /// <summary>What somebody would add for diagnostics.</summary>
        public string? Candidates { get; init; }
    }
}
