namespace Gg.Contracts.Tests;

/// <summary>
/// The narrowing: a role-shaped type that cannot say what its role may not
/// move.
/// </summary>
/// <remarks>
/// <para>
/// <b>The strongest form of the operator table is one a document cannot
/// express</b>, rather than one it gets told off for expressing. A narrowing
/// that syntactically cannot carry a loop set, a destination set, or a
/// context selection needs no <c>Validate</c> refusal for them at all - there
/// is nothing to refuse, and nothing for a refusal to miss.
/// </para>
/// <para>
/// <b>Nothing constructs it in production yet, deliberately.</b> Narrowings
/// by name are the slice's pre-committed cut; until they land, this type is
/// kept live by the vocabulary tests, the surface fingerprint, and the
/// round-trip suites constructing and parsing it every build - and the
/// contract ledger's 0.44.0 note says so out loud.
/// </para>
/// </remarks>
public class EnvelopeNarrowingTests
{
    private static EnvelopeNarrowing ANarrowing() => new()
    {
        Obligations =
        [
            new Obligation
            {
                Id = "human-look",
                Check = ObligationChecks.Human,
                Approver = "lead",
                Evidence = [EvidenceItems.AgentAccount, EvidenceItems.ChangeManifest],
            },
            new Obligation
            {
                Id = "in-scope",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            },
        ],
    };

    [Test]
    public async Task A_narrowing_carries_obligations_and_nothing_else()
    {
        // AT THE SCHEMA LEVEL, not in Validate. A property that does not exist
        // cannot be authored, cannot be stored, and cannot need a refusal that
        // somebody forgot to write.
        var members = typeof(EnvelopeNarrowing).GetProperties().Select(p => p.Name).ToList();

        await Assert.That(members).IsEquivalentTo((string[])[nameof(EnvelopeNarrowing.Obligations)])
            .Because("a narrowing that grew a member grew a thing lower layers can move, and "
                   + "that decision belongs to the operator table, not to a refactor.");
    }

    [Test]
    public async Task A_narrowing_with_no_obligations_is_refused()
    {
        var refused = EnvelopeNarrowing.Validate(new EnvelopeNarrowing { Obligations = [] });

        await Assert.That(refused).IsNotNull()
            .Because("a narrowing that narrows nothing is a document with no reason to exist, "
                   + "and storing it would mint versions that govern nothing.");
    }

    [Test]
    public async Task A_narrowing_obligation_is_held_to_the_same_rules_as_an_envelopes()
    {
        // SHARED RULES, not parallel ones. The per-obligation validation is the
        // envelope's own, extracted rather than copied, so the two documents
        // cannot drift about what a well-formed obligation is.
        var unapproved = EnvelopeNarrowing.Validate(new EnvelopeNarrowing
        {
            Obligations = [new Obligation { Id = "human-look", Check = ObligationChecks.Human }],
        });

        await Assert.That(unapproved).IsNotNull();
        await Assert.That(unapproved!).Contains("approver");

        var overruled = EnvelopeNarrowing.Validate(new EnvelopeNarrowing
        {
            Obligations =
            [
                new Obligation
                {
                    Id = "in-scope",
                    Check = ObligationChecks.Machine,
                    Rule = ObligationPredicates.NoFileOutsideScope,
                    Approver = "lead",
                },
            ],
        });

        await Assert.That(overruled).IsNotNull();
        await Assert.That(overruled!).Contains("machine");
    }

    [Test]
    public async Task A_narrowings_canonical_form_is_the_shape_declared()
    {
        // The whole document, byte for byte, so any drift in the second
        // emitter's output is a diff here rather than a surprise in a
        // customer's git history.
        await Assert.That(EnvelopeText.Render(ANarrowing())).IsEqualTo(
            "obligations:\n"
          + "  human-look:\n"
          + "    check: human\n"
          + "    approver: lead\n"
          + "    evidence:\n"
          + "      - agent-account\n"
          + "      - change-manifest\n"
          + "  in-scope:\n"
          + "    check: machine\n"
          + "    rule: no-file-outside-scope\n");
    }

    [Test]
    public async Task Two_narrowings_differing_only_in_authored_order_emit_identical_bytes()
    {
        var reordered = new EnvelopeNarrowing
        {
            Obligations = [.. ANarrowing().Obligations.Reverse()],
        };

        await Assert.That(EnvelopeText.Render(reordered)).IsEqualTo(EnvelopeText.Render(ANarrowing()))
            .Because("a canonical form is a function of what a document says, not of the order "
                   + "somebody typed it in.");
    }
}
