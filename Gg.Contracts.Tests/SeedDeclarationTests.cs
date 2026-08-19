using System.Reflection;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The seed is a wire resource now, and this is what holds it to that.
/// </summary>
/// <remarks>
/// <para>
/// <b>It was a client-side document and it becomes a served type.</b>
/// <c>TakeSeedComposer</c> ran in the console from a local digest and put its
/// rendering on that machine's clipboard, which is why handoff only worked for
/// somebody at that keyboard. Composing it control-plane-side needs the type in
/// the contract, because the two repositories cannot reference each other and
/// only this package crosses.
/// </para>
/// <para>
/// <b>The composer moves with it, deliberately.</b> Reimplementing the
/// composition on the control-plane side would be two derivations of one
/// document, and they would drift on the first change to either. It is the same
/// argument <c>CredentialLocator.ForRepo</c> is here for: derived by the
/// contract's own rule, so neither side can be wrong on its own.
/// </para>
/// </remarks>
public class SeedDeclarationTests
{
    [Test]
    public async Task The_seed_is_served_from_a_declared_route_a_person_may_call()
    {
        var seed = ProtocolSurface.Endpoints
            .SingleOrDefault(e => e.Path == "/v1/flights/{ref}/seed" && e.Method == "GET");

        await Assert.That(seed).IsNotNull()
            .Because("handoff stops being machine-local the moment the seed is fetchable, and a "
                   + "route this file does not name is a route the control plane may not serve: "
                   + "/v1/flights is a governed prefix and the declaration is closed over it.");

        await Assert.That(seed!.Response).IsEqualTo(typeof(TakeSeed));

        await Assert.That(seed.Audience).IsEqualTo(Audience.Developer)
            .Because("a runner that could read this could read what every flight in the tenant "
                   + "tried and ruled out. The consequence is deliberate and is why a resuming "
                   + "loop is HANDED the seed on its lease rather than fetching one.");

        await Assert.That(seed.RequiredHeaders).Contains(ProtocolSurface.SessionHeader);

        await Assert.That(seed.Statuses).Contains(404)
            .Because("a flight nobody has, and another tenant's flight, answer alike - the same "
                   + "rule GET /v1/flights/{ref} already follows.");
    }

    [Test]
    public async Task The_seed_carries_a_revision_of_its_own()
    {
        // A SECOND NUMBER, and it earns itself. The protocol revision says which
        // conversation both sides are having; this says what shape the document an
        // AGENT reads as context is in. A seed whose shape changed silently would
        // change what every future resumption knows, with nothing to point at.
        await Assert.That(TakeSeed.CurrentRevision).IsGreaterThan(0);

        var revision = typeof(TakeSeed).GetProperty("Revision");

        await Assert.That(revision).IsNotNull()
            .Because("the constant is what this build believes; the member is what a reader is "
                   + "told, and a reader cannot see a constant.");
        await Assert.That(revision!.PropertyType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task No_member_of_the_seed_can_hold_a_path_on_one_machine()
    {
        // STRUCTURAL, because the defect was structural. TakeSeed carried
        // `TreePath` - "where the work is, on this machine" - and that one member
        // is the whole reason the feature could not leave the keyboard it ran on.
        // LoopDigest.Validate already refuses an absolute path for the same
        // reason; the seed made of digests must not reintroduce one.
        var offenders = typeof(TakeSeed).GetProperties()
            .Where(p => p.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)
                     || p.Name.Contains("Tree", StringComparison.OrdinalIgnoreCase)
                     || p.Name.Contains("Directory", StringComparison.OrdinalIgnoreCase)
                     || p.Name.Contains("Machine", StringComparison.OrdinalIgnoreCase)
                     || p.Name.Contains("Host", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("a member named for a location is one that will eventually hold one. Found: "
                   + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_seed_and_its_measurements_are_pinned_and_in_the_vocabulary()
    {
        foreach (var type in (Type[])[typeof(TakeSeed), typeof(TakeMeasurements)])
        {
            await Assert.That(type.GetCustomAttribute<PinnedIdAttribute>()).IsNotNull()
                .Because($"{type.Name} crosses now, and everything that crosses is pinned.");

            await Assert.That(Vocabulary.Types).Contains(type)
                .Because($"{type.Name} is missing from the manifest, and silently absent is not a "
                       + "state that manifest allows.");
        }
    }

    [Test]
    public async Task Both_of_the_seeds_states_are_closed_vocabularies_rather_than_enumerations()
    {
        // NOT AN ENUM, and the difference is what reaches the wire. AccountState
        // was a C# enum while the seed was a local document; an enum crossing
        // this boundary serializes as an INTEGER, which is a wire value no
        // auditor can read and no vocabulary mechanism can see. Every other
        // closed set in this contract is a string list, discovered by shape.
        await Assert.That(AccountStates.All).Contains(AccountStates.Missing);
        await Assert.That(TranscriptStates.All).Contains(TranscriptStates.Elsewhere);
        await Assert.That(TranscriptStates.All).Contains(TranscriptStates.None);

        var members = ProtocolSurface.JsonMembers[typeof(TakeSeed)];

        await Assert.That(members).Contains("transcriptState");
        await Assert.That(members).Contains("accountState");
        await Assert.That(members).Contains("revision");
    }
}
