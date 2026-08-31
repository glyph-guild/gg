namespace Gg.Contracts.Tests;

/// <summary>
/// An obligation that would decide on a work item's text is refused when it is
/// written.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the containment, not a nicety at the end of a list.</b> A ticket
/// is a person's typed words, carried <c>stated</c> and never <c>measured</c> —
/// <see cref="EvidenceVoices"/> exists because <i>the lie hazard was never the
/// claim, it is a claim wearing measurement's clothes</i>. A gate whose verdict
/// rests on that text is a gate that anyone who can comment on the ticket can
/// answer, and the people who can comment on a ticket are not the people the
/// envelope named.
/// </para>
/// <para>
/// <b>Named specifically, on the <c>obligations.</c> precedent.</b> Both the
/// generic arms would already refuse this — as <i>unknown rule</i> and as
/// <i>not a condition this version understands</i> — and both would read as a
/// version that has not got round to it yet, which is how an escape hatch ships
/// as unsupported-but-authorable. Somebody will type it, because reading the
/// ticket is the obvious thing to want.
/// </para>
/// <para>
/// <b>Both routes and both documents.</b> An obligation decides through its
/// <c>rule</c> and attaches through its <c>when</c>, and
/// <c>EnvelopeNarrowing.Validate</c> shares this validator — so a refusal that
/// held only the full envelope would leave the narrowing door open.
/// </para>
/// </remarks>
public class IntentTextRefusalTests
{
    private static Envelope With(Obligation obligation) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations = [obligation],
        Loops =
        [
            new Loop
            {
                Id = "work",
                Executor = ExecutorRungs.Frontier,

                // A LOOP MAY NOT DISCHARGE A HUMAN CHECK - "a runner answering
                // for a person" - so the fixture cannot hand every obligation
                // to the loop. Written after the first version did, and made a
                // test red for a reason that had nothing to do with what it was
                // asserting: a false red is a passing test in waiting.
                Discharges = string.Equals(
                    obligation.Check, ObligationChecks.Machine, StringComparison.Ordinal)
                    ? [obligation.Id]
                    : [],
                Moves = [LoopMoves.Read, LoopMoves.Edit],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "forge",
                Kind = DestinationKinds.PullRequest,
                Requires = [obligation.Id],
            },
        ],
    };

    private static string? Refusal(Obligation obligation) =>
        Envelope.Validate(With(obligation), Roles.Root);

    // ---- the rule route: what the obligation DECIDES on ----

    [Test]
    public async Task An_obligation_deciding_on_ticket_text_is_refused()
    {
        var diagnosis = Refusal(new Obligation
        {
            Id = "ticket-says-so",
            Check = ObligationChecks.Machine,
            Rule = "intent.description mentions rollback",
        });

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("ticket-says-so")
            .Because("naming the obligation is how somebody finds the line.");
        await Assert.That(diagnosis!).Contains("stated")
            .Because("the voice is the reason. A ticket is a person's typed words, and a "
                   + "diagnosis that refuses without saying so reads as a missing feature.");
    }

    [Test]
    public async Task The_refusal_is_its_own_and_not_the_generic_unknown_rule()
    {
        // THE WHOLE POINT. The generic arm already refuses this - and reads as
        // a version that has not got round to it yet, which is how an escape
        // hatch ships as unsupported-but-authorable.
        var named = Refusal(new Obligation
        {
            Id = "ticket-says-so",
            Check = ObligationChecks.Machine,
            Rule = "intent.title contains hotfix",
        })!;

        var generic = Refusal(new Obligation
        {
            Id = "typo",
            Check = ObligationChecks.Machine,
            Rule = "no-file-outside-scpoe",
        })!;

        await Assert.That(generic).Contains("Expected one of");
        await Assert.That(named).DoesNotContain("Expected one of")
            .Because("offering the list of known rules invites somebody to look for the one "
                   + "that reads the ticket, and the answer is that there is not going to be "
                   + "one.");
    }

    // ---- the attachment route: WHEN the obligation appears ----

    [Test]
    public async Task An_obligation_attaching_on_ticket_text_is_refused_too()
    {
        var diagnosis = Refusal(new Obligation
        {
            Id = "in-scope",
            Check = ObligationChecks.Machine,
            Rule = ObligationPredicates.NoFileOutsideScope,
            When = "intent.labels include security",
        });

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("stated")
            .Because("attaching on the text is deciding on the text one step earlier - whether "
                   + "the gate EXISTS is as much a verdict as what it says.");
    }

    [Test]
    public async Task A_human_checked_obligation_cannot_attach_on_it_either()
    {
        // The route that looks safe: a person answers the gate, so what harm is
        // there in the ticket deciding whether to ask them? The harm is that
        // whoever can comment on the ticket decides whether anybody is asked.
        var diagnosis = Refusal(new Obligation
        {
            Id = "human-look",
            Check = ObligationChecks.Human,
            Approver = "lead",
            When = "intent.state is closed",
        });

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("stated");
    }

    // ---- the narrowing door, which shares the validator ----

    [Test]
    public async Task A_narrowing_cannot_author_one_either()
    {
        var narrowing = new EnvelopeNarrowing
        {
            Obligations =
            [
                new Obligation
                {
                    Id = "ticket-says-so",
                    Check = ObligationChecks.Machine,
                    Rule = "intent.body mentions migration",
                },
            ],
        };

        await Assert.That(EnvelopeNarrowing.Validate(narrowing)).IsNotNull()
            .Because("the narrowing shares ValidateObligation precisely so the two documents "
                   + "cannot drift, and a refusal that held only the full envelope would leave "
                   + "the smaller door open.");
    }

    // ---- and what must keep working ----

    [Test]
    public async Task The_rules_and_conditions_that_already_worked_still_do()
    {
        // The over-refusal half. A prefix rule tight enough to catch
        // `intent.title` and loose enough to catch a legitimate condition would
        // break every envelope anybody has applied.
        await Assert.That(Refusal(new Obligation
        {
            Id = "in-scope",
            Check = ObligationChecks.Machine,
            Rule = ObligationPredicates.NoFileOutsideScope,
        })).IsNull();

        foreach (var condition in AttachmentConditions.Forms)
        {
            // Only the forms that are whole conditions on their own; the
            // prefixed ones are rendered with a <placeholder> and are
            // exercised where their arguments are.
            if (condition.Contains('<', StringComparison.Ordinal))
            {
                continue;
            }

            var diagnosis = Refusal(new Obligation
            {
                Id = "human-look",
                Check = ObligationChecks.Human,
                Approver = "lead",
                When = condition,
            });

            await Assert.That(diagnosis).IsNull()
                .Because($"'{condition}' is an advertised attachment condition.");
        }
    }

    [Test]
    public async Task A_word_that_merely_starts_with_intent_is_not_caught()
    {
        // The poison twin for the prefix. `intentional` is not `intent.`, and a
        // guard that could not tell them apart would be refusing on a substring
        // rather than on a citation.
        var diagnosis = Refusal(new Obligation
        {
            Id = "typo",
            Check = ObligationChecks.Machine,
            Rule = "intentional-drift",
        })!;

        await Assert.That(diagnosis).Contains("Expected one of")
            .Because("this is an unknown rule and gets the ordinary unknown-rule sentence. If "
                   + "it got the ticket-text one, the refusal is matching a substring rather "
                   + "than a citation, and it would eventually refuse something legitimate.");
    }
}
