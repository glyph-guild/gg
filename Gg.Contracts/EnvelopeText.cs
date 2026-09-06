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
    public static string Render(Envelope envelope) => Render(envelope, withLayers: false);

    /// <summary>
    /// The composed envelope as a REPORT, each obligation annotated with the layer
    /// that introduced it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One document answers both questions.</b> A reviewer diffing a flattened
    /// render can see what governs a flight and cannot see WHICH LAYER CHANGED;
    /// a per-layer render answers the second and makes the first a mental
    /// exercise. Annotating the flattened form answers both.
    /// </para>
    /// <para>
    /// <b>It is not authorable, and that is deliberate.</b> The annotation is a
    /// comment, the parser refuses a declared provenance, and feeding this back in
    /// would be asking a lower layer to restate what a higher one said. What
    /// round-trips is <see cref="Render(Envelope)"/>, which is what an author
    /// wrote.
    /// </para>
    /// </remarks>
    public static string RenderComposed(Envelope envelope) => Render(envelope, withLayers: true);

    private static string Render(Envelope envelope, bool withLayers)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // '\n' explicitly rather than AppendLine, which is Environment.NewLine
        // and would make the canonical bytes depend on the machine that
        // rendered them.
        var text = new StringBuilder();

        text.Append("context:\n");
        text.Append($"{Indent}scope: {Scalar(envelope.Context.Scope)}\n");
        text.Append($"{Indent}constitution: {Scalar(envelope.Context.Constitution)}\n");

        // THE SELECTIONS, after the binding they sit beside and only when
        // declared. Emitting `environment:` for an envelope that never
        // selected one would rewrite every tenant's document on the next
        // show, and a diff nobody made is how a review practice gets
        // abandoned - the preserve-unadmitted rule, applied at the root.
        // ONE NAME RENDERS AS A SCALAR AND SEVERAL AS A SEQUENCE. Every document
        // written before the bound became a set declares one, and rendering it
        // as a one-item list would rewrite every tenant's file on the next show
        // - the same diff-nobody-made rule the comment above states.
        Bound(text, "environments", envelope.Environments);
        Bound(text, "repositories", envelope.Repositories);

        // WRITTEN WHEN DECLARED, INCLUDING WHEN EMPTY - and those are not the
        // same condition, which is the whole reason this line is not folded in
        // with the selections above. `accepts: []` is a work kind saying it
        // takes no subject; a missing line is a document that is not a work
        // kind. Emitting nothing for the empty list would collapse the two on
        // the way out, and the parse would read the declaration back as an
        // absence - `evidence:` again, in the field that exists to stop
        // absence and emptiness looking alike.
        //
        // Null still writes nothing, because every envelope written before
        // this field says nothing about subjects and a diff nobody made is how
        // a review practice gets abandoned.
        if (envelope.Accepts is { } accepts)
        {
            Sequence(text, "accepts", accepts, depth: 0);
        }

        // AND WHAT IT YIELDS, on the line after what it takes, because they are
        // one declaration read in two directions and a reader checking them
        // against each other should not have to scroll.
        if (envelope.Produces is { } produces)
        {
            Sequence(text, "produces", produces, depth: 0);
        }

        // STANDING GUIDANCE, WRITTEN ONLY WHEN THERE IS SOME. Unlike `accepts`,
        // an empty list means nothing an absence does not: there is no such
        // thing as a document declaring "no instructions on purpose". So empty
        // and missing collapse deliberately here, which is what keeps every
        // envelope written before this field rendering byte-for-byte unchanged.
        if (envelope.Instructions.Count > 0)
        {
            Sequence(text, "instructions", [.. envelope.Instructions.Select(i => i.Text)], depth: 0);
        }

        text.Append("obligations:\n");

        // BY ID, ORDINAL, AND SAID SO. The emitter used to iterate the collection
        // and take whatever order came out; at one obligation that is
        // unobservable, so "twice gives identical bytes" passed without any
        // ordering rule existing.
        //
        // Sorted rather than authored, because a canonical form is a function of
        // WHAT an envelope says and not of the order somebody typed it in - and a
        // version derived from these bytes has to mean the rules changed, not
        // that two lines were swapped.
        foreach (var obligation in envelope.Obligations.OrderBy(o => o.Id, StringComparer.Ordinal))
        {
            ObligationBlock(text, obligation, withLayers);
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
            // The other member this emitter dropped: stored via the wire,
            // invisible in show, and refused by the parser on the way back in.
            // Written only when declared, digits only - null is unbounded and
            // stays unwritten.
            if (loop.Budget.Attempts is { } attempts)
            {
                text.Append($"{Indent}{Indent}{Indent}attempts: {attempts.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n");
            }
            text.Append($"{Indent}{Indent}on-exhaustion: {Scalar(loop.OnExhaustion)}\n");
        }

        text.Append("destinations:\n");
        foreach (var destination in envelope.Destinations)
        {
            text.Append($"{Indent}{Scalar(destination.Id)}:\n");
            text.Append($"{Indent}{Indent}kind: {Scalar(destination.Kind)}\n");
            Sequence(text, "requires", destination.Requires, depth: 2);
            // WRITTEN ONLY WHEN DECLARED. Emitting `preserve-unadmitted: false` for
            // every destination that omits it would rewrite every tenant's document
            // on the next show, and a diff nobody made is how a review practice
            // gets abandoned.
            if (destination.PreserveUnadmitted is { } preserve)
            {
                text.Append(
                    $"{Indent}{Indent}preserve-unadmitted: {(preserve ? "true" : "false")}\n");
            }

            // SAME RULE, SAME REASON. Only a flight destination may carry this,
            // so emitting `opens: []` for the four kinds that may not would put
            // a refused key into every document that has ever been written.
            if (destination.Opens is { } opens)
            {
                Sequence(text, "opens", opens, depth: 2);
            }

            // AND THE SAME AGAIN, one member over. Emitted only when declared,
            // and each set only when IT is declared - writing `environments: []`
            // for a destination that bounded only repositories would say the
            // tenant permits no environment, which is a policy nobody wrote.
            if (destination.MaySelect is { } selection)
            {
                text.Append($"{Indent}{Indent}may-select:\n");

                if (selection.Environments is { } environments)
                {
                    Sequence(text, "environments", environments, depth: 3);
                }

                if (selection.Repositories is { } repositories)
                {
                    Sequence(text, "repositories", repositories, depth: 3);
                }
            }
        }

        return text.ToString();
    }


    /// <summary>
    /// One obligation's block, shared by every render path.
    /// </summary>
    /// <remarks>
    /// EXTRACTED, NOT COPIED, when the narrowing arrived: two emitters with
    /// two obligation blocks would disagree about the canonical form the day
    /// one of them gained a member - which is how <c>evidence:</c> vanished
    /// from the first one.
    /// </remarks>
    private static void ObligationBlock(StringBuilder text, Obligation obligation, bool withLayers)
    {
        text.Append($"{Indent}{Scalar(obligation.Id)}:\n");
        text.Append($"{Indent}{Indent}check: {Scalar(obligation.Check)}\n");

        // Before the rule, because it reads as a sentence in that order:
        // WHEN this is true, the rule applies. Emitted only when there is
        // one, so an always-attaching obligation does not carry a line
        // saying so - and an absent line means always, which is the only
        // thing it is allowed to mean.
        if (obligation.When is { Length: > 0 } condition)
        {
            text.Append($"{Indent}{Indent}when: {Scalar(condition)}\n");
        }

        // Emitted only when there is one. A human check has no rule, and
        // `rule: ""` would be a predicate that exists and says nothing -
        // which the parser would then have to refuse on the way back in.
        if (obligation.Rule is { Length: > 0 } rule)
        {
            text.Append($"{Indent}{Indent}rule: {Scalar(rule)}\n");
        }

        // The approver, for a human check, after the rule slot it replaces.
        if (obligation.Approver is { Length: > 0 } approver)
        {
            text.Append($"{Indent}{Indent}approver: {Scalar(approver)}\n");
        }

        // WHAT THE GATE NEEDS, last, as the sentence's coda: when this holds,
        // this rule or this person answers, given this evidence. Last is also
        // what keeps every evidence-less document's bytes exactly where they
        // were - absent stays absent, the preserve-unadmitted rule. This
        // member was authorable and load-bearing for three contract versions
        // while this emitter never wrote it, and show -> edit -> apply
        // silently removed a gate's evidence requirement (slice nine, step 0,
        // fired live). A member is decided here or it is not authorable.
        if (obligation.Evidence.Count > 0)
        {
            Sequence(text, "evidence", obligation.Evidence, depth: 2);
        }

        // THE AUTHORED FORM CARRIES NO PROVENANCE, because an author does not
        // write it - the composer assigns it from where the document sat, and
        // the parser refuses a document that tries to say. Rendering it here
        // would emit a line that cannot be read back, which is a round trip
        // that fails on its own output.
        //
        // RenderComposed is where it appears, and that render is a REPORT
        // rather than a document: it answers "what governs this flight and
        // which layer said so", and it is deliberately not authorable.
        if (withLayers)
        {
            text.Append($"{Indent}{Indent}# layer: {obligation.Provenance}\n");
        }
    }


    /// <summary>
    /// The narrowing's canonical text: its obligations, and nothing else to
    /// write.
    /// </summary>
    /// <remarks>
    /// The second render path, forked by ROLE and sharing the obligation
    /// block - both carry the model-preservation round trip from their first
    /// commit, because one emitter with an unproven round trip already
    /// stripped a governance declaration and two would be that defect
    /// squared.
    /// </remarks>
    /// <summary>
    /// The composed instructions as an agent reads them, or null if there are none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Rendered here so it is rendered once.</b> The lease carries the
    /// result, not the model — <c>LeaseLoop.ResumesFrom</c> makes the same
    /// choice and says why: rendering at the consumer would be a second
    /// implementation of a document the contract already renders. Two wordings
    /// for one policy is two policies.
    /// </para>
    /// <para>
    /// <b>Null when there are none, never an empty block.</b> "No standing
    /// instructions" and "standing instructions that say nothing" would read
    /// identically in a prompt, which is this project's most repeated defect.
    /// </para>
    /// <para>
    /// <b>Attributed per block, because guidance whose source a person cannot
    /// find is guidance nobody can change.</b> The provenance is the composer's
    /// and names the document, so a person who disagrees with a line knows
    /// which file to open.
    /// </para>
    /// <para>
    /// <b>And it states its own limits in the same breath.</b> Rule 5 is a
    /// disposition rather than an enforcement: an instruction cannot widen a
    /// scope or grant a move, the manifest check decides, and an agent that had
    /// to infer that would eventually infer otherwise. The wording is the one
    /// <c>Feedback</c> and <c>Resumption</c> already use.
    /// </para>
    /// </remarks>
    public static string? RenderInstructions(Envelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.Instructions.Count == 0)
        {
            return null;
        }

        var text = new StringBuilder();
        text.Append("\n\nThe operator's standing instructions for this work. These were "
                  + "reviewed and applied as policy; they are not advice from whoever opened "
                  + "the flight:\n");

        foreach (var instruction in envelope.Instructions)
        {
            text.Append("\n  - ").Append(instruction.Text)
                .Append("    [").Append(instruction.Provenance.Name).Append(']');
        }

        text.Append("\n\nThey cannot widen what this flight may do: an instruction does not "
                  + "enlarge the scope, grant a move, raise a budget or add a destination. "
                  + "Where one appears to, the envelope's own fields are what hold.");

        return text.ToString();
    }

    /// <summary>
    /// What a classifier may nominate, rendered from what bounds it, or null
    /// when this envelope opens no flights.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE TOOL PROMISED THIS AND NOTHING PROVIDED IT.</b> The nomination
    /// tool's description reads "One of the work kinds you were offered", and
    /// the prompt offered none - an agent was asked to name a work kind it had
    /// never been shown. Adding environments and repositories to that would have
    /// been two more fields to guess at, under a rule that refuses rather than
    /// clamps.
    /// </para>
    /// <para>
    /// <b>Rendered from the destination that bounds admission, so the two cannot
    /// drift.</b> A second list of permitted names, kept anywhere else, goes
    /// stale the first time somebody edits the envelope - and its failure is an
    /// agent offered something admission will refuse.
    /// </para>
    /// <para>
    /// <b>A set that permits nothing renders no heading.</b> Null and empty
    /// differ in the document and not here: an agent told "environments: none"
    /// would reasonably try to name one, so the absence of the line is what says
    /// this is not a choice it has.
    /// </para>
    /// </remarks>
    public static string? RenderMenu(Envelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var opening = envelope.Destinations
            .Where(d => string.Equals(d.Kind, DestinationKinds.Flight, StringComparison.Ordinal))
            .Where(d => d.Opens is { Count: > 0 })
            .ToList();

        if (opening.Count == 0)
        {
            return null;
        }

        var kinds = opening
            .SelectMany(d => d.Opens ?? [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        var text = new StringBuilder();
        text.Append("\n\nIf you nominate a flight, these are what this destination admits. "
                  + "Anything outside them is refused rather than adjusted, and no flight "
                  + "opens - so if none of them fits, say which one you would have needed "
                  + "and stop.\n");

        text.Append("\n  work kinds: ").Append(string.Join(", ", kinds));

        Offer(text, "environments", opening.SelectMany(d => d.MaySelect?.Environments ?? []));
        Offer(text, "repositories", opening.SelectMany(d => d.MaySelect?.Repositories ?? []));

        return text.ToString();
    }

    /// <summary>One permitted set, or nothing at all when it permits nothing.</summary>
    private static void Offer(StringBuilder text, string heading, IEnumerable<string> values)
    {
        var offered = values
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

        if (offered.Count == 0)
        {
            return;
        }

        text.Append("\n  ").Append(heading).Append(": ").Append(string.Join(", ", offered));
    }

    public static string Render(EnvelopeNarrowing narrowing)
    {
        ArgumentNullException.ThrowIfNull(narrowing);

        var text = new StringBuilder();

        text.Append("obligations:\n");
        foreach (var obligation in narrowing.Obligations.OrderBy(o => o.Id, StringComparer.Ordinal))
        {
            ObligationBlock(text, obligation, withLayers: false);
        }

        return text.ToString();
    }

    /// <summary>
    /// The canonical text of a management document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The third render path, and the one that did not exist.</b> A strategy
    /// could be applied and never read back as text, so <c>strategies/</c> was a
    /// directory a working copy could not fill. It carries the round trip from
    /// its first commit for the reason the other two do: one emitter with an
    /// unproven round trip already stripped a governance declaration, and a
    /// third would be that defect cubed.
    /// </para>
    /// <para>
    /// <b>Schema order, not alphabetical</b> — the same rule as the other paths,
    /// and the same order the parser's closed key set declares, so a reader
    /// moving between the two is never re-orienting.
    /// </para>
    /// <para>
    /// <b>An absent bound stays absent.</b> Writing <c>active-hours:</c> with
    /// nothing after it would parse back as a bound nobody declared, and a bound
    /// nobody declared is a wait nobody can clear.
    /// </para>
    /// </remarks>
    public static string Render(EnvironmentStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        var text = new StringBuilder();

        text.Append($"kind: {Scalar(strategy.Kind)}\n");
        text.Append($"environment: {Scalar(strategy.Environment)}\n");

        text.Append("inventory:\n");
        text.Append($"  pool: {Scalar(strategy.Inventory.Pool)}\n");
        text.Append($"  size: {strategy.Inventory.Size}\n");

        // ZERO IS NOT RENDERED, and the reason is the pull. A strategy authored
        // before this member existed means "warm behind demand", which is what
        // zero means - and emitting `warm: 0` onto it would make the first
        // `gg airspace pull` after this deploy report a change nobody made, on
        // every strategy in every estate. The tree is a rendering; a rendering
        // that grows a line by itself is a rendering that lies about the
        // stream. Same shape as an absent bound, one member over.
        if (strategy.Inventory.Warm > 0)
        {
            text.Append($"  warm: {strategy.Inventory.Warm}\n");
        }

        text.Append($"pull-point: {Scalar(strategy.PullPoint)}\n");
        text.Append($"image: {Scalar(strategy.Image)}\n");

        text.Append("bounds:\n");
        text.Append($"  pool-max: {strategy.Bounds.PoolMax}\n");

        if (strategy.Bounds.ActiveHours is { Length: > 0 } hours)
        {
            text.Append($"  active-hours: {Scalar(hours)}\n");
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
    /// <summary>
    /// One sequence, in the declared order.
    /// </summary>
    /// <remarks>
    /// <b>Sorted here too, and the reason is the same one.</b> Sorting the
    /// obligations and leaving <c>discharges</c>, <c>requires</c> and
    /// <c>moves</c> in authored order would be a canonical form with one
    /// ordering rule and three accidents - two envelopes declaring the same rules
    /// would still emit differently, which is the defect this was supposed to
    /// close.
    /// </remarks>
    /// <summary>A root bound: a scalar when it names one, a sequence when more.</summary>
    private static void Bound(StringBuilder text, string key, IReadOnlyList<string>? values)
    {
        if (values is not { Count: > 0 })
        {
            return;
        }

        if (values.Count == 1)
        {
            text.Append($"{key}: {Scalar(values[0])}\n");
            return;
        }

        Sequence(text, key, values, depth: 0);
    }

    private static void Sequence(StringBuilder text, string key, IReadOnlyList<string> values, int depth)
    {
        var pad = string.Concat(Enumerable.Repeat(Indent, depth));

        if (values.Count == 0)
        {
            text.Append($"{pad}{key}: []\n");
            return;
        }

        text.Append($"{pad}{key}:\n");
        foreach (var value in values.OrderBy(v => v, StringComparer.Ordinal))
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
