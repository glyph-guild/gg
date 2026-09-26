using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// Every fact kind the vocabulary declares has a payload slot
/// <see cref="FactEnvelope.Validate"/> knows about.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written because it was missing and cost a live flight.</b> GG-324 shipped a
/// <c>loop.session</c> and ingress refused it: <i>"A fact carries exactly one
/// payload; this one carries 0."</i> The kind was registered every other way -
/// the constant, <c>FactKinds.All</c>, the pinned type, the envelope slot, the
/// declared JSON members, the vocabulary, the category, the pipeline, the
/// hygiene, the disposition - and <c>Validate</c>'s own <c>carried</c> table did
/// not name it, so the envelope arrived with a payload nothing counted.
/// </para>
/// <para>
/// <b>Invisible to every test that built an envelope directly</b>, which is all
/// of them: the pipeline's tests assert what lands in the slot, and nothing
/// validated the result the way ingress does. So the first thing to find it was a
/// runner on the fleet, which is the most expensive place to learn it.
/// </para>
/// <para>
/// <b>And it is the failure this vocabulary names as its worst.</b>
/// <c>FactKinds</c>' own remark: <i>"Silently absent is indistinguishable from
/// satisfied, which is this system's most dangerous failure mode."</i> This one
/// was loud, which is luck rather than design - a kind whose slot is missing is
/// refused, and a kind whose slot is missing while something else is present
/// would be accepted as that something else.
/// </para>
/// </remarks>
public class EveryKindHasASlotTests
{
    [Test]
    public async Task No_kind_is_left_without_a_slot_that_validate_counts()
    {
        var unslotted = FactKinds.All
            .Except(FactEnvelope.KindsWithASlot, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(unslotted).IsEmpty()
            .Because("a kind Validate cannot count is a fact a runner ships and ingress "
                   + "refuses with 'this one carries 0' - which is what happened to "
                   + "loop.session on GG-324. Found: " + string.Join(", ", unslotted));
    }

    /// <summary>
    /// And nothing has a slot the vocabulary does not declare.
    /// </summary>
    /// <remarks>
    /// The other direction, because a slot for a kind nobody declares is a
    /// payload no producer can name and no reader can ask for - dead surface that
    /// reads as capability.
    /// </remarks>
    [Test]
    public async Task No_slot_is_left_without_a_kind()
    {
        var undeclared = FactEnvelope.KindsWithASlot
            .Except(FactKinds.All, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(undeclared).IsEmpty()
            .Because("a slot whose kind nobody declares is dead surface that reads as "
                   + "capability. Found: " + string.Join(", ", undeclared));
    }

    /// <summary>
    /// The two kinds GG-324 found, driven through Validate rather than compared.
    /// </summary>
    /// <remarks>
    /// <b>Liveness for the two assertions above</b>, which are set arithmetic and
    /// would pass on an empty KindsWithASlot. This one builds the envelope a runner
    /// builds and asks what ingress asks.
    /// </remarks>
    [Test]
    public async Task The_two_kinds_that_found_this_validate_as_shipped()
    {
        var artifact = new ArtifactReference
        {
            Locator = "/state/transcripts/f/implement.session.jsonl",
            Sha256 = new string('b', 64),
            Bytes = 1,
            MediaType = "application/x-ndjson",
            Scope = ArtifactScopes.RunnerLocal,
        };

        var shell = new FactEnvelope
        {
            IdempotencyKey = "k",
            Kind = FactKinds.LoopSession,
            Digest = new string('a', 64),
            ObservedAt = DateTimeOffset.UnixEpoch,
        };

        await Assert.That(FactEnvelope.Validate(
            shell with { Session = new LoopSession { Artifact = artifact } })).IsNull();

        await Assert.That(FactEnvelope.Validate(shell with
        {
            Kind = FactKinds.DocumentProposal,
            Document = new DocumentProposal
            {
                Role = Roles.WorkKind, Name = "ui-preview", Document = "context:\n",
            },
        })).IsNull();
    }
}
