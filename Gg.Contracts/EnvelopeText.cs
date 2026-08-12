using System.Text;

namespace Gg.Contracts;

/// <summary>
/// The envelope's canonical text form: model to YAML, deterministically.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hand-written, and it can be.</b> The schema is closed - maps, lists and
/// scalars, no cycles, no polymorphism - so emitting it is a few hundred
/// characters rather than a dependency. That is what lets the schema live in
/// the assembly a customer audits, which takes no third-party package.
/// </para>
/// <para>
/// <b>Shared rather than client-side</b>, because a web editor will eventually
/// render this and because <c>show</c> output has to be byte-stable to diff.
/// One emitter, one output, everywhere.
/// </para>
/// <para>
/// <b>Comments are not preserved.</b> The stored thing is the model; this
/// renders it. Anyone who puts a comment in loses it on the next round trip,
/// which is why the schema carries <c>description</c>-shaped fields where
/// prose belongs rather than comment-preservation machinery. Same shape as
/// <c>terraform fmt</c>: a normalised form emitted, a superset accepted.
/// </para>
/// <para>
/// <b>The output is a normalised subset on purpose.</b> Block style
/// throughout, no anchors, no aliases, no flow collections, one document. Each
/// of those is a second way to write something this already writes one way,
/// and a canonical form has one way.
/// </para>
/// </remarks>
public static class EnvelopeText
{
    /// <summary>Two spaces, everywhere. The one indent this form uses.</summary>
    private const string Indent = "  ";

    /// <summary>The envelope as canonical YAML, ending in exactly one newline.</summary>
    /// <remarks>
    /// Key order is the SCHEMA's order rather than alphabetical, because the
    /// schema's order is the order somebody reads it in - context before the
    /// obligations that use it, obligations before the loops that discharge
    /// them. Either rule is stable; only one of them reads like a document.
    /// </remarks>
    public static string Render(Envelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // '\n' explicitly rather than AppendLine, which is Environment.NewLine
        // and would make the canonical bytes depend on the machine that
        // rendered them.
        var text = new StringBuilder();

        text.Append("context:\n");
        text.Append($"{Indent}scope: {Scalar(envelope.Context.Scope)}\n");
        text.Append($"{Indent}constitution: {Scalar(envelope.Context.Constitution)}\n");

        text.Append("obligations:\n");
        foreach (var obligation in envelope.Obligations)
        {
            text.Append($"{Indent}{Scalar(obligation.Id)}:\n");
            text.Append($"{Indent}{Indent}check: {Scalar(obligation.Check)}\n");
            text.Append($"{Indent}{Indent}rule: {Scalar(obligation.Rule)}\n");
            text.Append($"{Indent}{Indent}provenance: {Scalar(obligation.Provenance)}\n");
        }

        text.Append("loops:\n");
        foreach (var loop in envelope.Loops)
        {
            text.Append($"{Indent}{Scalar(loop.Id)}:\n");
            text.Append($"{Indent}{Indent}executor: {Scalar(loop.Executor)}\n");
            Sequence(text, "discharges", loop.Discharges, depth: 2);
            Sequence(text, "moves", loop.Moves, depth: 2);
            text.Append($"{Indent}{Indent}budget:\n");
            text.Append($"{Indent}{Indent}{Indent}wall-clock: {Scalar(loop.Budget.WallClock)}\n");
            text.Append($"{Indent}{Indent}on-exhaustion: {Scalar(loop.OnExhaustion)}\n");
        }

        text.Append("destinations:\n");
        foreach (var destination in envelope.Destinations)
        {
            text.Append($"{Indent}{Scalar(destination.Id)}:\n");
            text.Append($"{Indent}{Indent}kind: {Scalar(destination.Kind)}\n");
            Sequence(text, "requires", destination.Requires, depth: 2);
        }

        return text.ToString();
    }

