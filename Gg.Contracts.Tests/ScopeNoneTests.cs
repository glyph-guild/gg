using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// <c>scope</c> stays required, gains <c>none</c> as a value, and absent never
/// starts meaning it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The move that looks like pedantry and is not.</b> Making the field
/// optional would fail the way <c>evidence:</c> did: a line that can be dropped
/// is a constraint that can be dropped silently, and <i>nothing was written</i>
/// would render identically to <i>nothing is bounded</i>. So <c>none</c> is a
/// VALUE, and these tests are shaped against omission rather than against
/// <c>none</c>.
/// </para>
/// <para>
/// <b>And it is a tightening.</b> Before it, <c>"**"</c> was the only way to
/// write unbounded, and it was indistinguishable from a bound somebody meant.
/// After it, subjectless work says <c>none</c> and <c>"**"</c> goes back to
/// meaning every path — so the two are asserted to be different values that
/// never render as each other.
/// </para>
/// </remarks>
public class ScopeNoneTests
{
    private static Envelope Document(string scope, IReadOnlyList<string>? accepts) => new()
    {
        Context = new ContextBinding { Scope = scope, Constitution = "1.0.0" },
        Accepts = accepts,
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
                Id = "work",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["in-scope"],
                Moves = [LoopMoves.Read],
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
                Requires = ["in-scope"],
            },
        ],
    };

    private static Envelope Subjectless() => Document(EnvelopeScopes.None, accepts: []);

    [Test]
    public async Task A_document_that_omits_scope_is_refused_and_says_which_key()
    {
        // ALREADY TRUE, AND ASSERTED HERE BECAUSE IT IS NOW LOad-BEARING. The
        // parser has always refused a missing key rather than defaulting it,
        // which is why `none` could be added as a value without the field
        // becoming optional underneath. What changes with `none` in the
        // vocabulary is the COST of that rule lapsing: before, a missing
        // scope had no value it could be mistaken for.
        var written = EnvelopeText.Render(Subjectless());
        var without = string.Join(
            '\n', written.Split('\n').Where(l => !l.TrimStart().StartsWith("scope:", StringComparison.Ordinal)));

        var parsed = EnvelopeYaml.Parse(without);

        await Assert.That(parsed.Envelope).IsNull();
        await Assert.That(parsed.Diagnosis!).Contains("scope");
    }

    [Test]
    public async Task Absent_is_refused_rather_than_read_as_none()
    {
        // THE POISON TWIN OF THE ONE ABOVE, and the whole point of the value.
        // A parser that read a missing scope as `none` would turn every
        // envelope that lost the line - to an edit, a bad round trip, a merge -
        // into an envelope that bounds nothing, silently, and the flight would
        // still fly.
        var written = EnvelopeText.Render(Subjectless());
        var without = string.Join(
            '\n', written.Split('\n').Where(l => !l.TrimStart().StartsWith("scope:", StringComparison.Ordinal)));

        // THE OBVIOUS ASSERTION HERE IS WRONG AND I WROTE IT FIRST.
        // `DoesNotContain("scope:")` matches `in-scope:` - obligations render
        // as a map keyed by id - so it failed while the filter above was
        // working perfectly. The check has to be about the LINE, which is what
        // the removal was about.
        await Assert.That(without.Split('\n').Any(
                l => l.TrimStart().StartsWith("scope:", StringComparison.Ordinal))).IsFalse()
            .Because("the twin is only meaningful if the line really went.");
        await Assert.That(EnvelopeYaml.Parse(without).Diagnosis)
            .IsNotEqualTo(EnvelopeYaml.Parse(written).Diagnosis)
            .Because("one of these documents is valid and the other is missing a required "
                   + "line; a parser answering the same thing to both has stopped reading it.");
    }

    [Test]
    public async Task None_survives_both_render_paths()
    {
        foreach (var written in (string[])
                 [EnvelopeText.Render(Subjectless()), EnvelopeText.RenderComposed(Subjectless())])
        {
            var read = EnvelopeYaml.Parse(written);

            await Assert.That(read.Diagnosis).IsNull()
                .Because($"the emitter's own output must parse. Wrote:\n{written}");
            await Assert.That(read.Envelope!.Context.Scope).IsEqualTo(EnvelopeScopes.None);
        }
    }

    [Test]
    public async Task None_and_the_universal_glob_are_different_values()
    {
        // THE TIGHTENING, ASSERTED. `**` used to be the only way to write
        // unbounded and was indistinguishable from a bound somebody meant.
        var everything = EnvelopeText.Render(Document("**", accepts: [SubjectKinds.Repository]));
        var nothing = EnvelopeText.Render(Subjectless());

        await Assert.That(everything).IsNotEqualTo(nothing);
        await Assert.That(EnvelopeYaml.Parse(everything).Envelope!.Context.Scope).IsEqualTo("**");
        await Assert.That(EnvelopeYaml.Parse(nothing).Envelope!.Context.Scope)
            .IsEqualTo(EnvelopeScopes.None)
            .Because("every path and no tree are opposite claims, and before this slice one "
                   + "string had to carry both.");
    }

    [Test]
    public async Task A_narrowing_cannot_carry_a_scope_at_all_which_is_stronger_than_refusing_one()
    {
        // A CORRECTION TO THIS SLICE'S OWN BRIEF, recorded where it was found.
        // The brief says `none` must survive "the fragment path" too. It
        // cannot: EnvelopeNarrowing has no context and therefore no scope, so
        // there is no fragment round trip for the value to survive.
        //
        // That is not a gap - it is ADR-0018 section 1 working. A narrowing
        // living in a service repository must not be able to say `scope:`, and
        // the TYPE is the primary lock rather than a check, because no gate
        // stands in front of those files. A refusal can be forgotten; an
        // absent member cannot be written.
        await Assert.That(typeof(EnvelopeNarrowing).GetProperty("Context")).IsNull();
        await Assert.That(typeof(EnvelopeNarrowing).GetProperty("Scope")).IsNull();

        var refused = EnvelopeYaml.ParseNarrowing(
            "context:\n  scope: src/**\nobligations:\n  human-look:\n    check: human\n"
          + "    approver: lead\n");

        await Assert.That(refused.Narrowing).IsNull();
        await Assert.That(refused.Diagnosis!).Contains("context")
            .Because("the parser closes the fragment's root to what a narrowing may say, so a "
                   + "copied context block is named rather than ignored.");
    }
}
