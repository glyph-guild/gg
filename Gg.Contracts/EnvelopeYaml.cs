using YamlDotNet.Core;
using YamlDotNet.Core.Events;

// PARSING IS AUTHORING, NOT WIRE. The result records below are never
// serialized: they carry a model, a diagnosis and some notes back to whoever
// asked, on the same machine. A pinned id on one would be a promise about
// something that never crosses a boundary - which is the reason
// Gg.Contracts.Description sits in its own namespace, and this is the same
// reason one document class over.
namespace Gg.Contracts.Authoring;

using Gg.Contracts;

/// <summary>What reading an envelope produced.</summary>
public sealed record EnvelopeParse
{
    /// <summary>
    /// The version the text says it was based on, or null when it says nothing.
    /// </summary>
    /// <remarks>
    /// <b>The third class of key: consumed.</b> Part of the document, refused, or
    /// this — a precondition the applier states, honoured at apply and then gone.
    /// It is deliberately not a member of the model: the stored form is the
    /// idempotence key, so a key that changes on every pull would mint a version
    /// per document per pull and divert every one of them to a gate.
    /// </remarks>
    public string? BasedOn { get; init; }

    /// <summary>The envelope, or null when there is a diagnosis.</summary>
    public Envelope? Envelope { get; init; }

    /// <summary>What was wrong, or null when nothing was.</summary>
    public string? Diagnosis { get; init; }

    /// <summary>
    /// Things that were true of the text and are not true of the model.
    /// </summary>
    /// <remarks>
    /// Not warnings and not errors: facts about what the round trip did.
    /// Comments are the only one today, and the reason the list exists is that
    /// losing one silently is the thing somebody discovers weeks later.
    /// </remarks>
    public IReadOnlyList<string> Notes { get; init; } = [];
}


/// <summary>What reading a narrowing produced.</summary>
/// <remarks>
/// A separate result for a separate door. The role is chosen by the caller -
/// there is no <c>kind:</c> discriminator, because a document that could name
/// its own role is the governed thing describing its own authority, the same
/// rule as <c>layer:</c> and <c>provenance:</c>.
/// </remarks>
public sealed record EnvelopeNarrowingParse
{
    /// <summary>
    /// The version the text says it was based on, or null when it says nothing.
    /// </summary>
    /// <remarks>
    /// <b>The third class of key: consumed.</b> Part of the document, refused, or
    /// this — a precondition the applier states, honoured at apply and then gone.
    /// It is deliberately not a member of the model: the stored form is the
    /// idempotence key, so a key that changes on every pull would mint a version
    /// per document per pull and divert every one of them to a gate.
    /// </remarks>
    public string? BasedOn { get; init; }

    /// <summary>The narrowing, or null when there is a diagnosis.</summary>
    public EnvelopeNarrowing? Narrowing { get; init; }

    /// <summary>What was wrong, or null when nothing was.</summary>
    public string? Diagnosis { get; init; }

    /// <summary>Facts about what the round trip did. Comments are the only one today.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}

/// <summary>What reading a strategy produced.</summary>
/// <remarks>
/// A separate result for a separate door, the narrowing's rule: the caller
/// decided this text is a strategy by which door it knocked on. <c>kind:</c>
/// inside names the infrastructure row (docker-host), never the document's
/// role.
/// </remarks>
public sealed record StrategyParse
{
    /// <summary>
    /// The version the text says it was based on, or null when it says nothing.
    /// </summary>
    /// <remarks>
    /// <b>The third class of key: consumed.</b> Part of the document, refused, or
    /// this — a precondition the applier states, honoured at apply and then gone.
    /// It is deliberately not a member of the model: the stored form is the
    /// idempotence key, so a key that changes on every pull would mint a version
    /// per document per pull and divert every one of them to a gate.
    /// </remarks>
    public string? BasedOn { get; init; }

    /// <summary>The strategy, or null when there is a diagnosis.</summary>
    public EnvironmentStrategy? Strategy { get; init; }

    /// <summary>What was wrong, or null when nothing was.</summary>
    public string? Diagnosis { get; init; }