    /// <summary>
    /// A block sequence, never a flow one.
    /// </summary>
    /// <remarks>
    /// An empty list is written as an empty flow sequence, which is the one
    /// place flow style is unavoidable: block style has no way to say "a list
    /// with nothing in it", and omitting the key entirely would make an empty
    /// list and a missing one the same document.
    /// </remarks>
    private static void Sequence(StringBuilder text, string key, IReadOnlyList<string> values, int depth)
    {
        var pad = string.Concat(Enumerable.Repeat(Indent, depth));

        if (values.Count == 0)
        {
            text.Append($"{pad}{key}: []\n");
            return;
        }

        text.Append($"{pad}{key}:\n");
        foreach (var value in values)
        {
            text.Append($"{pad}{Indent}- {Scalar(value)}\n");
        }
    }

    /// <summary>
    /// A scalar, quoted whenever a reader could turn it into something else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Norway problem and its relatives, handled at the point of emission
    /// so the canonical form is safe in a parser that is not ours. Our own
    /// reader never coerces - it works at the event layer, where a scalar is a
    /// string and a style - but this file is going to be opened by editors, web
    /// forms and whatever a customer's CI uses, and every one of those will
    /// read <c>1.10</c> as a float and hand back <c>1.1</c>.
    /// </para>
    /// <para>
    /// Quoting more than strictly necessary is deliberate. The alternative is a
    /// minimal-quoting rule that has to be exactly right about YAML's implicit
    /// typing, which is a large surface to be exactly right about, and being
    /// wrong once changes what governs work.
    /// </para>
    /// </remarks>
    private static string Scalar(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        return NeedsQuoting(value) ? Quote(value) : value;
    }

    /// <summary>
    /// An ALLOW-LIST, and that direction is the whole design.
    /// </summary>
    /// <remarks>
    /// <para>
    /// YAML's rules for which plain scalars are safe are large, positional and
    /// full of exceptions, and a deny-list has to be exactly right about all of
    /// them. Being wrong once changes what governs work. An allow-list has to
    /// be right about one small set instead, and every mistake it makes is in
    /// the direction of an extra pair of quotes.
    /// </para>
    /// <para>
    /// What survives unquoted is deliberately what this schema is made of:
    /// identifiers like <c>in-scope</c>, <c>run-tests</c> and
    /// <c>handoff-to-human</c>. What gets quoted is everything that is not one
    /// - globs, versions, durations - which is exactly the set somebody would
    /// have quoted by hand anyway.
    /// </para>
    /// </remarks>
    private static bool NeedsQuoting(string value)
    {
        // Words YAML resolves to a bool or a null whatever else is true of
        // them. Checked first because they are otherwise perfectly ordinary
        // identifiers.
        foreach (var reserved in (string[])
                 ["true", "false", "yes", "no", "on", "off", "null", "y", "n"])
        {
            if (string.Equals(value, reserved, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // A letter, then letters, digits and the three joiners identifiers
        // actually use. Anything else - a leading digit, a glob, a colon, a
        // space, a control character - is quoted.
        if (!char.IsAsciiLetter(value[0]))
        {
            return true;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Double quotes, with the two escapes that form requires.
    /// </summary>
    /// <remarks>
    /// Double rather than single, because single-quoted YAML cannot express a
    /// control character at all and this has to be able to round-trip anything
    /// the model holds.
    /// </remarks>
    private static string Quote(string value)
    {
        var text = new StringBuilder("\"");

        foreach (var character in value)
        {
            switch (character)
            {
                case '"': text.Append("\\\""); break;
                case '\\': text.Append("\\\\"); break;
                case '\n': text.Append("\\n"); break;
                case '\r': text.Append("\\r"); break;
                case '\t': text.Append("\\t"); break;
                default:
                    if (char.IsControl(character))
                    {
                        text.Append("\\x").Append(((int)character).ToString("x2",
                            System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        text.Append(character);
                    }
                    break;
            }
        }

        return text.Append('"').ToString();
    }
}
