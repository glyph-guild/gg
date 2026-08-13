using Gg.Contracts;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Gg.Client;

/// <summary>What reading an envelope produced.</summary>
public sealed record EnvelopeParse
{
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

        return new EnvelopeParse { Envelope = envelope, Notes = Notes(text) };
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
        Closed(root, "context", "obligations", "loops", "destinations");

        var context = RequireMap(Require(root, "context"), "context");
        Closed(context, "scope", "constitution");

        return new Envelope
        {
            Context = new ContextBinding
            {
                Scope = RequireScalar(Require(context, "scope"), "context.scope"),
                Constitution = RequireScalar(Require(context, "constitution"), "context.constitution"),
            },
            Obligations = [.. Named(root, "obligations").Select(MapObligation)],
            Loops = [.. Named(root, "loops").Select(MapLoop)],
            Destinations = [.. Named(root, "destinations").Select(MapDestination)],
        };
    }

    private static Obligation MapObligation((string Id, MapNode Body) entry)
    {
        Closed(entry.Body, "check", "when", "rule", "approver", "provenance");

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
            Provenance = entry.Body.Entries.TryGetValue("provenance", out var provenance)
                ? RequireScalar(provenance, $"{entry.Body.Path}.provenance")
                : ObligationProvenances.Org,
        };
    }

    private static Loop MapLoop((string Id, MapNode Body) entry)
    {
        Closed(entry.Body, "executor", "discharges", "moves", "budget", "on-exhaustion");

        var budget = RequireMap(Require(entry.Body, "budget"), $"{entry.Body.Path}.budget");
        Closed(budget, "wall-clock");

        return new Loop
        {
            Id = entry.Id,
            Executor = RequireScalar(Require(entry.Body, "executor"), $"{entry.Body.Path}.executor"),
            Discharges = Strings(Require(entry.Body, "discharges"), $"{entry.Body.Path}.discharges"),
            Moves = Strings(Require(entry.Body, "moves"), $"{entry.Body.Path}.moves"),
            Budget = new LoopBudget
            {
                WallClock = RequireScalar(Require(budget, "wall-clock"), $"{budget.Path}.wall-clock"),
            },
            OnExhaustion = RequireScalar(
                Require(entry.Body, "on-exhaustion"), $"{entry.Body.Path}.on-exhaustion"),
        };
    }

    private static Destination MapDestination((string Id, MapNode Body) entry)
    {
        Closed(entry.Body, "kind", "requires");

        return new Destination
        {
            Id = entry.Id,
            Kind = RequireScalar(Require(entry.Body, "kind"), $"{entry.Body.Path}.kind"),
            Requires = Strings(Require(entry.Body, "requires"), $"{entry.Body.Path}.requires"),
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