    /// <summary>Facts about what the round trip did. Comments are the only one today.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}

/// <summary>
/// Envelope YAML to model. The only YAML parser in the product.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here and nowhere else.</b> Every YAML library is a package reference, so
/// this cannot live in <c>Gg.Contracts</c>, and it must not live in the
/// control plane: YAML's attack surface - billion laughs, anchor expansion,
/// type coercion - stays on this side of the boundary, away from the service
/// that holds the platform's own signing keys. The wire between them is JSON,
/// and both sides fail closed on their own format.
/// </para>
/// <para>
/// <b>The event layer, not the object mapper.</b> Of 432 types in YamlDotNet
/// exactly two carry <c>[RequiresDynamicCode]</c> - <c>DeserializerBuilder</c>
/// and <c>SerializerBuilder</c> - which are the reflection-driven mapper this
/// deliberately does not use. That is not only what makes it AOT-publishable:
/// it is what makes it CORRECT. At the event layer a scalar is a string and a
/// style, so <c>1.10</c> stays <c>1.10</c> because nothing ever asks for a
/// type; anchors and aliases arrive as events rather than being resolved away;
/// and duplicate keys arrive as two entries rather than one. All four hazards
/// are visible here and invisible one layer up.
/// </para>
/// <para>
/// <b>A superset of what the emitter writes is accepted.</b> Flow style,
/// unquoted identifiers, any key order. Somebody hand-writing an envelope
/// should not have to guess our canonical form, and <c>show</c> normalises it.
/// </para>
/// </remarks>
public static class EnvelopeYaml
{
    /// <summary>Reads envelope text, or says what is wrong with it.</summary>
    public static EnvelopeParse Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Node document;
        try
        {
            document = Read(text);
        }
        catch (EnvelopeSyntaxException refusal)
        {
            return new EnvelopeParse { Diagnosis = refusal.Message };
        }
        catch (YamlException malformed)
        {
            // The library's own diagnosis, with the position it carries. Its
            // wording is about YAML rather than about envelopes, which is
            // right: at this point the text is not an envelope, it is not
            // YAML.
            return new EnvelopeParse
            {
                Diagnosis = $"This is not readable as YAML at line {malformed.Start.Line}, "
                          + $"column {malformed.Start.Column}: {malformed.Message}",
            };
        }

        Envelope envelope;
        try
        {
            envelope = Map(document);
        }
        catch (EnvelopeSyntaxException refusal)
        {
            return new EnvelopeParse { Diagnosis = refusal.Message };
        }

        // The SCHEMA's own rule, applied after the text has been read. Shared
        // rather than reimplemented here, so gg and the control plane cannot
        // disagree about what a valid envelope is.
        if (Envelope.Validate(envelope) is { } invalid)
        {
            return new EnvelopeParse { Diagnosis = invalid };
        }

        return new EnvelopeParse
        {
            Envelope = envelope, BasedOn = Consumed(document), Notes = Notes(text),
        };
    }


    /// <summary>Reads narrowing text, or says what is wrong with it.</summary>
    /// <remarks>
    /// The same reader, the same obligation mapping, a root closed to exactly
    /// one key - so <c>loops:</c>, <c>destinations:</c>, <c>context:</c> and a
    /// selection are refused by name rather than parsed and silently dropped
    /// by composition, which is the silent-no-op class this slice deletes.
    /// </remarks>
    public static EnvelopeNarrowingParse ParseNarrowing(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Node document;
        try
        {
            document = Read(text);
        }
        catch (EnvelopeSyntaxException refusal)
        {
            return new EnvelopeNarrowingParse { Diagnosis = refusal.Message };
        }
        catch (YamlException malformed)
        {
            return new EnvelopeNarrowingParse
            {
                Diagnosis = $"This is not readable as YAML at line {malformed.Start.Line}, "
                          + $"column {malformed.Start.Column}: {malformed.Message}",
            };
        }

        EnvelopeNarrowing narrowing;
        try
        {
            var root = RequireMap(document, "");
            Closed(root, BasedOnKey, "obligations");
            narrowing = new EnvelopeNarrowing
            {
                Obligations = [.. Named(root, "obligations").Select(MapObligation)],
            };
        }
        catch (EnvelopeSyntaxException refusal)
        {
            return new EnvelopeNarrowingParse { Diagnosis = refusal.Message };
        }

        if (EnvelopeNarrowing.Validate(narrowing) is { } invalid)
        {
            return new EnvelopeNarrowingParse { Diagnosis = invalid };
        }

        return new EnvelopeNarrowingParse
        {
            Narrowing = narrowing, BasedOn = Consumed(document), Notes = Notes(text),
        };
    }

