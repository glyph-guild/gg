namespace Gg.Contracts.Tests;

/// <summary>
/// The canonical text form: stable bytes, and the one coercion that would
/// change what governs work.
/// </summary>
/// <remarks>
/// <para>
/// <b>`show` twice must give identical bytes</b>, because the text form exists
/// to be diffed. A customer who keeps envelopes in git and syncs them has
/// their review process and our authority; a renderer that reordered a map or
/// varied its quoting would make every sync a spurious diff and the practice
/// would be abandoned within a week.
/// </para>
/// <para>
/// <b>`1.10` must not become `1.1`.</b> The Norway problem's real form here:
/// YAML's implicit typing reads an unquoted <c>1.10</c> as a float, and a
/// float drops the trailing zero. After a slice spent learning that versions
/// matter, this is the coercion to guard - it silently changes which
/// constitution a flight ran under.
/// </para>
/// <para>
/// The emitter's answer is to quote anything a reader could coerce, so the
/// canonical form is safe in a parser that is not ours. That is worth more
/// than it costs: the file is going to be opened by editors, web forms and
/// whatever a customer's CI uses.
/// </para>
/// </remarks>
public class EnvelopeRoundTripTests
{
    private static Envelope AnEnvelope(string constitution = "1.0.0") => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = constitution },
        Obligations =
        [
            new Obligation
            {
                Id = "in-scope",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["in-scope"],
                Moves = [LoopMoves.Read, LoopMoves.Edit, LoopMoves.RunTests, LoopMoves.Search],
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
                Requires = ["in-scope"],
            },
        ],
    };

    [Test]
    public async Task Rendering_twice_gives_identical_bytes()
    {
        var envelope = AnEnvelope();

        await Assert.That(EnvelopeText.Render(envelope)).IsEqualTo(EnvelopeText.Render(envelope));
    }

    [Test]
    public async Task Rendering_two_equal_models_gives_identical_bytes()
    {
        // Stronger than rendering one model twice, and the one that actually
        // matters: two hosts holding the same envelope must produce the same
        // file, or a diff is noise.
        await Assert.That(EnvelopeText.Render(AnEnvelope())).IsEqualTo(EnvelopeText.Render(AnEnvelope()));
    }

    [Test]
    public async Task A_version_string_keeps_its_trailing_zero()
    {
        var text = EnvelopeText.Render(AnEnvelope(constitution: "1.10"));

        await Assert.That(text).Contains("\"1.10\"")
            .Because("unquoted, 1.10 is a float to every YAML reader in the world and comes back 1.1 - "
                   + "which silently changes which constitution a flight ran under.");
        await Assert.That(text).DoesNotContain("constitution: 1.10");
    }

    [Test]
    public async Task Anything_a_reader_could_coerce_is_quoted()
    {
        // The Norway problem by its own name, plus its relatives. None of these
        // is a value we expect; all of them are values a reader would silently
        // retype.
        //
        // The emitter's rule is an ALLOW-LIST rather than this list: an
        // identifier survives unquoted and everything else is quoted. These are
        // the cases that would hurt, asserted directly, but the rule catches a
        // larger set than anybody enumerated - which is the point of writing it
        // that way round.
        foreach (var hazard in (string[])["no", "yes", "true", "false", "null", "~", "on", "off", "1.10", "010"])
        {
            var text = EnvelopeText.Render(AnEnvelope(constitution: hazard));

            await Assert.That(text).Contains($"constitution: \"{hazard}\"")
                .Because($"'{hazard}' unquoted is not a string to a YAML reader.");
        }
    }

    [Test]
    public async Task The_canonical_form_uses_block_sequences_and_no_flow_style()
    {
        var text = EnvelopeText.Render(AnEnvelope());

        await Assert.That(text).DoesNotContain("[")
            .Because("flow style is a second way to write the same list, and a canonical form has one.");
        await Assert.That(text).DoesNotContain("&").And.DoesNotContain("*x")
            .Because("no anchors and no aliases: a policy document that can reference itself is one "
                   + "nobody can read linearly.");
        await Assert.That(text).DoesNotContain("---")
            .Because("one document, so there is never a question of which one is in force.");
        await Assert.That(text).Contains("      - in-scope");
    }

    [Test]
    public async Task The_canonical_form_is_the_shape_the_slice_declared()
    {
        // Asserted whole rather than key by key. The point of a canonical form
        // is the exact bytes, and a test that checked "contains scope" would
        // pass on a renderer that had quietly started emitting something else.
        var text = EnvelopeText.Render(AnEnvelope());

        await Assert.That(text).IsEqualTo(
            """
            context:
              scope: "src/**"
              constitution: "1.0.0"
            obligations:
              in-scope:
                check: machine
                rule: no-file-outside-scope
                provenance: org
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

            """);
    }

    [Test]
    public async Task The_rendered_form_ends_with_exactly_one_newline()
    {
        // A file, not a fragment. Text that does not end in a newline makes
        // every diff tool say so, forever.
        var text = EnvelopeText.Render(AnEnvelope());

        await Assert.That(text.EndsWith('\n')).IsTrue();
        await Assert.That(text.EndsWith("\n\n", StringComparison.Ordinal)).IsFalse();
        await Assert.That(text).DoesNotContain("\r")
            .Because("the canonical form is bytes, so the line ending cannot depend on the machine.");
    }
}
