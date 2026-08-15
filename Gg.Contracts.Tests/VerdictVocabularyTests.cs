namespace Gg.Contracts.Tests;

/// <summary>
/// The verdict a client reads, closed on the wire.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three copies existed and none of them was here.</b>
/// <c>Gg.ControlPlane.Contracts.Events.ObligationOutcomes</c> holds the spellings
/// the control plane writes; <c>Gg.Engine.AdmissionEngine.PreserveOutcomes</c>
/// holds a private pair with a comment explaining that two spellings of
/// <i>violated</i> is a comparison which fails silently in the permissive
/// direction. The one that CROSSES - <see cref="ObligationAttribution.Outcome"/> -
/// was an unconstrained string.
/// </para>
/// <para>
/// <b><see cref="Attachments"/> is in the same file, closed and fingerprinted.</b>
/// The verdict beside it never was, so a value added to it moved no ledger and
/// asked nobody to think about it - which is exactly the defect the vocabulary
/// mechanism exists to close, sitting inside the mechanism's own file.
/// </para>
/// <para>
/// <b>Safe to close now because something asserts the value arrives.</b>
/// <c>good-grief:DecisionVisibilityTests</c> proves a decision's verdict reaches
/// this surface. Closing a vocabulary whose values nothing observes would be a
/// spelling nobody checks against a behaviour nobody measures.
/// </para>
/// </remarks>
public class VerdictVocabularyTests
{
    [Test]
    public async Task The_verdict_is_a_closed_vocabulary()
    {
        await Assert.That(ObligationOutcomes.All.Order().ToList())
            .IsEquivalentTo((string[])["satisfied", "unevaluable", "violated"]);
    }

    [Test]
    public async Task It_is_fingerprinted_the_same_way_the_attachment_beside_it_is()
    {
        // THE POINT OF CLOSING IT. A value added here now moves the contract
        // ledger, which is what forces the conversation an added value needs -
        // the only safe response to an unknown value is to halt, so an added one
        // breaks every prior reader by design.
        var verdicts = typeof(ObligationOutcomes)
            .GetCustomAttributes(typeof(VocabularyOfAttribute), false);
        var attachments = typeof(Attachments)
            .GetCustomAttributes(typeof(VocabularyOfAttribute), false);

        await Assert.That(verdicts.Length).IsEqualTo(1);
        await Assert.That(((VocabularyOfAttribute)verdicts[0]).Fingerprint)
            .IsEqualTo(((VocabularyOfAttribute)attachments[0]).Fingerprint)
            .Because("both are read off the same endpoint by the same client, so a reader that "
                   + "is current for one is current for the other or the pairing means nothing.");
    }

    [Test]
    public async Task Unevaluable_is_here_even_though_nothing_writes_it()
    {
        // THE STORE CAN HOLD IT AND NOTHING PRODUCES IT. A halted flight records
        // no verdict at all - deliberately, because a verdict set with a hole in
        // it reads as a complete answer - so this value is unreachable today.
        //
        // It is declared anyway, because the control plane's vocabulary has it,
        // and a wire vocabulary missing a value the writer can emit is the
        // permissive silence one spelling away.
        await Assert.That(ObligationOutcomes.All).Contains(ObligationOutcomes.Unevaluable);
    }

    [Test]
    public async Task It_is_not_the_attachment_vocabulary_wearing_a_different_name()
    {
        // BOTH SPELL `unevaluable` AND THEY ARE DIFFERENT FACTS. An attachment
        // that is unevaluable means the CONDITION could not be read; a verdict
        // that is unevaluable means the obligation could not be measured. They
        // share a word and answer different questions, and merging them would
        // make "this rule did not apply" and "this rule could not be checked"
        // one value.
        await Assert.That(Attachments.All.Order().ToList())
            .IsNotEquivalentTo(ObligationOutcomes.All.Order().ToList());
        await Assert.That(Attachments.All).Contains("unevaluable");
        await Assert.That(ObligationOutcomes.All).Contains("unevaluable");
    }
}