    /// <summary>Reads strategy text, or says what is wrong with it.</summary>
    /// <remarks>
    /// <para>
    /// <b>The closed key set is the containment.</b> <c>host:</c>,
    /// <c>socket:</c>, <c>daemon:</c> and <c>credential:</c> are refused by
    /// not being admitted - the resident runner's endpoint lives in its own
    /// environment and nowhere in a policy document, so there is no key for
    /// it to arrive through.
    /// </para>
    /// <para>
    /// <b>A missing pull point is refused for its own reason</b>, before the
    /// generic missing-key wording could claim it: a powered-off pool cannot
    /// pull, and the author finds that out here rather than at 2 a.m.
    /// </para>
    /// </remarks>
    public static StrategyParse ParseStrategy(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Node document;
        try
        {
            document = Read(text);
        }
        catch (EnvelopeSyntaxException refusal)
        {
            return new StrategyParse { Diagnosis = refusal.Message };
        }
        catch (YamlException malformed)
        {
            return new StrategyParse
            {
                Diagnosis = $"This is not readable as YAML at line {malformed.Start.Line}, "
                          + $"column {malformed.Start.Column}: {malformed.Message}",
            };
        }

        EnvironmentStrategy strategy;
        try
        {
            strategy = MapStrategy(document);
        }
        catch (EnvelopeSyntaxException refusal)
        {
            return new StrategyParse { Diagnosis = refusal.Message };
        }

        // The SCHEMA's own rule, shared rather than reimplemented, so gg and
        // the control plane cannot disagree about what a valid strategy is.
        if (EnvironmentStrategy.Validate(strategy) is { } invalid)
        {
            return new StrategyParse { Diagnosis = invalid };
        }

        return new StrategyParse
        {
            Strategy = strategy, BasedOn = Consumed(document), Notes = Notes(text),
        };
    }

    private static EnvironmentStrategy MapStrategy(Node document)
    {
        var root = RequireMap(document, "");
        Closed(root, BasedOnKey, "kind", "environment", "inventory", "pull-point", "image", "bounds");

        if (!root.Entries.ContainsKey("pull-point"))
        {
            throw new EnvelopeSyntaxException(
                "This strategy names no pull point, and a powered-off pool cannot pull. "
              + "Declare pull-point: " + PullPoints.ResidentRunner
              + " - the refusal happens here, at authoring, not at 2 a.m.");
        }

        var inventory = RequireMap(Require(root, "inventory"), "inventory");
        Closed(inventory, "pool", "size", "warm");
        var size = WholeNumber(Require(inventory, "size"), "inventory.size");

        // ABSENT WARM IS ZERO, never the size: a document that names no target
        // is one written before targets existed, and it meant "warm behind
        // demand". Defaulting to the inventory would turn every strategy
        // already in force into a proactive one on the deploy that read it.
        var warm = inventory.Entries.TryGetValue("warm", out var declaredWarm)
            ? WholeNumber(declaredWarm, "inventory.warm")
            : 0;

        // ABSENT BOUNDS DEFAULT TO THE INVENTORY, never to unbounded: the
        // size is the outermost bound a pool can have, so an author who
        // declares none has declared that one.
        var bounds = new StrategyBounds { PoolMax = size, ActiveHours = null };
        if (root.Entries.TryGetValue("bounds", out var declared))
        {
            var map = RequireMap(declared, "bounds");
            Closed(map, "pool-max", "active-hours");
            bounds = new StrategyBounds
            {
                PoolMax = map.Entries.TryGetValue("pool-max", out var poolMax)
                    ? WholeNumber(poolMax, "bounds.pool-max")
                    : size,
                ActiveHours = map.Entries.TryGetValue("active-hours", out var hours)
                    ? RequireScalar(hours, "bounds.active-hours")
                    : null,
            };
        }

        return new EnvironmentStrategy
        {
            Kind = RequireScalar(Require(root, "kind"), "kind"),
            Environment = RequireScalar(Require(root, "environment"), "environment"),
            Inventory = new StrategyInventory
            {
                Pool = RequireScalar(Require(inventory, "pool"), "inventory.pool"),
                Size = size,
                Warm = warm,
            },
            PullPoint = RequireScalar(Require(root, "pull-point"), "pull-point"),
            Image = RequireScalar(Require(root, "image"), "image"),
            Bounds = bounds,
        };
    }

