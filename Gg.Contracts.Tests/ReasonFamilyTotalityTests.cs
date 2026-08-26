using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// Every declared reason kind has a family and a sentence.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written because a kind shipped without either and nothing noticed.</b>
/// <c>stale-working-copy</c> was added in 0.65.0 with a sentence and no entry in
/// <c>All</c> and no entry in <c>FamilyOf</c>. The whole gg suite stayed green:
/// nothing there calls <c>FamilyOf</c> on a kind it did not name explicitly, and
/// the closed-vocabulary fingerprint reads <c>All</c>, so a kind missing from
/// <c>All</c> is invisible to the guard that exists to notice new values.
/// </para>
/// <para>
/// <b>It surfaced as a 500 in the other repository</b> — the control plane built
/// the reason, <c>FamilyOf</c> threw, and a governed refusal became an internal
/// error. That is precisely the shape <c>Reason</c>'s own doc comment says these
/// throws exist to prevent, one level up: the gap failed an audit rather than a
/// build.
/// </para>
/// <para>
/// <b>Discovered from the vocabulary rather than listed</b>, so the next kind is
/// covered the day it is written rather than the day somebody consumes it.
/// </para>
/// </remarks>
public class ReasonFamilyTotalityTests
{
    [Test]
    public async Task Every_kind_has_a_family()
    {
        foreach (var kind in ReasonKinds.All)
        {
            var family = ReasonKinds.FamilyOf(kind);

            await Assert.That(ReasonFamilies.All).Contains(family)
                .Because($"'{kind}' derives family '{family}', which is not a family - a "
                       + "refusal filed under a name nobody reads is a refusal nobody reads");
        }
    }

    [Test]
    public async Task Every_kind_has_a_sentence_that_is_not_the_kind_repeated()
    {
        foreach (var kind in ReasonKinds.All)
        {
            // The second parameter is a real clearing, because blocked-by-bound
            // THROWS on one it does not know - slice twelve's deliberate poison,
            // and the reason a placeholder here would fail for the right reason
            // at the wrong test.
            var sentence = Reason.Sentence(kind, ["a-field", BoundClearings.Capacity]);

            await Assert.That(sentence).IsNotEmpty()
                .Because($"'{kind}' renders nothing, so a person is told a machine word");
            await Assert.That(sentence).IsNotEqualTo(kind);
        }
    }

    [Test]
    public async Task A_kind_that_is_not_declared_still_throws()
    {
        // LIVENESS. Both assertions above walk a list, and a walk that found
        // nothing would pass. What must stay true is that an UNDECLARED kind
        // fails loudly rather than deriving a plausible family.
        await Assert.That(() => ReasonKinds.FamilyOf("invented-just-now"))
            .Throws<InvalidOperationException>();
    }
}
