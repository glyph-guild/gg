using System.Reflection;
using System.Text.Json;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A key a sender omits does not become a null a reader dereferences.
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS SHIPPED, AND IT BROKE THE ESTATE WALK.</b> <c>Envelope.Instructions</c>
/// was declared <c>= []</c> and read as <c>envelope.Instructions.Count</c>. A
/// property initializer does not run on the deserialization path, so every
/// envelope from a control plane that had never heard of the field arrived with
/// null and <c>EnvelopeText.Render</c> threw — while the comment directly above
/// the throwing line promised those envelopes rendered byte-for-byte unchanged.
/// The comment stated the guarantee; the dereference removed it.
/// </para>
/// <para>
/// <b>It had a sibling that predated it by sixty contract versions.</b>
/// <c>Obligation.Evidence</c> carries the same declaration and the same
/// dereference, latent since 0.44.0 because every producer happens to send the
/// key. "Nobody omits it yet" is not a property; it is a habit of the current
/// senders.
/// </para>
/// <para>
/// <b>Which is why this is a sweep and not two fixes.</b> The next optional
/// collection on a wire type will be declared the same way by somebody reading
/// the ones already there, and the failure only appears against a sender that
/// does not write the key — which is precisely the sender you get during a
/// version skew, and never the one in your own tests.
/// </para>
/// </remarks>
public class AbsentCollectionsSurviveTheWireTests
{
    /// <summary>The minimum an envelope must carry, and nothing optional.</summary>
    private const string BareEnvelope = """
        {"context":{"scope":"src/**","constitution":"1.0.0"},
         "obligations":[{"id":"in-scope","check":"machine","rule":"no-file-outside-scope"}],
         "loops":[{"id":"implement","executor":"frontier","discharges":["in-scope"],
                   "moves":["read"],"budget":{"wallClock":"30m"},
                   "onExhaustion":"handoff-to-human"}],
         "destinations":[]}
        """;

    [Test]
    public async Task An_envelope_from_a_sender_that_omits_every_optional_key_still_renders()
    {
        // THE PRODUCTION PATH, and the exact configuration that failed: a CLI
        // one contract version ahead of the control plane it is talking to,
        // which is the ordinary state between a merge and the next pin bump.
        var envelope = JsonSerializer.Deserialize(
            BareEnvelope, ProtocolJsonContext.Default.Envelope)!;

        await Assert.That(() => EnvelopeText.Render(envelope)).ThrowsNothing();
    }

    [Test]
    public async Task An_explicit_null_is_the_same_as_an_absent_key()
    {
        // A sender that writes the key as null is as real as one that omits
        // it, and Gg.Contracts writes null for every optional member because
        // it declares no JsonIgnore anywhere.
        var envelope = JsonSerializer.Deserialize(
            BareEnvelope.Replace("{\"context\"", "{\"instructions\":null,\"context\"",
                StringComparison.Ordinal),
            ProtocolJsonContext.Default.Envelope)!;

        await Assert.That(envelope.Instructions).IsNotNull();
        await Assert.That(() => EnvelopeText.Render(envelope)).ThrowsNothing();
    }

    [Test]
    public async Task No_non_nullable_collection_on_a_composed_type_reads_as_null()
    {
        // THE SWEEP. A non-nullable collection property is a promise to every
        // caller that it can be dereferenced; a nullable one tells the caller
        // to check. Both are fine - what is not fine is the first one lying,
        // which is what an initializer alone does across the wire.
        var envelope = JsonSerializer.Deserialize(
            BareEnvelope, ProtocolJsonContext.Default.Envelope)!;

        var nulls = new List<string>();
        Sweep(envelope, "Envelope", nulls);

        foreach (var obligation in envelope.Obligations)
        {
            Sweep(obligation, "Obligation", nulls);
        }

        foreach (var loop in envelope.Loops)
        {
            Sweep(loop, "Loop", nulls);
        }

        await Assert.That(nulls).IsEmpty()
            .Because("a non-nullable collection that reads null across the wire is a promise "
                   + "the type makes and the deserializer breaks. Found: "
                   + string.Join(", ", nulls));
    }

    [Test]
    public async Task The_sweep_can_tell_a_collection_from_a_scalar()
    {
        // LIVENESS. A sweep that matched nothing would pass for a type whose
        // every collection was null.
        var seen = new List<string>();
        Sweep(
            JsonSerializer.Deserialize(BareEnvelope, ProtocolJsonContext.Default.Envelope)!,
            "Envelope",
            seen,
            recordEvenWhenPresent: true);

        await Assert.That(seen).Contains("Envelope.Obligations");
        await Assert.That(seen).Contains("Envelope.Instructions");
    }

    private static void Sweep(
        object value, string name, List<string> found, bool recordEvenWhenPresent = false)
    {
        foreach (var property in value.GetType().GetProperties(
            BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.PropertyType.IsGenericType
                || property.PropertyType.GetGenericTypeDefinition() != typeof(IReadOnlyList<>))
            {
                continue;
            }

            // A NULLABLE ONE IS NOT A DEFECT. It tells its caller to check, and
            // Accepts and Produces are nullable precisely because absence means
            // something there.
            var context = new NullabilityInfoContext().Create(property);
            if (context.ReadState == NullabilityState.Nullable)
            {
                continue;
            }

            if (recordEvenWhenPresent || property.GetValue(value) is null)
            {
                found.Add($"{name}.{property.Name}");
            }
        }
    }
}