    /// <summary>A refusal that already knows how to explain itself.</summary>
    private sealed class EnvelopeSyntaxException(string message) : Exception(message);

    // ---- the document, as a tree of exactly three shapes ----

    private abstract record Node(string Path);

    private sealed record ScalarNode(string Value, string Path) : Node(Path);

    private sealed record MapNode(IReadOnlyDictionary<string, Node> Entries, string Path) : Node(Path);

    private sealed record SeqNode(IReadOnlyList<Node> Items, string Path) : Node(Path);

    /// <summary>
    /// The event stream, into a tree, refusing what must never be resolved
    /// silently.
    /// </summary>
    private static Node Read(string text)
    {
        var parser = new Parser(new StringReader(text));

        if (!Advance(parser) || parser.Current is not StreamStart)
        {
            throw new EnvelopeSyntaxException(Empty);
        }

        if (!Advance(parser) || parser.Current is not DocumentStart)
        {
            throw new EnvelopeSyntaxException(Empty);
        }

        Advance(parser);
        var root = ReadNode(parser, path: "");

        if (!Advance(parser) || parser.Current is not DocumentEnd)
        {
            throw new EnvelopeSyntaxException("An envelope is one document.");
        }

        // A second document is refused rather than the first one silently
        // winning. Which of two envelopes is in force is not a question this
        // should ever have to answer.
        if (Advance(parser) && parser.Current is DocumentStart)
        {
            throw new EnvelopeSyntaxException(
                "This file has more than one YAML document in it, and an envelope is one. "
              + "Which of them governs would be decided by whichever reader saw it first.");
        }

        return root;
    }

    private const string Empty =
        "This is empty. An envelope that governs nothing would let a flight report success "
      + "having enforced nothing at all.";

    private static bool Advance(IParser parser) => parser.MoveNext();

    private static Node ReadNode(IParser parser, string path)
    {
        var current = parser.Current
            ?? throw new EnvelopeSyntaxException(Empty);

        // Anchors and aliases, refused wherever they appear. A policy document
        // that can reference itself is one nobody can read linearly - and an
        // alias is a value that does not appear at the place it takes effect,
        // which is the property a reviewer relies on.
        if (current is NodeEvent node && !node.Anchor.IsEmpty)
        {
            throw new EnvelopeSyntaxException(
                $"An anchor (&{node.Anchor}) is not allowed{At(path)}. An envelope is read top to "
              + "bottom by people, so nothing in it may be defined somewhere else.");
        }

        return current switch
        {
            AnchorAlias alias => throw new EnvelopeSyntaxException(
                $"An alias (*{alias.Value}) is not allowed{At(path)}. An envelope is read top to "
              + "bottom by people, so nothing in it may be defined somewhere else."),
            Scalar scalar => new ScalarNode(scalar.Value, path),
            MappingStart => ReadMap(parser, path),
            SequenceStart => ReadSeq(parser, path),
            _ => throw new EnvelopeSyntaxException($"This is not something an envelope holds{At(path)}."),
        };
    }

    private static MapNode ReadMap(IParser parser, string path)
    {
        var entries = new Dictionary<string, Node>(StringComparer.Ordinal);

        while (Advance(parser) && parser.Current is not MappingEnd)
        {
            if (parser.Current is not Scalar key)
            {
                throw new EnvelopeSyntaxException(
                    $"A key here is not a plain name{At(path)}. An envelope's keys are names.");
            }

            var child = path.Length == 0 ? key.Value : $"{path}.{key.Value}";

            // Duplicate keys, refused rather than last-wins. Parsers vary and
            // most take the last one silently, which is a way to smuggle a
            // change past a reviewer: the diff shows an added line and the
            // behaviour comes from it rather than from the line above it.
            if (entries.ContainsKey(key.Value))
            {
                throw new EnvelopeSyntaxException(
                    $"'{key.Value}' appears twice{At(path)}. Most YAML readers would silently keep "
                  + "the last one, so a reviewer would see an added line and not know it had "
                  + "replaced the line above it.");
            }

            Advance(parser);
            entries[key.Value] = ReadNode(parser, child);
        }

        return new MapNode(entries, path);
    }

