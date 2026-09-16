using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// The watch's bound is the destination every other nominator's is.
/// </summary>
/// <remarks>
/// <para>
/// <b>S39.1-03, and the claim is ONE SPELLING rather than two that agree
/// today.</b> ADR-0022 § 5 states it: <c>opens:</c>, <c>may-select</c>,
/// <c>opens-as</c> and <c>requires</c>, the same record an agent's nominating
/// flight carries — and slice thirty-eight reached it one nominator earlier for
/// a repository. <i>"A second menu shaped like `opens:` would have its own
/// validation, its own direction arms, and no way for a narrowing to reach it,
/// which is how one of two spellings stops agreeing."</i>
/// </para>
/// <para>
/// <b>So the proof is that the sentences MATCH</b>, not that each door refuses
/// separately. Two validators that both say no today are two validators; one
/// validator is a refusal a caller can read in one place. Asserting the text is
/// identical is what makes a copy fail here rather than in a year.
/// </para>
/// <para>
/// <b>And the bound is REQUIRED on a watch, where a repository's is
/// optional</b> — the one place the two deliberately differ. A repository
/// existed before the member did, so absent had to mean today's behaviour
/// (rule 8 of thirty-eight). A watch does not exist without its bound: nothing
/// else tells a sweep what kind to nominate, so a watch with none would sweep
/// and produce rows nothing could open.
/// </para>
/// </remarks>
public class AWatchsBoundIsADestinationTests
{
    private static RegisterRepositoryRequest ARepository(Destination? nominates) => new()
    {
        Name = "payments",
        Provider = "a-forge",
        Id = "an-immutable-forge-id",
        Path = "acme/payments-service",
        Nominates = nominates,
    };

    [Test]
    public async Task A_bound_of_the_wrong_kind_is_refused_in_the_same_words_at_both_doors()
    {
        // THE ONE-SPELLING PROOF. Same malformed bound, two documents, and the
        // refusal has to be the same sentence - because it is the same rule,
        // reached from two places rather than written twice.
        var wrong = AWatchDeclaresReferencesTests.ABound()
            with { Kind = DestinationKinds.PullRequest };

        var atTheWatch = WatchDocument.Validate(
            AWatchDeclaresReferencesTests.AWatch() with { Nominates = wrong });
        var atTheRepository = RegisterRepositoryRequest.Validate(ARepository(wrong));

        await Assert.That(atTheWatch).IsNotNull();
        await Assert.That(atTheRepository).IsNotNull();

        await Assert.That(atTheWatch).IsEqualTo(atTheRepository)
            .Because("one rule refused both, so one sentence came back. Two sentences would "
                   + "be two validators agreeing today - which is the state that stops.");
    }

    [Test]
    public async Task An_opens_as_nobody_declared_is_refused_in_the_same_words_too()
    {
        var wrong = AWatchDeclaresReferencesTests.ABound() with { OpensAs = "sometimes" };

        var atTheWatch = WatchDocument.Validate(
            AWatchDeclaresReferencesTests.AWatch() with { Nominates = wrong });
        var atTheRepository = RegisterRepositoryRequest.Validate(ARepository(wrong));

        await Assert.That(atTheWatch).IsNotNull();
        await Assert.That(atTheWatch).IsEqualTo(atTheRepository);
    }

    [Test]
    public async Task A_watch_with_no_bound_is_refused_where_a_repository_with_none_is_not()
    {
        // THE ONE PLACE THEY DIFFER, asserted from both sides so the asymmetry
        // is deliberate rather than an oversight in whichever door was written
        // second.
        await Assert.That(WatchDocument.Validate(
            AWatchDeclaresReferencesTests.AWatch() with { Nominates = null })).IsNotNull()
            .Because("nothing else tells a sweep what kind to nominate, so a watch without a "
                   + "bound would sweep and leave rows nothing can open.");

        await Assert.That(RegisterRepositoryRequest.Validate(ARepository(null))).IsNull()
            .Because("a repository existed before the member did, so absent is today's "
                   + "behaviour - rule 8 of thirty-eight, and refusing it would refuse every "
                   + "registration in existence.");
    }

    [Test]
    public async Task Gated_is_readable_on_a_watch_and_means_what_it_means_everywhere()
    {
        // `opens-as` REACHING A THIRD NOMINATOR. ADR-0022 section 5 says
        // `requires` can only mean the second gate here, because no flight
        // stands in front of the destination - and `opens-as` is the member
        // that already expressed exactly that.
        var gated = AWatchDeclaresReferencesTests.ABound()
            with { OpensAs = DestinationOpening.Gated };

        await Assert.That(WatchDocument.Validate(
            AWatchDeclaresReferencesTests.AWatch() with { Nominates = gated })).IsNull();

        await Assert.That(DestinationOpening.Of(gated)).IsEqualTo(DestinationOpening.Gated);
    }
}
