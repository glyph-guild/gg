using System.Reflection;

namespace Gg.Contracts;

/// <summary>One layer's document, and where it sits in the topology.</summary>
/// <remarks>
/// <b>The role and the name are beside the document, never inside it.</b> A
/// composer that read them out of the document would let a narrowing claim
/// the floor's authority - the rule that says no envelope arrives from a
/// runner, one level up. The parent is what lets <see cref="EnvelopeComposition.Compose"/>
/// verify the chain it was handed rather than trusting the caller's ordering.
/// </remarks>
[PinnedId("7a3e19c4-6b02-4d58-9f31-8c04e7b25d6a")]
public sealed record EnvelopeLayer
{
    /// <summary>One of <see cref="Roles"/>. Assigned by the topology, never the document.</summary>
    public required string Role { get; init; }

    /// <summary>The document's name. <c>root</c> for the floor, open otherwise.</summary>
    public required string Name { get; init; }

    /// <summary>The name this one sits under. Null only for root.</summary>
    public string? Parent { get; init; }

    /// <summary>The full document, for root and work kinds. Null for a narrowing.</summary>
    public Envelope? Document { get; init; }

    /// <summary>The partial document, for narrowings. Null otherwise.</summary>
    public EnvelopeNarrowing? Narrowing { get; init; }

    /// <summary>The version of that document, so the composition is reproducible.</summary>
    public required string Version { get; init; }
}

/// <summary>What composing a set of layers produced, or why it could not.</summary>
[PinnedId("2c85f0b7-91d4-4e63-a07c-5b1e9d38f24c")]
public sealed record Composition
{
    /// <summary>The single envelope evaluation sees, or null when refused.</summary>
    public Envelope? Composed { get; init; }

    /// <summary>What was wrong, naming the layer and the field.</summary>
    public string? Refused { get; init; }
}

/// <summary>
/// Layers into the one envelope evaluation reads - generic over the operator
/// table, which is data on the schema.
/// </summary>
/// <remarks>
/// <para>
/// <b>Order-freedom is a property of the operators, not a claim about the
/// code.</b> The predecessor composed by a ranking (<c>ordered[0]</c> took
/// five fields wholesale), and ADR-0014 records that reasoning as wrong: it
/// was an artifact of replacement semantics. <c>intersect</c>, <c>min</c> and
/// <c>union</c> are commutative and associative, and the two <c>-only</c>
/// operators name a single role - so there is no ordering to consult and a
/// caller cannot change what governs a flight by shuffling a list.
/// </para>
/// <para>
/// <b>A document declaring a field it may not MOVE is refused, not
/// ignored.</b> The old wholesale-take silently discarded a lower layer's
/// context, loops and destinations; at chain depth four that is three
/// documents silently dropped - the silent-no-op class this product exists
/// to name, arriving through the layering machinery itself. An ECHO of the
/// governing value is not a move: work kinds are full envelopes and
/// <c>Validate</c> requires their members.
/// </para>
/// <para>
/// <b>One layer supplies the sets. Everything below it is a meet.</b> Which
/// loops and destinations EXIST comes from the work kind when one is present
/// and from root otherwise; you pick sets, you do not narrow them into
/// being. Everything else composes by its declared operator.
/// </para>
/// </remarks>
public static class EnvelopeComposition
{
    /// <summary>The operator table: field to operator, read off the schema.</summary>
    /// <remarks>
    /// Built by reflection in the static constructor, which THROWS on a field
    /// that neither declares an operator nor carries a written exemption - so
    /// a new schema member fails the build's first composition test until
    /// somebody decides how it composes. The drift guard the fact vocabulary
    /// has, applied here.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> Operators { get; }

    /// <summary>The fields that deliberately have no operator, each with its reason.</summary>
    public static IReadOnlyDictionary<string, string> OperatorExemptions { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [$"{nameof(Envelope)}.{nameof(Envelope.Context)}"] =
                "a container; its leaves declare (scope intersects, constitution is root-only)",
            [$"{nameof(Loop)}.{nameof(Loop.Id)}"] =
                "identity, not policy - it is the key the set is a set OF",
            [$"{nameof(Loop)}.{nameof(Loop.Discharges)}"] =
                "intra-document wiring: it names its own document's obligations and travels "
              + "with its loop, which the sets' operator owns",
            [$"{nameof(Destination)}.{nameof(Destination.MaySelect)}"] =
                "a container; its leaves declare (both menus intersect, the way opens does)",
            [$"{nameof(Loop)}.{nameof(Loop.Budget)}"] =
                "a container; its leaves declare (wall-clock and attempts are min)",
            [$"{nameof(Destination)}.{nameof(Destination.Id)}"] =
                "identity, not policy",
            [$"{nameof(Destination)}.{nameof(Destination.Kind)}"] =
                "what the destination IS; membership in the set is the composable thing, "
              + "and the sets' operator owns it",
        };

