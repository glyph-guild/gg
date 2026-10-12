using System.Text.Json;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// An extension is applied and read back through the named-envelope bodies,
/// the way a narrowing is.
/// </summary>
/// <remarks>
/// <para>
/// <b>A third member, not a third door</b> - the wrapper's own argument. The
/// topology decides what a name carries, so a body with the wrong shape is a
/// mistake the control plane can name precisely.
/// </para>
/// <para>
/// <b>Exactly one of three.</b> An apply carrying two shapes is one whose
/// meaning depends on which the reader believes.
/// </para>
/// </remarks>
public class AnExtensionCrossesTheWireTests
{
    [Test]
    public async Task An_apply_may_carry_an_extension_alone()
    {
        var apply = new NamedEnvelopeApply { Extension = WorkKindFixtures.Extension(WorkKindFixtures.Child) };

        await Assert.That(apply.Validate()).IsNull();
    }

    [Test]
    public async Task An_apply_carries_exactly_one_shape()
    {
        var extension = WorkKindFixtures.Extension(WorkKindFixtures.Child);
        var envelope = WorkKindFixtures.Parsed(WorkKindFixtures.Base);

        await Assert.That(new NamedEnvelopeApply { Extension = extension, Envelope = envelope }.Validate())
            .IsNotNull();
        await Assert.That(new NamedEnvelopeApply
            {
                Extension = extension,
                Narrowing = new EnvelopeNarrowing { Obligations = [] },
            }.Validate())
            .IsNotNull();
        await Assert.That(new NamedEnvelopeApply().Validate()).IsNotNull();
    }

    [Test]
    public async Task A_state_reads_an_extension_back()
    {
        var state = new NamedEnvelopeState
        {
            Name = "investigate-instructions",
            Role = Roles.WorkKind,
            Version = "investigate-instructions@v1",
            Extension = WorkKindFixtures.Extension(WorkKindFixtures.Child),
            UpdatedAt = DateTimeOffset.UnixEpoch,
            UpdatedBy = "a-person",
        };

        var json = JsonSerializer.Serialize(state, JsonSerializerOptions.Web);
        var back = JsonSerializer.Deserialize<NamedEnvelopeState>(json, JsonSerializerOptions.Web)!;

        await Assert.That(back.Extension!.Extends).IsEqualTo("investigate");
        await Assert.That(back.Extension.Remove!.Destinations).IsEquivalentTo(new[] { "none" });
        await Assert.That(json).Contains("\"extension\"");
    }
}
