namespace Gg.Contracts;

/// <summary>One layer's document, and where it sat.</summary>
/// <remarks>
/// <b>The layer is beside the document, never inside it.</b> A composer that read
/// the layer out of the document would let a flight-level file claim <c>org</c>,
/// which is the governed thing describing its own authority - the rule that says
/// no envelope arrives from a runner, one level up.
/// </remarks>
[PinnedId("7a3e19c4-6b02-4d58-9f31-8c04e7b25d6a")]
public sealed record EnvelopeLayer
{
    /// <summary>One of <see cref="ObligationProvenances"/>. Assigned by whoever held the file.</summary>
    public required string Layer { get; init; }

    /// <summary>What that layer's authors wrote.</summary>
    public required Envelope Document { get; init; }

    /// <summary>The version of that document, so the composition is reproducible.</summary>
    public required string Version { get; init; }
}

/// <summary>What composing a set of layers produced, or why it could not.</summary>
[PinnedId("2c85f0b7-91d4-4e63-a07c-5b1e9d38f24c")]
public sealed record Composition
{
    /// <summary>The single envelope evaluation sees, or null when refused.</summary>
    public Envelope? Composed { get; init; }

    /// <summary>What was wrong, naming the layer and the obligation.</summary>
    public string? Refused { get; init; }
}

/// <summary>
/// Layers into the one envelope evaluation reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>Add-only, which is decidable, rather than narrow-only, which is not.</b> The
/// design says a lower layer may only narrow and never widen. General widening
/// detection over predicates is undecidable, and every approximation is wrong in
/// ways nobody can characterise - so what is implemented is the rule that has the
/// same effect and a yes-or-no answer: <b>a lower layer may add its own
/// obligations and may not touch a higher layer's.</b> No modification, no
/// removal, no weakening.
/// </para>
/// <para>
/// <b>Strengthening needs no primitive, because strengthening is adding.</b> If
/// org declares <c>check: agent</c> and a flight wants a person to look as well,
/// the flight ADDS its own obligation. The org's stays exactly as org wrote it,
/// both attach, and the stricter one binds because both must hold. That removes
/// the only case that looked like it needed an edit - and it means the envelope
/// has no edit-a-higher-layer operation to secure, which is a better outcome than
/// securing one.
/// </para>
/// <para>
/// <b>Order-independent.</b> Layers are composed by their declared ranking rather
/// than by the order they were handed over, so a caller cannot change what
/// governs a flight by shuffling a list.
/// </para>
/// </remarks>
public static class EnvelopeComposition
{
    /// <summary>
    /// The composed envelope, with every obligation carrying the layer that
    /// introduced it.
    /// </summary>
    /// <remarks>
    /// The context, loops and destinations come from the highest layer present.
    /// Only obligations compose in this step: a second binding, a second loop and
    /// a second destination are all cardinality this slice does not move.
    /// </remarks>
    public static Composition Compose(IReadOnlyList<EnvelopeLayer> layers)
    {
        ArgumentNullException.ThrowIfNull(layers);

        if (layers.Count == 0)
        {
            return new Composition { Refused = "No layers to compose. A flight governed by "
                                             + "nothing is a real state and it is not this one." };
        }

        if (layers.Select(l => l.Layer).Distinct(StringComparer.Ordinal).Count() != layers.Count)
        {
            return new Composition { Refused = "Two documents claim the same layer. Which one "
                                             + "governs would be decided by list order, and a "
                                             + "list order is not an ownership model." };
        }

        foreach (var layer in layers)
        {
            if (!ObligationProvenances.All.Contains(layer.Layer, StringComparer.Ordinal))
            {
                return new Composition
                {
                    Refused = $"'{layer.Layer}' is not a layer this version knows. Expected one "
                            + $"of: {string.Join(", ", ObligationProvenances.All)}.",
                };
            }
        }

        // BY THE DECLARED RANKING, never by how the caller happened to order the
        // list. Composition that depended on argument order would let whoever
        // assembles the call decide which layer outranks which.
        var ordered = layers
            .OrderBy(l => ObligationProvenances.All.ToList().IndexOf(l.Layer))
            .ToList();

        var composed = new List<Obligation>();
        var introducedBy = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var layer in ordered)
        {
            foreach (var obligation in layer.Document.Obligations)
            {
                if (introducedBy.TryGetValue(obligation.Id, out var higher))
                {
                    // THE ONE REFUSAL, and it names the layer rather than the rule.
                    // "Obligation redefined" would send somebody reading their own
                    // file for a mistake that is in somebody else's.
                    return new Composition
                    {
                        Refused = $"The {layer.Layer} layer declares obligation "
                                + $"'{obligation.Id}', which the {higher} layer introduced. A "
                                + "lower layer may add its own obligations and may not touch a "
                                + "higher layer's - no modification, no removal, no weakening. "
                                + "To require MORE than "
                                + $"'{obligation.Id}' asks, add an obligation of your own: both "
                                + "attach, both must hold, and the stricter one binds.",
                    };
                }

                introducedBy[obligation.Id] = layer.Layer;

                // ASSIGNED FROM WHERE THE DOCUMENT SAT. Whatever the document says
                // about its own provenance is discarded here, and the parser refuses
                // a document that tries to say anything at all.
                composed.Add(obligation with { Provenance = layer.Layer });
            }
        }

        var highest = ordered[0].Document;

        return new Composition
        {
            Composed = highest with { Obligations = composed },
        };
    }

    /// <summary>
    /// Whether a lower layer's document may be applied over what is already there.
    /// </summary>
    /// <remarks>
    /// The same rule as <see cref="Compose"/>, asked before anything is stored
    /// rather than at evaluation - so an author finds out when they apply rather
    /// than when a flight runs.
    /// </remarks>
    public static string? MayApply(
        string layer, Envelope document, IReadOnlyList<EnvelopeLayer> existing)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(existing);

        return Compose([.. existing.Where(e => !string.Equals(e.Layer, layer, StringComparison.Ordinal)),
            new EnvelopeLayer { Layer = layer, Document = document, Version = "pending" }]).Refused;
    }
}
