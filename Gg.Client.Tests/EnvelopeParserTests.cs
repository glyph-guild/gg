using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Text to model, and the four ways a YAML reader will otherwise change what
/// governs work.
/// </summary>
/// <remarks>
/// <para>
/// <b>The error messages are the deliverable, not the grammar.</b> The grammar
/// comes from a library. <i>"Unknown key 'chek' at obligations.in-scope"</i> is
/// the difference between a schema somebody adopts and one they abandon after
/// the second time it said "invalid document".
/// </para>
/// <para>
/// <b>The parser refuses a superset of what the emitter writes.</b> Flow style
/// and unquoted identifiers are accepted, because a person hand-writing an
/// envelope should not have to guess our canonical form. What is refused is
/// only what is ambiguous or hidden.
/// </para>
/// <para>
/// This lives in <c>gg</c> and only in <c>gg</c>. Every YAML library is a
/// package reference; the control plane holds none, so YAML's attack surface -
/// billion laughs, anchor expansion, type coercion - never reaches the service
/// that holds the platform's own signing keys.
/// </para>
/// </remarks>
public class EnvelopeParserTests
{
    private const string Valid = """
        context:
          scope: "src/**"
          constitution: "1.0.0"
        obligations:
          in-scope:
            check: machine
            rule: no-file-outside-scope
        loops:
          implement:
            executor: frontier
            discharges:
              - in-scope
            moves:
              - read
              - edit
              - run-tests
              - search
            budget:
              wall-clock: "30m"
            on-exhaustion: handoff-to-human
        destinations:
          pull-request:
            kind: pull-request
            requires:
              - in-scope
        """;

    [Test]
    public async Task The_canonical_form_parses_back_to_the_model_it_came_from()
    {
        var parsed = EnvelopeYaml.Parse(Valid);

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.Envelope).IsNotNull();

