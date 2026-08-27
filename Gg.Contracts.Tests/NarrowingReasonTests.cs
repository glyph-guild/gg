namespace Gg.Contracts.Tests;

/// <summary>
/// Absent and unreachable are two answers, and collapsing them makes a forge
/// incident look like somebody's mistake.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0018 § 6, and the distinction is the whole of it.</b> A declared
/// directory that is not there at the pinned commit is a
/// <b>misconfiguration</b> — somebody turned the layer on before the team
/// committed anything, and the fix is a merge. A forge that cannot be asked is
/// an <b>outage</b> — nothing is wrong with the configuration and there is
/// nothing for the tenant to fix. One sentence for both would send the second
/// person to look at the first person's problem.
/// </para>
/// <para>
/// <b>Neither is <i>constrains nothing</i>.</b> That is the third answer and it
/// is the one that must never be produced by a read that did not happen: a
/// fetch that 404s and composes as no narrowing is indistinguishable from a
/// repository that chose to constrain nothing, which is the silent weakening
/// the whole feature exists to prevent.
/// </para>
/// <para>
/// <b>And both join <c>All</c> in the commit that declares them.</b> Slice
/// thirteen's <c>stale-working-copy</c> shipped as a <c>const</c> with a
/// sentence and no entry in <c>All</c> — invisible to the fingerprint, which
/// reads the list, and to the totality guard whose whole job is noticing a new
/// value. It surfaced next door as a 500 when <c>FamilyOf</c> threw.
/// </para>
/// </remarks>
public class NarrowingReasonTests
{
    [Test]
    public async Task Both_kinds_are_declared_and_both_are_in_the_list()
    {
        // THE 0.65.0 LESSON, for the second slice running. A const with a
        // sentence and no entry in All is invisible to everything that matters.
        await Assert.That(ReasonKinds.All).Contains(ReasonKinds.DeclaredAndAbsent);
        await Assert.That(ReasonKinds.All).Contains(ReasonKinds.ForgeUnreachable);
    }

    [Test]
    public async Task They_are_two_different_values()
    {
        await Assert.That(ReasonKinds.DeclaredAndAbsent)
            .IsNotEqualTo(ReasonKinds.ForgeUnreachable)
            .Because("a misconfiguration and an outage are different answers, and one value "
                   + "for both sends the second person to look at the first person's problem.");
    }

    [Test]
    public async Task Each_has_a_family_and_neither_is_a_refusal()
    {
        // A HALT IS NOT A REFUSAL. Nothing was refused - the flight was
        // admitted and cannot be evaluated - so filing these under `refused`
        // would put a flight that is waiting into the same bucket as a
        // document somebody was told no about.
        await Assert.That(ReasonKinds.FamilyOf(ReasonKinds.DeclaredAndAbsent))
            .IsEqualTo(ReasonFamilies.Failed);
        await Assert.That(ReasonKinds.FamilyOf(ReasonKinds.ForgeUnreachable))
            .IsEqualTo(ReasonFamilies.Failed);
    }

    [Test]
    public async Task The_absent_sentence_names_the_repository_and_the_path()
    {
        // § 6: the halt names both, because "a narrowing is missing" sends
        // somebody to look through every repository they have.
        var sentence = Reason.Sentence(
            ReasonKinds.DeclaredAndAbsent, ["payments", ".goodgrief/narrowings/"]);

        await Assert.That(sentence).Contains("payments");
        await Assert.That(sentence).Contains(".goodgrief/narrowings/");
        await Assert.That(sentence.ToLowerInvariant()).Contains("commit")
            .Because("the directory exists on somebody's branch and not at the commit this "
                   + "flight pinned, which is the sentence that stops the argument.");
    }

    [Test]
    public async Task The_unreachable_sentence_says_it_is_not_the_tenants_fault()
    {
        var sentence = Reason.Sentence(ReasonKinds.ForgeUnreachable, ["acme/payments-service"]);

        await Assert.That(sentence).Contains("acme/payments-service");
        await Assert.That(sentence.ToLowerInvariant()).DoesNotContain("declare")
            .Because("an outage is not a configuration problem, and a sentence that talks "
                   + "about declarations invites somebody to go and change one.");
    }

    [Test]
    public async Task Neither_sentence_says_the_repository_constrains_nothing()
    {
        // THE THIRD ANSWER, and the one neither of these may become. A fetch
        // that failed and read as unconstrained is the silent weakening this
        // feature exists to prevent, so neither sentence may leave a reader
        // thinking the flight simply had no narrowings.
        (string Kind, string[] Parameters)[] cases =
        [
            (ReasonKinds.DeclaredAndAbsent, ["payments", ".goodgrief/narrowings/"]),
            (ReasonKinds.ForgeUnreachable, ["acme/payments-service"]),
        ];

        foreach (var (kind, parameters) in cases)
        {
            var sentence = Reason.Sentence(kind, parameters).ToLowerInvariant();

            await Assert.That(sentence).DoesNotContain("no narrowings")
                .Because($"'{kind}' means the answer is UNKNOWN, and a sentence saying there "
                       + "are none is the one thing a read that did not happen must never say.");
            await Assert.That(sentence).DoesNotContain("unconstrained");
        }
    }

    [Test]
    public async Task Each_sentence_is_not_the_kind_repeated()
    {
        (string Kind, string[] Parameters)[] cases =
        [
            (ReasonKinds.DeclaredAndAbsent, ["payments", ".goodgrief/narrowings/"]),
            (ReasonKinds.ForgeUnreachable, ["acme/payments-service"]),
        ];

        foreach (var (kind, parameters) in cases)
        {
            var sentence = Reason.Sentence(kind, parameters);

            await Assert.That(sentence).IsNotEqualTo(kind);
            await Assert.That(sentence.Length).IsGreaterThan(60);
        }
    }
}
