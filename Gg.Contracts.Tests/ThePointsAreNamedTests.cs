using System.Reflection;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// An environment comes up through five named points, and the names are closed.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0033's second amendment, which superseded Decision 9.</b> That decision
/// collapsed five hooks into an AppHost because <i>"Aspire IS the warm-up, the
/// reconcile, the health model and the teardown"</i> — true of Aspire, and the
/// reason the collapse was wrong: it made Aspire the contract, excluding every
/// compose stack and every project that is not .NET. The owner retook it: the
/// five points are the abstraction, and Aspire is one implementation behind them.
/// </para>
/// <para>
/// <b>Two of the five are renames, and both had to be.</b> <c>warm</c> already
/// meant a COUNT (<see cref="StrategyInventory.Warm"/>) and a VERB (the control
/// plane's <c>WarmedAsync</c>), so a hook named <c>warm</c> would have been a
/// third sense of a word this system already uses twice — it is
/// <see cref="EnvironmentPoints.Prepare"/>. And <c>verify</c> is already a pool
/// action, <i>"inspect a pool member and attest what was found. Changes
/// nothing"</i>, about the member rather than the customer's app — it is
/// <see cref="EnvironmentPoints.Ready"/>.
/// </para>
/// <para>
/// <b>Closed, and refused by name rather than by count.</b> A point nobody
/// declared cannot be invoked, and the refusal says which word was not
/// understood — the same disposition every other closed vocabulary here uses,
/// because a caller told only that something was wrong has to guess.
/// </para>
/// </remarks>
public class ThePointsAreNamedTests
{
    [Test]
    public async Task The_five_points_are_declared_and_in_All()
    {
        // IN `All`, which is slice twelve's lesson: `airspace-registration` was
        // declared in 0.53.0 and left out of its own list, so the vocabulary
        // refused a word it had itself declared and it took a slice to notice.
        await Assert.That(EnvironmentPoints.All).IsEquivalentTo(new[]
        {
            EnvironmentPoints.Prepare,
            EnvironmentPoints.Attach,
            EnvironmentPoints.Sync,
            EnvironmentPoints.Ready,
            EnvironmentPoints.Detach,
        });

        await Assert.That(EnvironmentPoints.All.Count).IsEqualTo(5)
            .Because("five points is the abstraction ADR-0033's second amendment restored, "
                   + "and a sixth is a decision rather than an addition.");
    }

    [Test]
    public async Task Neither_rename_leaves_its_old_name_behind()
    {
        // THE WHOLE REASON FOR THE RENAMES. `warm` is a count and a verb
        // already; `verify` is a pool action already. Either name here would be
        // a second meaning for a word in use, which is the collision the owner
        // renamed to avoid - so the test is that the old spellings are absent,
        // not merely that the new ones are present.
        await Assert.That(EnvironmentPoints.All).DoesNotContain("warm")
            .Because("`warm` is a count on StrategyInventory and a verb on the control "
                   + "plane's grants; a third sense would make the word useless.");

        await Assert.That(EnvironmentPoints.All).DoesNotContain("verify")
            .Because("`verify` is a pool action about the MEMBER - inspect it and attest "
                   + "what was found - and this is about the customer's app.");
    }

    [Test]
    public async Task A_point_nobody_declared_is_refused_naming_itself()
    {
        foreach (var written in (string[]) ["warm", "verify", "start", "up", "Prepare", ""])
        {
            var refusal = EnvironmentPoints.Validate(written);

            await Assert.That(refusal).IsNotNull()
                .Because($"'{written}' is not a point this version knows, and a point nobody "
                       + "declared cannot be invoked.");

            await Assert.That(refusal!).Contains(EnvironmentPoints.Prepare)
                .Because("the refusal lists what was expected, so a reader who misspelled one "
                       + "can see the set rather than guess at it.");
        }
    }

    [Test]
    public async Task A_declared_point_is_taken()
    {
        foreach (var point in EnvironmentPoints.All)
        {
            await Assert.That(EnvironmentPoints.Validate(point)).IsNull()
                .Because($"'{point}' is in All, and a vocabulary that refused its own member "
                       + "would be the slice-twelve defect wearing the other face.");
        }
    }

    [Test]
    public async Task Both_new_vocabularies_declare_which_fingerprint_they_belong_to()
    {
        // BY SHAPE IS NOT ENOUGH. ClosedVocabularies finds a vocabulary by its
        // shape - a sealed static class with a public static list of strings -
        // but nothing about the shape says whether its values reach a FACT or
        // only the contract surface. That is declared, and a vocabulary that
        // declares nothing is invisible to the fingerprint it belongs in.
        foreach (var type in (Type[]) [typeof(EnvironmentPoints), typeof(FilesystemRelationships)])
        {
            await Assert.That(type.GetCustomAttribute<VocabularyOfAttribute>()).IsNotNull()
                .Because($"{type.Name} is a closed vocabulary, so a change to it has to move "
                       + "a fingerprint - and which one is not something its shape can say.");
        }
    }
}