        var envelope = parsed.Envelope!;
        await Assert.That(envelope.Context.Scope).IsEqualTo("src/**");
        await Assert.That(envelope.Obligations.Single().Id).IsEqualTo("in-scope");
        await Assert.That(envelope.Loops.Single().Discharges).IsEquivalentTo((string[])["in-scope"]);
        await Assert.That(envelope.Loops.Single().Budget.WallClock).IsEqualTo("30m");
        await Assert.That(envelope.Destinations.Single().Kind).IsEqualTo("pull-request");
    }

    [Test]
    public async Task Apply_round_trips_the_model_without_loss()
    {
        // The property `gg envelope apply` rests on: what a person edits and
        // sends is what comes back out of `show`.
        var parsed = EnvelopeYaml.Parse(Valid);

        await Assert.That(EnvelopeText.Render(parsed.Envelope!))
            .IsEqualTo(EnvelopeText.Render(EnvelopeYaml.Parse(EnvelopeText.Render(parsed.Envelope!)).Envelope!));
    }

    // ---- hazard one: an unknown key ----

    [Test]
    public async Task An_unknown_key_is_refused_and_named_with_its_path()
    {
        // Proven with a MISSPELLING, because that is how it will actually
        // arrive. A parser that ignored this would produce an obligation with
        // no checker, which reports satisfied by never running - Article XI's
        // worst case, reached by a typo.
        var parsed = EnvelopeYaml.Parse(Valid.Replace("check: machine", "chek: machine", StringComparison.Ordinal));

        await Assert.That(parsed.Envelope).IsNull();
        await Assert.That(parsed.Diagnosis).Contains("chek");
        await Assert.That(parsed.Diagnosis).Contains("obligations.in-scope")
            .Because("the path is what turns a refusal into something somebody can act on.");
        await Assert.That(parsed.Diagnosis).Contains("check")
            .Because("naming what was expected is most of the value of naming what was found.");
    }

    [Test]
    public async Task An_unknown_top_level_key_is_refused_and_named()
    {
        var parsed = EnvelopeYaml.Parse(Valid + "\ngates:\n  approve: somebody\n");

        await Assert.That(parsed.Diagnosis).Contains("gates");
    }

    [Test]
    public async Task A_missing_key_is_refused_and_named()
    {
        // The other direction. An envelope missing its scope would otherwise
        // parse into an obligation reading an empty glob, which matches
        // nothing and therefore never fails.
        var parsed = EnvelopeYaml.Parse(Valid.Replace("  scope: \"src/**\"\n", "", StringComparison.Ordinal));

        await Assert.That(parsed.Envelope).IsNull();
        await Assert.That(parsed.Diagnosis).Contains("scope");
        await Assert.That(parsed.Diagnosis).Contains("context");
    }

    // ---- hazard two: a duplicate key ----

    [Test]
    public async Task A_duplicate_key_is_refused_rather_than_last_wins()
    {
        // Most parsers take last-wins silently, which is a way to smuggle a
        // change past a reviewer: the diff shows an added line and the
        // behaviour comes from it rather than from the line above.
        var parsed = EnvelopeYaml.Parse(
            Valid.Replace("    check: machine\n", "    check: machine\n    check: human\n", StringComparison.Ordinal));

        await Assert.That(parsed.Envelope).IsNull();
        await Assert.That(parsed.Diagnosis).Contains("check");
        await Assert.That(parsed.Diagnosis).Contains("twice");
    }

    [Test]
    public async Task A_duplicate_key_at_the_top_level_is_refused()
    {
        var parsed = EnvelopeYaml.Parse(Valid + "\ncontext:\n  scope: \"**\"\n  constitution: \"1.0.0\"\n");

        await Assert.That(parsed.Diagnosis).Contains("context");
        await Assert.That(parsed.Diagnosis).Contains("twice");
    }

    // ---- hazard three: anchors and aliases ----

    [Test]
    public async Task An_anchor_is_refused()
    {
        var parsed = EnvelopeYaml.Parse(
            Valid.Replace("  scope: \"src/**\"", "  scope: &s \"src/**\"", StringComparison.Ordinal));

        await Assert.That(parsed.Envelope).IsNull();
        await Assert.That(parsed.Diagnosis).Contains("anchor");
    }

    [Test]
    public async Task An_alias_is_refused()
    {
        var parsed = EnvelopeYaml.Parse("""
            context: &c
              scope: "src/**"
              constitution: "1.0.0"
            obligations: *c
            """);

        await Assert.That(parsed.Envelope).IsNull();
        await Assert.That(parsed.Diagnosis).Contains("alias").Or.Contains("anchor");
    }

    // ---- hazard four: implicit typing ----

    [Test]
    public async Task An_unquoted_version_keeps_its_trailing_zero()
    {
        // THE ONE THAT SILENTLY CHANGES WHAT GOVERNS WORK. Read through an
        // object mapper, 1.10 is a float and comes back 1.1. This parser works
        // at the event layer, where a scalar is a string and a style, and
        // nothing coerces because nothing asks for a type.
        var parsed = EnvelopeYaml.Parse(Valid.Replace("\"1.0.0\"", "1.10", StringComparison.Ordinal));

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.Envelope!.Context.Constitution).IsEqualTo("1.10");
    }

    [Test]
    public async Task A_quoted_version_survives_the_round_trip_byte_for_byte()
    {
        var parsed = EnvelopeYaml.Parse(Valid.Replace("\"1.0.0\"", "\"1.10\"", StringComparison.Ordinal));

        await Assert.That(EnvelopeText.Render(parsed.Envelope!)).Contains("constitution: \"1.10\"");
    }

    // ---- what a person typed, rather than what we emit ----

    [Test]
    public async Task Flow_style_is_accepted_even_though_it_is_never_emitted()
    {
        // A superset on purpose. Somebody hand-writing an envelope should not
        // have to guess our canonical form, and `show` normalises it anyway.
        var parsed = EnvelopeYaml.Parse(
            Valid.Replace("    discharges:\n      - in-scope\n",
                          "    discharges: [in-scope]\n", StringComparison.Ordinal));

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.Envelope!.Loops.Single().Discharges).IsEquivalentTo((string[])["in-scope"]);
    }

    [Test]
    public async Task A_comment_is_dropped_and_said_out_loud()
    {
        // Comments are not preserved: the stored thing is the model and `show`
        // renders it. Somebody has to be told the first time, or they lose one
        // silently and find out weeks later.
        var parsed = EnvelopeYaml.Parse("# why this envelope exists\n" + Valid);

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.Notes).IsNotEmpty();
        await Assert.That(string.Join(" ", parsed.Notes).ToLowerInvariant()).Contains("comment");
    }

    [Test]
    public async Task An_envelope_with_no_comments_says_nothing_about_them()
    {
        // The other half. A note that always appeared would be a note nobody
        // reads, which is the same as not having one.
        await Assert.That(EnvelopeYaml.Parse(Valid).Notes).IsEmpty();
    }

    // ---- malformed rather than invalid ----

    [Test]
    public async Task A_syntax_error_names_the_line()
    {
        var parsed = EnvelopeYaml.Parse("context:\n  scope: [unclosed\n");

        await Assert.That(parsed.Envelope).IsNull();
        await Assert.That(parsed.Diagnosis).IsNotNull();
        await Assert.That(parsed.Diagnosis).Contains("line");
    }

    [Test]
    public async Task An_empty_document_is_refused_rather_than_becoming_an_empty_envelope()
    {
        // Article XI. An empty envelope governs nothing, and a flight that ran
        // under one would report success having enforced nothing at all.
        foreach (var nothing in (string[])["", "   ", "\n\n", "# just a comment\n"])
        {
            var parsed = EnvelopeYaml.Parse(nothing);

            await Assert.That(parsed.Envelope).IsNull();
            await Assert.That(parsed.Diagnosis).IsNotNull();
        }
    }

    [Test]
    public async Task Two_documents_are_refused()
    {
        var parsed = EnvelopeYaml.Parse(Valid + "\n---\n" + Valid);

        await Assert.That(parsed.Envelope).IsNull();
        await Assert.That(parsed.Diagnosis).Contains("one");
    }

    // ---- the model's own rule, applied after parsing ----

    [Test]
    public async Task A_document_that_parses_but_breaks_the_schema_is_refused_by_the_schemas_rule()
    {
        // Parsing and validating are different jobs and this is where they
        // meet: the text was readable, and the envelope it describes is not
        // one this slice allows. The diagnosis comes from Envelope.Validate,
        // so gg and the control plane cannot disagree about it.
        var parsed = EnvelopeYaml.Parse(Valid.Replace(
            "    executor: frontier", "    executor: cheap", StringComparison.Ordinal));

        await Assert.That(parsed.Envelope).IsNull();
        await Assert.That(parsed.Diagnosis).Contains("cheap");
    }

    // ---- two obligations, which is where a parser can silently lose one ----

    private static Envelope AtTwo() => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations =
        [
            new Obligation
            {
                Id = "in-scope",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            },
            new Obligation
            {
                Id = "not-exhausted",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.LoopNotExhausted,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["in-scope", "not-exhausted"],
                Moves = [LoopMoves.Read, LoopMoves.Edit],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "pull-request",
                Kind = DestinationKinds.PullRequest,
                Requires = ["in-scope", "not-exhausted"],
            },
        ],
    };

    [Test]
    public async Task Two_obligations_round_trip_without_loss()
    {
        // Emitted, parsed, emitted. The second emit has to be the first, or
        // something the parser dropped is something a tenant wrote - and nobody
        // notices until a rule stops being enforced.
        var text = EnvelopeText.Render(AtTwo());
        var parsed = EnvelopeYaml.Parse(text);

        await Assert.That(parsed.Envelope).IsNotNull().Because(parsed.Diagnosis ?? "");
        await Assert.That(EnvelopeText.Render(parsed.Envelope!)).IsEqualTo(text);
    }

    [Test]
    public async Task Both_obligations_survive_as_distinct_obligations()
    {
        // A parser that read the second over the first would produce ONE
        // obligation and a round trip that looks fine until you count them.
        var parsed = EnvelopeYaml.Parse(EnvelopeText.Render(AtTwo())).Envelope!;

        await Assert.That(parsed.Obligations.Count).IsEqualTo(2);
        await Assert.That(parsed.Obligations.Select(o => o.Id).Order().ToList())
            .IsEquivalentTo((string[])["in-scope", "not-exhausted"]);
        await Assert.That(parsed.Obligations.Select(o => o.Rule).Distinct().Count()).IsEqualTo(2)
            .Because("they read different facts, which is the whole reason there are two.");
    }

    [Test]
    public async Task A_duplicate_obligation_id_is_still_refused_at_two()
    {
        // The duplicate-key hazard, at the cardinality where it stops being
        // theoretical: two obligations named the same thing is a rule silently
        // replacing another rule.
        var text = EnvelopeText.Render(AtTwo()).Replace("not-exhausted:", "in-scope:");

        var parsed = EnvelopeYaml.Parse(text);

        await Assert.That(parsed.Envelope).IsNull();
        await Assert.That(parsed.Diagnosis!).Contains("in-scope");
    }
}