    static EnvelopeComposition()
    {
        var operators = new Dictionary<string, string>(StringComparer.Ordinal);
        Walk(typeof(Envelope), operators);
        Walk(typeof(ContextBinding), operators);
        Walk(typeof(Loop), operators);
        Walk(typeof(LoopBudget), operators);
        Walk(typeof(Destination), operators);
        Walk(typeof(DestinationSelection), operators);
        Walk(typeof(EnvelopeNarrowing), operators);

        Operators = operators;
    }

    /// <summary>One schema type's properties into the table - or the throw that is the guard.</summary>
    private static void Walk(
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
        Type type,
        Dictionary<string, string> operators)
    {
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var field = $"{type.Name}.{property.Name}";
                var declared = property.GetCustomAttribute<ComposesAttribute>();

                if (declared is null && !OperatorExemptions.ContainsKey(field))
                {
                    throw new InvalidOperationException(
                        $"{field} declares no merge operator and carries no exemption. A field "
                        + "nobody decided an operator for would compose by accident - declare "
                        + "one of: " + string.Join(", ", MergeOperators.All)
                        + ", or write the exemption down with its reason.");
                }

                if (declared is not null && OperatorExemptions.ContainsKey(field))
                {
                    throw new InvalidOperationException(
                        $"{field} declares an operator AND carries an exemption; one of the two "
                        + "is stale.");
                }

                if (declared is not null)
                {
                    if (!MergeOperators.All.Contains(declared.Operator, StringComparer.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"{field} declares operator '{declared.Operator}', which is not one "
                            + "of: " + string.Join(", ", MergeOperators.All) + ".");
                    }

                    operators[field] = declared.Operator;
                }
            }
        }
    }

    private static string Op(string type, string member) => Operators[$"{type}.{member}"];

    /// <summary>
    /// The composed envelope, with every obligation carrying (role, name).
    /// </summary>
    public static Composition Compose(IReadOnlyList<EnvelopeLayer> layers)
    {
        ArgumentNullException.ThrowIfNull(layers);

        if (Verify(layers) is { } broken)
        {
            return new Composition { Refused = broken };
        }

        var root = layers.FirstOrDefault(l => string.Equals(l.Role, Roles.Root, StringComparison.Ordinal));
        var workKind = layers.FirstOrDefault(l => string.Equals(l.Role, Roles.WorkKind, StringComparison.Ordinal));

        // ONE LAYER SUPPLIES THE SETS: the work kind when present, the floor
        // otherwise. Everything the base carries that another layer may not
        // move is checked below, field by field, from the table.
        var @base = workKind ?? root!;
        var baseDocument = @base.Document!;

        // The obligations, from EVERY layer: union keyed by id, a collision
        // refused naming both documents - shadowing is removal with extra
        // steps, and refusing the collision is worth more than the add-only
        // rule it protects.
        var composed = new List<Obligation>();
        var introducedBy = new Dictionary<string, string>(StringComparer.Ordinal);

        // BY NAME, so the composed sequence is the composer's rather than the
        // caller's: composition is order-free in content, but the composed
        // obligations keep an order and a pin's digest is over the bytes.
        // Two callers composing the same layers must hash the same bytes.
        foreach (var layer in layers.OrderBy(l => l.Name, StringComparer.Ordinal))
        {
            var obligations = layer.Document?.Obligations ?? layer.Narrowing!.Obligations;
            foreach (var obligation in obligations)
            {
                if (introducedBy.TryGetValue(obligation.Id, out var other))
                {
                    return new Composition
                    {
                        Refused = $"Both '{other}' and '{layer.Name}' declare obligation "
                                + $"'{obligation.Id}'. Two documents' rules under one name would "
                                + "compose to whichever was read second - to require MORE than "
                                + $"'{obligation.Id}' asks, add an obligation of your own: both "
                                + "attach, both must hold, and the stricter one binds.",
                    };
                }

                introducedBy[obligation.Id] = layer.Name;

                // ASSIGNED FROM WHERE THE DOCUMENT SAT. Whatever the document
                // says about its own provenance is discarded here, and the
                // parser refuses a document that tries to say anything at all.
                composed.Add(obligation with
                {
                    Provenance = new ObligationProvenance { Role = layer.Role, Name = layer.Name },
                });
            }
        }

        // THE INSTRUCTIONS, APPENDED IN LAYER ORDER. Not name order, unlike the
        // obligations above: for those the sequence is arbitrary and sorting by
        // name only buys byte-stability, while here the order IS the content.
        // Root's guidance is written to be read first and a work kind's to be
        // read after it, so ranking by role is the semantics rather than a
        // tie-break — and name order within a rank keeps two callers composing
        // the same layers to the same bytes, which the pin's digest needs.
        //
        // Only full documents contribute. A narrowing has no such member and
        // will not get one: it declares what it ADDS, and text is not that.
        var instructions = new List<EnvelopeInstruction>();

        foreach (var layer in layers
            .Where(l => l.Document is not null)
            .OrderBy(l => InstructionRank(l.Role))
            .ThenBy(l => l.Name, StringComparer.Ordinal))
        {
            foreach (var instruction in layer.Document!.Instructions)
            {
                // ASSIGNED FROM WHERE THE DOCUMENT SAT, exactly as an
                // obligation's is. Guidance whose source a person cannot find
                // is guidance nobody can change.
                instructions.Add(instruction with
                {
                    Provenance = new ObligationProvenance { Role = layer.Role, Name = layer.Name },
                });
            }
        }

        // THE ROOT-ONLY FIELDS AND THE MEETS, from the table. Only full
        // envelopes carry them - a narrowing cannot express any of this, which
        // is the strongest form of the operator table.
        var context = baseDocument.Context;
        var environment = baseDocument.Environment;
        var repository = baseDocument.Repository;

        if (root is not null && !ReferenceEquals(@base, root))
        {
            var floor = root.Document!;

            if (Moved(@base, floor.Context.Constitution, baseDocument.Context.Constitution,
                    $"{nameof(ContextBinding)}.{nameof(ContextBinding.Constitution)}".ToLowerInvariant(),
                    Op(nameof(ContextBinding), nameof(ContextBinding.Constitution))) is { } constitution)
            {
                return new Composition { Refused = constitution };
            }

            if (Moved(@base, floor.Environment, baseDocument.Environment, "environment",
                    Op(nameof(Envelope), nameof(Envelope.Environment))) is { } environmentMoved)
            {
                return new Composition { Refused = environmentMoved };
            }

            if (Moved(@base, floor.Repository, baseDocument.Repository, "repository",
                    Op(nameof(Envelope), nameof(Envelope.Repository))) is { } repositoryMoved)
            {
                return new Composition { Refused = repositoryMoved };
            }

            environment = floor.Environment;
            repository = floor.Repository;

            // SCOPE INTERSECTS, and an intersection nobody can express as one
            // glob is a refusal naming both rather than a silent pick.
            switch (IntersectScope(floor.Context.Scope, baseDocument.Context.Scope))
            {
                case (null, { } undecidable):
                    return new Composition { Refused = undecidable };
                case ({ } met, _):
                    context = baseDocument.Context with
                    {
                        Scope = met,
                        Constitution = floor.Context.Constitution,
                    };
                    break;
            }
        }

        // A NARROWING'S OBLIGATIONS BIND, and this is where the operator table's
        // union on Destination.Requires finally gets applied. It has been
        // declared since slice nine and honoured by nothing: destinations came
        // wholesale from the base and no lower layer touched them, so a
        // narrowing added obligations that admission - which iterates `requires`
        // and nothing else - never looked at.
        //
        // ADR-0014 names the cost: "a narrowing must be able to union
        // `requires`, or it is decorative." A narrowing that may only add
        // obligations blocks nothing and produces verdicts nobody has to honour.
        //
        // THE BINDING IS THE COMPOSER'S, not the author's, and that is the whole
        // design. A member on EnvelopeNarrowing naming destination ids would be
        // a cross-document reference that can dangle - naming an exit the work
        // kind does not declare, or failing to name one it does. There is no
        // such member, so neither failure is expressible.
        //
        // ROOT AND THE WORK KIND ARE NOT AUTO-BOUND. They author their own
        // requires in the same document, and a floor that records an obligation
        // without requiring it made that choice deliberately - "a destination
        // requiring nothing is a real envelope" is a sentence in the admission
        // engine. A narrowing has no field in which to make that choice, which
        // is exactly why its default has to be the constraining one.
        var bound = layers
            .Where(l => string.Equals(l.Role, Roles.Narrowing, StringComparison.Ordinal))
            .SelectMany(l => l.Narrowing!.Obligations)
            .Select(o => o.Id)
            .ToList();

        var destinations = bound.Count == 0
            ? baseDocument.Destinations
            : [.. baseDocument.Destinations.Select(d => d with
            {
                // UNION rather than append: `requires` is iterated to build the
                // refusal sentence, so the same id twice reads as two
                // outstanding gates where there is one.
                Requires = (IReadOnlyList<string>)
                    [.. d.Requires.Concat(bound).Distinct(StringComparer.Ordinal)],
            })];

        return new Composition
        {
            Composed = baseDocument with
            {
                Context = context,
                Environment = environment,
                Repository = repository,
                Obligations = composed,
                Instructions = instructions,
                Destinations = destinations,
            },
        };
    }

    /// <summary>
    /// Where a role sits when instructions are appended.
    /// </summary>
    /// <remarks>
    /// <b>A total order over the roles that can carry text</b>, so appending is
    /// deterministic. It is the reading order a person expects: the floor's
    /// standing guidance, then what this kind of work adds to it. An unknown
    /// role sorts last rather than throwing — a role this table has not learned
    /// about should not be able to insert itself ahead of root.
    /// </remarks>
    private static int InstructionRank(string role) => role switch
    {
        Roles.Root => 0,
        Roles.WorkKind => 1,
        _ => 2,
    };

    /// <summary>
    /// Whether a lower layer's document may be applied over what is already
    /// there - the same rule as <see cref="Compose"/>, asked before anything
    /// is stored.
    /// </summary>
    public static string? MayApply(EnvelopeLayer candidate, IReadOnlyList<EnvelopeLayer> existing)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(existing);

        return Compose([
            .. existing.Where(e => !string.Equals(e.Name, candidate.Name, StringComparison.Ordinal)),
            candidate]).Refused;
    }

    private static string? Verify(IReadOnlyList<EnvelopeLayer> layers)
    {
        if (layers.Count == 0)
        {
            return "No layers to compose. A flight governed by nothing is a real state and it "
                 + "is not this one.";
        }

        foreach (var layer in layers)
        {
            if (!Roles.All.Contains(layer.Role, StringComparer.Ordinal))
            {
                return $"'{layer.Role}' is not a role this version knows. Expected one of: "
                     + string.Join(", ", Roles.All) + ".";
            }

            var isFull = string.Equals(layer.Role, Roles.Narrowing, StringComparison.Ordinal)
                ? layer.Narrowing is not null && layer.Document is null
                : layer.Document is not null && layer.Narrowing is null;
            if (!isFull)
            {
                return $"'{layer.Name}' carries the wrong document shape for the "
                     + $"'{layer.Role}' role. Root and work kinds carry the full envelope; a "
                     + "narrowing carries the partial document that cannot express what its "
                     + "role may not move.";
            }
        }

        if (layers.Select(l => l.Name).Distinct(StringComparer.Ordinal).Count() != layers.Count)
        {
            var doubled = layers.GroupBy(l => l.Name, StringComparer.Ordinal)
                .First(g => g.Count() > 1).Key;
            return $"Two documents claim the name '{doubled}'. Which one governs would be "
                 + "decided by list order, and a list order is not an ownership model.";
        }

        var roots = layers.Where(l => string.Equals(l.Role, Roles.Root, StringComparison.Ordinal)).ToList();
        if (roots.Count > 1)
        {
            return "Two documents claim the root role. The floor is exactly one document, "
                 + "always.";
        }

        foreach (var layer in layers)
        {
            var isRoot = string.Equals(layer.Role, Roles.Root, StringComparison.Ordinal);
            var namedRoot = string.Equals(layer.Name, Roles.Root, StringComparison.Ordinal);

            if (isRoot && (!namedRoot || layer.Parent is not null))
            {
                return $"The root role belongs to the name 'root' and sits under nothing; "
                     + $"'{layer.Name}' claims it. root is a reserved name, never a pointer - "
                     + "repointing the floor is a gated envelope change, not a relabeling.";
            }

            if (!isRoot && namedRoot)
            {
                return $"'{Roles.Root}' is the floor's name and this document claims the "
                     + $"'{layer.Role}' role under it.";
            }

            // THE CHAIN: every non-root layer names a parent that is present.
            // Verified from what was handed rather than trusted from the
            // caller's ordering - which is what lets order-freedom survive
            // losing the enum.
            if (!isRoot)
            {
                if (layer.Parent is not { Length: > 0 } parent)
                {
                    return $"'{layer.Name}' names no parent. Only root sits under nothing.";
                }

                if (!layers.Any(l => string.Equals(l.Name, parent, StringComparison.Ordinal)))
                {
                    return $"'{layer.Name}' names '{parent}' as its parent, and no document by "
                         + "that name is in this composition - a chain is verified from the "
                         + "floor up, and a link to a missing document is not a chain.";
                }
            }
        }

        if (roots.Count == 0)
        {
            // Reached only when every parent resolves, which with no root
            // means a cycle.
            return "No document in this composition plays root, and every parent resolves - "
                 + "which is a cycle. A chain reaches the floor or it is not a chain.";
        }

        var workKinds = layers.Where(l => string.Equals(l.Role, Roles.WorkKind, StringComparison.Ordinal)).ToList();
        if (workKinds.Count > 1)
        {
            return "Two documents claim the work-kind role. One layer supplies the sets - a "
                 + "flight is FOR one thing, declared at creation.";
        }

        return null;
    }

    /// <summary>An echo of the governing value is not a move; anything else is.</summary>
    private static string? Moved(
        EnvelopeLayer layer, string? governing, string? declared, string field, string @operator) =>
        string.Equals(governing, declared, StringComparison.Ordinal)
            ? null
            : $"'{layer.Name}' moves {field} from '{governing ?? "nothing"}' to "
            + $"'{declared ?? "nothing"}', and {field} is {@operator}: only the root document "
            + "may move it. Echoing the governing value is allowed; moving it is asking for "
            + "the floor's authority.";

    /// <summary>
    /// The meet of two scopes, when one contains the other - or why there is
    /// no expressible meet.
    /// </summary>
    /// <remarks>
    /// Glob containment is decidable for the shapes this schema uses: equal
    /// scopes, the universal scope, and prefix globs (<c>dir/**</c>). Two
    /// scopes where neither contains the other have a real intersection that
    /// no single glob expresses, and inventing one would silently change what
    /// governs work - refused naming both.
    /// </remarks>
    private static (string? Met, string? Refused) IntersectScope(string wider, string narrower)
    {
        // NONE IS A DOMAIN MISMATCH, NOT A NARROWER GLOB, and this branch is
        // the whole of ADR-0020's unlisted question. A path bound is a
        // statement about a tree; work that accepts no subject has no tree, so
        // the floor's bound is not narrowed away and is not in conflict with
        // anything - it does not apply, and that is decided from the documents
        // alone without evaluating a fact.
        //
        // WITHOUT IT THE FIRST RESEARCH FLIGHT IS A REFUSAL. `src/**` and
        // `none` fall through to "neither contains the other", which is
        // correct for two globs and nonsense for a glob and a declaration that
        // there is nothing to glob over.
        //
        // The computed value is what `none absorbs` would also produce, and
        // that is worth saying rather than glossing: what differs is that the
        // answer comes from a declaration rather than from a claim that
        // nothing is narrower than something. Validate keeps `none` legal
        // exactly where `accepts: []` is, so this branch is unreachable except
        // by a document that declared it takes no subject - which is what
        // makes the reasoning checkable instead of merely stated.
        if (string.Equals(wider, EnvelopeScopes.None, StringComparison.Ordinal)
            || string.Equals(narrower, EnvelopeScopes.None, StringComparison.Ordinal))
        {
            return (EnvelopeScopes.None, null);
        }

        if (string.Equals(wider, narrower, StringComparison.Ordinal))
        {
            return (wider, null);
        }

        if (Contains(wider, narrower))
        {
            return (narrower, null);
        }

        if (Contains(narrower, wider))
        {
            return (wider, null);
        }

        return (null,
            $"scope '{wider}' and scope '{narrower}' have no expressible intersection - "
          + "neither contains the other, and a glob nobody wrote deciding what governs work "
          + "is worse than a refusal. Narrow one until it sits inside the other.");
    }

    /// <summary>Glob containment: does <paramref name="outer"/> allow everything <paramref name="inner"/> does?</summary>
    /// <remarks>Internal so direction shares the one primitive scope ordering has.</remarks>
    internal static bool ScopeContains(string outer, string inner) => Contains(outer, inner);

    private static bool Contains(string outer, string inner)
    {
        if (string.Equals(outer, "**", StringComparison.Ordinal))
        {
            return true;
        }

        if (!outer.EndsWith("/**", StringComparison.Ordinal))
        {
            return false;
        }

        var prefix = outer[..^2];

        return inner.StartsWith(prefix, StringComparison.Ordinal);
    }
}
