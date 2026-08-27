using System.Reflection;

namespace Gg.Contracts.Tests;

/// <summary>
/// <c>SubjectKinds</c> — what a piece of work can be ABOUT, as a closed
/// vocabulary rather than as three unrelated optional strings.
/// </summary>
/// <remarks>
/// <para>
/// <b>It did not exist, and its absence is why ADR-0020's open question could
/// not be answered.</b> What this codebase called a <i>subject</i> was a
/// repository registry name; a launch request carries a work kind, an
/// environment and a repository as three separate members with no kind between
/// them. <c>accepts:</c> has to range over something, and there was nothing.
/// </para>
/// <para>
/// <b>Closed for the reason every vocabulary here is closed.</b> An unknown
/// subject kind must halt. The alternative reading - treat what we do not
/// recognise as no constraint - makes a typo in <c>accepts:</c> into a kind
/// that accepts everything, which is the permissive direction and the one this
/// product exists to refuse.
/// </para>
/// </remarks>
public class SubjectKindVocabularyTests
{
    [Test]
    public async Task Every_member_is_in_All()
    {
        // 0.65.0's lesson for the third slice running: `stale-working-copy`
        // shipped with a sentence and no `All` entry, was invisible to the
        // closed-vocabulary fingerprint BECAUSE the fingerprint reads `All`,
        // and surfaced control-plane-side as a 500. The guard belongs where
        // the omission happens.
        var declared = typeof(SubjectKinds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        await Assert.That(declared).IsNotEmpty()
            .Because("an empty scan satisfies the assertion below without reading anything, "
                   + "which is the shape of a guard that stopped guarding.");

        var missing = declared.Except(SubjectKinds.All, StringComparer.Ordinal).ToList();

        await Assert.That(missing).IsEmpty()
            .Because("a value with a sentence and no All entry is invisible to the fingerprint "
                   + "that exists to force a version conversation. Found: "
                   + string.Join(", ", missing));
    }

    [Test]
    public async Task An_unknown_subject_kind_is_refused_rather_than_read_as_no_constraint()
    {
        await Assert.That(SubjectKinds.IsKnown(SubjectKinds.Repository)).IsTrue();
        await Assert.That(SubjectKinds.IsKnown("repositoy")).IsFalse()
            .Because("a typo that reads as 'accepts anything' is a kind that accepts anything, "
                   + "and nothing on the page would say so.");
        await Assert.That(SubjectKinds.IsKnown("")).IsFalse();
    }

    [Test]
    public async Task The_vocabulary_declares_which_fingerprint_it_belongs_to()
    {
        // Discovered by SHAPE - any public static IReadOnlyList<string> on a
        // static class - so this type joins the closed-vocabulary sweep the day
        // it is written. What shape cannot answer is which fingerprint it
        // belongs to, so that is declared and this is what verifies somebody
        // declared it.
        await Assert.That(typeof(SubjectKinds).GetCustomAttribute<VocabularyOfAttribute>())
            .IsNotNull()
            .Because("a closed vocabulary with no declared fingerprint moves neither of them, "
                   + "so adding a value to it would break every prior reader silently.");
    }

    [Test]
    public async Task An_accepts_list_naming_an_unknown_kind_is_refused_where_an_author_can_act()
    {
        var envelope = new Envelope
        {
            Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
            Accepts = ["repositoy"],
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

        var refusal = Envelope.Validate(envelope);

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("repositoy")
            .Because("the refusal names the value somebody typed, which is the difference "
                   + "between a diagnosis and a complaint.");
        await Assert.That(refusal!).Contains(SubjectKinds.Repository)
            .Because("and it lists what was expected, the way the unknown-destination-kind "
                   + "refusal already does.");
    }
}