    private static SeqNode ReadSeq(IParser parser, string path)
    {
        var items = new List<Node>();

        while (Advance(parser) && parser.Current is not SequenceEnd)
        {
            items.Add(ReadNode(parser, $"{path}[{items.Count}]"));
        }

        return new SeqNode(items, path);
    }

    private static string At(string path) => path.Length == 0 ? "" : $" at '{path}'";

    // ---- the tree, into the closed schema ----

    private static Envelope Map(Node document)
    {
        var root = RequireMap(document, "");
        Closed(root, BasedOnKey, "context", "environment", "environments", "repository",
               "repositories", "accepts", "produces", "instructions", "obligations", "loops",
               "destinations");

        var context = RequireMap(Require(root, "context"), "context");
        Closed(context, "scope", "constitution");

        return new Envelope
        {
            Context = new ContextBinding
            {
                Scope = RequireScalar(Require(context, "scope"), "context.scope"),
                Constitution = RequireScalar(Require(context, "constitution"), "context.constitution"),
            },
            // THE SELECTIONS. Root keys beside context:, not members of it -
            // the context block is what a flight is bound to, a selection is
            // what a flight is ABOUT. Absent stays absent: reading a missing
            // key back as "" would be a different document on disk and the
            // same value to the engine, so show-after-apply would not round
            // trip.
            Environments = BoundOf(root, "environments", "environment"),
            Repositories = BoundOf(root, "repositories", "repository"),
            // AND THE ONE WHOSE EMPTY VALUE MEANS SOMETHING. `accepts: []` is
            // a work kind saying it takes no subject; a missing key is a
            // document that is not a work kind. So absence maps to null and an
            // empty sequence maps to an empty list, and the two never collapse
            // into each other on either side of the round trip.
            Accepts = root.Entries.TryGetValue("accepts", out var accepts)
                ? Strings(accepts, "accepts")
                : null,
            Produces = root.Entries.TryGetValue("produces", out var produces)
                ? Strings(produces, "produces")
                : null,
            // PROVENANCE IS NOT READ BACK, because a document does not get to
            // say where it came from - the composer assigns it, exactly as it
            // does for an obligation. A block is a line of text and nothing
            // else, which is why this is Strings and not a map.
            //
            // Absence and emptiness collapse here on purpose, unlike `accepts`:
            // there is no such thing as a document declaring "no instructions,
            // deliberately", so both read back as an empty list and every
            // envelope written before this field round-trips unchanged.
            Instructions = root.Entries.TryGetValue("instructions", out var instructions)
                ? [.. Strings(instructions, "instructions")
                    .Select(text => new EnvelopeInstruction { Text = text })]
                : [],
            Obligations = [.. Named(root, "obligations").Select(MapObligation)],
            Loops = [.. Named(root, "loops").Select(MapLoop)],
            Destinations = [.. Named(root, "destinations").Select(MapDestination)],
        };
    }

    private static Obligation MapObligation((string Id, MapNode Body) entry)
    {
        Closed(entry.Body, "check", "when", "rule", "approver", "provenance", "evidence");

        // PROVENANCE IS DERIVED, NEVER DECLARED, and refusing it here is the whole
        // of that. The composer assigns the layer from where the document sat; a
        // document that could name its own would let a flight-level file claim
        // `org`, which is the governed thing describing its own authority - the
        // same rule as `no envelope arrives from a runner`.
        //
        // Refused rather than ignored. Silently discarding it would leave people
        // writing a line that looks load-bearing and does nothing, which is how a
        // field becomes folklore.
        if (entry.Body.Entries.ContainsKey("provenance"))
        {
            throw new EnvelopeSyntaxException(
                $"{entry.Body.Path}.provenance is not yours to set. Which layer an obligation "
              + "came from is assigned from where its document sat, so that a lower layer "
              + "cannot claim a higher one's authority. Remove the line; the composed envelope "
              + "will carry it.");
        }

        return new Obligation
        {
            Id = entry.Id,
            Check = RequireScalar(Require(entry.Body, "check"), $"{entry.Body.Path}.check"),
            // OPTIONAL, and absent means always. A condition that is present and
            // unrecognised is refused by Envelope.Validate rather than read as
            // false, because false is the answer that makes the obligation vanish.
            When = entry.Body.Entries.TryGetValue("when", out var when)
                ? RequireScalar(when, $"{entry.Body.Path}.when")
                : null,
            // OPTIONAL HERE, closed by the schema's rule afterwards. A machine
            // check with no rule is refused and a human check with one is refused,
            // and both refusals belong in Envelope.Validate where both repositories
            // read them - not in a parser only this repository has.
            Rule = entry.Body.Entries.TryGetValue("rule", out var rule)
                ? RequireScalar(rule, $"{entry.Body.Path}.rule")
                : null,
            Approver = entry.Body.Entries.TryGetValue("approver", out var approver)
                ? RequireScalar(approver, $"{entry.Body.Path}.approver")
                : null,
            // WHAT THE GATE NEEDS. Absent is nothing declared, which is what every
            // envelope written before this existed means - and an entry naming something
            // the flight cannot produce halts it rather than rendering a blank.
            Evidence = entry.Body.Entries.TryGetValue("evidence", out var evidence)
                ? Strings(evidence, $"{entry.Body.Path}.evidence")
                : [],
            // NOT SET HERE. A parsed document has no layer: it acquires one when a
            // composer places it, and until then `org` is the default the type
            // carries rather than a claim this parser made.
        };
    }

    private static Loop MapLoop((string Id, MapNode Body) entry)
    {
        Closed(entry.Body, "executor", "discharges", "moves", "budget", "on-exhaustion");

        var budget = RequireMap(Require(entry.Body, "budget"), $"{entry.Body.Path}.budget");
        Closed(budget, "wall-clock", "attempts");

        return new Loop
        {
            Id = entry.Id,
            Executor = RequireScalar(Require(entry.Body, "executor"), $"{entry.Body.Path}.executor"),
            Discharges = Strings(Require(entry.Body, "discharges"), $"{entry.Body.Path}.discharges"),
            Moves = Strings(Require(entry.Body, "moves"), $"{entry.Body.Path}.moves"),
            Budget = new LoopBudget
            {
                WallClock = RequireScalar(Require(budget, "wall-clock"), $"{budget.Path}.wall-clock"),
                // ABSENT STAYS ABSENT - null is unbounded, and a number nobody
                // chose would be a termination condition nobody agreed to. This
                // key parsed nowhere while the wire accepted it, which is the
                // evidence: defect through the other door; fixed together.
                Attempts = budget.Entries.TryGetValue("attempts", out var attempts)
                    ? WholeNumber(attempts, $"{budget.Path}.attempts")
                    : null,
            },
            OnExhaustion = RequireScalar(
                Require(entry.Body, "on-exhaustion"), $"{entry.Body.Path}.on-exhaustion"),
        };
    }

    /// <summary>
    /// A boolean, or a refusal naming what was there instead.
    /// </summary>
    /// <remarks>
    /// <b>Two spellings only, and never a coercion.</b> YAML readers accept `yes`,
    /// `on`, `y` and more as truth, and a governance permission read from a
    /// dialect-dependent value is a permission whose meaning depends on which
    /// parser opened the file. So this is `true` or `false` and anything else is a
    /// diagnosis - which matters most in the direction that grants something.
    /// </remarks>
    /// <summary>A positive whole number, or a refusal naming what was there.</summary>
    /// <remarks>
    /// Digits only, no YAML dialect coercion - the same rule as
    /// <see cref="Flag"/>, on the member that bounds how many times work may
    /// re-run.
    /// </remarks>
    private static int WholeNumber(Node node, string path)
    {
        var value = RequireScalar(node, path);

        return int.TryParse(value, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : throw new EnvelopeSyntaxException(
                $"{path} is '{value}', and this reads only a positive whole number of runs.");
    }

    private static bool Flag(Node node, string path) =>
        RequireScalar(node, path) switch
        {
            "true" => true,
            "false" => false,
            var other => throw new EnvelopeSyntaxException(
                $"{path} is '{other}', and this reads only 'true' or 'false'. A permission whose "
              + "meaning depends on which YAML dialect opened the file is not a permission."),
        };

    private static Destination MapDestination((string Id, MapNode Body) entry)
    {
        Closed(entry.Body, "kind", "requires", "preserve-unadmitted", "opens", "may-select");

        return new Destination
        {
            Id = entry.Id,
            Kind = RequireScalar(Require(entry.Body, "kind"), $"{entry.Body.Path}.kind"),
            Requires = Strings(Require(entry.Body, "requires"), $"{entry.Body.Path}.requires"),
            // ABSENT STAYS ABSENT. Reading a missing key back as false would be the
            // same value to the engine and a different document on disk, so
            // `envelope show` after `envelope apply` would not round trip.
            PreserveUnadmitted =
                entry.Body.Entries.TryGetValue("preserve-unadmitted", out var preserve)
                    ? Flag(preserve, $"{entry.Body.Path}.preserve-unadmitted")
                    : null,
            // AND THE SAME FOR THIS ONE. Reading a missing `opens` back as an
            // empty list would turn every pull-request destination into one
            // Validate refuses, on a document nobody edited.
            Opens = entry.Body.Entries.TryGetValue("opens", out var opens)
                ? Strings(opens, $"{entry.Body.Path}.opens")
                : null,
            // AND THE SAME AGAIN. Absent stays absent: a missing `may-select`
            // read back as empty sets would say the tenant permits nothing,
            // which is a different document from one that bounds no selection.
            MaySelect = entry.Body.Entries.TryGetValue("may-select", out var selection)
                ? MapSelection(selection, $"{entry.Body.Path}.may-select")
                : null,
        };
    }

    /// <summary>
    /// A root bound, under either spelling, as a scalar or a sequence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two keys, one value, and it is not the mistake it looks like.</b> The
    /// bound was <c>environment:</c> and single-valued; every document already
    /// written and already stored uses that key, so refusing it would break
    /// every envelope ever applied. The plural is what a document declaring
    /// several uses. This is the same arrangement the intent read has for two
    /// spellings of one projection: both exist in real data.
    /// </para>
    /// <para>
    /// <b>Both at once is refused rather than resolved.</b> A precedence would
    /// mean the document says one thing and the engine reads another, and
    /// whichever we picked would be wrong for somebody.
    /// </para>
    /// <para>
    /// A scalar under either key is one permitted name. Absent stays absent:
    /// null is unbounded, and reading a missing key back as an empty set would
    /// turn every unbounded document into one Validate refuses.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<string>? BoundOf(MapNode root, string plural, string singular)
    {
        var hasPlural = root.Entries.TryGetValue(plural, out var many);
        var hasSingular = root.Entries.TryGetValue(singular, out var one);

        if (hasPlural && hasSingular)
        {
            throw new EnvelopeSyntaxException(
                $"'{plural}' and '{singular}' are both declared, and they are two spellings of "
              + $"one bound. Keep '{plural}'; '{singular}' is the older name and reading both "
              + "would mean the document says one thing and the engine another.");
        }

        if (hasPlural)
        {
            return many is ScalarNode ? [RequireScalar(many!, plural)] : Strings(many!, plural);
        }

        return hasSingular ? [RequireScalar(one!, singular)] : null;
    }

    /// <summary>The sets a destination permits a nomination to select from.</summary>
    /// <remarks>
    /// Both keys optional and both absent-is-absent, for the reason the two
    /// above are: an empty set says the tenant permits nothing and null says
    /// they bounded nothing, and those render different menus.
    /// </remarks>
    private static DestinationSelection MapSelection(Node node, string path)
    {
        var body = RequireMap(node, path);
        Closed(body, "environments", "repositories");

        return new DestinationSelection
        {
            Environments = body.Entries.TryGetValue("environments", out var environments)
                ? Strings(environments, $"{path}.environments")
                : null,
            Repositories = body.Entries.TryGetValue("repositories", out var repositories)
                ? Strings(repositories, $"{path}.repositories")
                : null,
        };
    }

    /// <summary>
    /// Every key this map may have, and no others.
    /// </summary>
    /// <remarks>
    /// CLOSED, and that is the point. A misspelled <c>chek: machine</c> must
    /// not produce an obligation with no checker - an obligation nothing
    /// evaluates reports satisfied by never running, which is Article XI's
    /// worst failure reached by a typo. So the diagnosis names what was found
    /// AND what was expected: one of those tells somebody there is a problem,
    /// and both together tell them what to type.
    /// </remarks>
    /// <summary>The one spelling of the consumed key.</summary>
    /// <remarks>
    /// Named once because it is admitted in three closed sets and read in three
    /// mappers, and a key spelled six times is a key spelled five ways
    /// eventually.
    /// </remarks>
    private const string BasedOnKey = "based-on";

    /// <summary>
    /// Reads the precondition out of a document root, if it states one.
    /// </summary>
    /// <remarks>
    /// <b>Consumed rather than mapped.</b> It is admitted by the closed key set
    /// so it is not refused as unknown, read here so the applier can state it,
    /// and never written into the model - which is the whole of the third class.
    /// </remarks>
    private static string? Consumed(Node document) =>
        document is MapNode root && root.Entries.TryGetValue(BasedOnKey, out var stated)
            ? RequireScalar(stated, BasedOnKey)
            : null;

    private static void Closed(MapNode map, params string[] allowed)
    {
        foreach (var key in map.Entries.Keys)
        {
            if (!allowed.Contains(key, StringComparer.Ordinal))
            {
                throw new EnvelopeSyntaxException(
                    $"Unknown key '{key}'{At(map.Path)}. Expected one of: "
                  + string.Join(", ", allowed) + ".");
            }
        }
    }

    private static Node Require(MapNode map, string key) =>
        map.Entries.TryGetValue(key, out var value)
            ? value
            : throw new EnvelopeSyntaxException(
                $"'{key}' is missing{At(map.Path)}, and an envelope without it governs nothing.");

    private static MapNode RequireMap(Node node, string path) =>
        node as MapNode
        ?? throw new EnvelopeSyntaxException($"'{path}' should be a block of keys.");

    private static string RequireScalar(Node node, string path) =>
        (node as ScalarNode)?.Value
        ?? throw new EnvelopeSyntaxException($"'{path}' should be a single value.");

    private static IReadOnlyList<string> Strings(Node node, string path) =>
        node is SeqNode sequence
            ? [.. sequence.Items.Select(item => RequireScalar(item, path))]
            : throw new EnvelopeSyntaxException($"'{path}' should be a list.");

    /// <summary>
    /// The named children of a map, as (name, body) pairs.
    /// </summary>
    /// <remarks>
    /// Obligations, loops and destinations are written as maps keyed by name
    /// and held as lists carrying an id. The text form reads better keyed; the
    /// model is easier to reason about as a list, and neither has to win.
    /// </remarks>
    private static IEnumerable<(string Id, MapNode Body)> Named(MapNode root, string key)
    {
        var map = RequireMap(Require(root, key), key);

        foreach (var (id, body) in map.Entries)
        {
            yield return (id, RequireMap(body, $"{key}.{id}"));
        }
    }

    // ---- what the round trip did not keep ----

    /// <summary>
    /// Whether the text had comments in it, so somebody can be told.
    /// </summary>
    /// <remarks>
    /// A second pass with the scanner rather than a scan for '#', because '#'
    /// inside a quoted scalar is not a comment and a note that fired on one
    /// would be a note people learn to ignore.
    /// </remarks>
    private static IReadOnlyList<string> Notes(string text)
    {
        var scanner = new Scanner(new StringReader(text), skipComments: false);

        try
        {
            while (scanner.MoveNext())
            {
                if (scanner.Current is YamlDotNet.Core.Tokens.Comment)
                {
                    return
                    [
                        "Comments are not kept. The envelope is stored as a model and rendered back "
                      + "by gg envelope show, so the next round trip will not have them. Put prose "
                      + "somewhere it survives.",
                    ];
                }
            }
        }
        catch (YamlException)
        {
            // The document already parsed. A scanner disagreeing about it is
            // not something to report to somebody who asked about their
            // envelope.
        }

        return [];
    }
}
