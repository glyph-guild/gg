using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A flight that cannot run for want of a credential says so, and says which of
/// the two ways it happened.
/// </summary>
/// <remarks>
/// <para>
/// <b>The diagnosis was already precise and nothing could render it.</b>
/// <see cref="CredentialResolutionFailure"/> carries the reference - kind,
/// locator, identity, scopes - and a sentence, and the control plane records it
/// on the flight log as <c>credential-unresolved</c>. But there was no reason
/// kind for it, and <see cref="Reason.Sentence"/> THROWS on a kind it does not
/// know - so the <c>why</c> line every other halt renders could never say
/// "this needs a credential" at all. <c>gg doctor</c> names the gap in its own
/// words: <i>it fails at the runner where nobody is looking</i>.
/// </para>
/// <para>
/// <b>Two kinds, not one, and it is the split
/// <see cref="ReasonKinds.DeclaredAndAbsent"/> and
/// <see cref="ReasonKinds.ForgeUnreachable"/> already make.</b> Nothing holding
/// a reference at all is a tenant who has not registered one; a reference that
/// this machine cannot read is a credential that is registered and is somewhere
/// else. Same silence, and the remedies point at different machines -
/// registering it again on the laptop that is already holding it is the wrong
/// one, told confidently.
/// </para>
/// <para>
/// <b>Both are <c>failed</c>, never <c>refused</c>.</b> Nothing was refused: the
/// flight was admitted and the world cannot satisfy it yet. A halt is not a
/// refusal, which is the rule every other waiting kind is already filed under.
/// </para>
/// </remarks>
public class ACredentialBlockedFlightSaysWhichTests
{
    [Test]
    public async Task The_two_ways_are_two_kinds()
    {
        // THE CRITERION, as membership. A kind absent from All is a kind no
        // fingerprint covers and no totality test can see - which is exactly
        // how `stale-working-copy` shipped with no family and stayed green.
        await Assert.That(ReasonKinds.All).Contains(ReasonKinds.NoCredentialRegistered);
        await Assert.That(ReasonKinds.All).Contains(ReasonKinds.CredentialUnreadable);
    }

    [Test]
    public async Task A_flight_waiting_on_a_credential_is_waiting_and_never_refused()
    {
        await Assert.That(ReasonKinds.FamilyOf(ReasonKinds.NoCredentialRegistered))
            .IsEqualTo(ReasonFamilies.Failed);
        await Assert.That(ReasonKinds.FamilyOf(ReasonKinds.CredentialUnreadable))
            .IsEqualTo(ReasonFamilies.Failed);
    }

    [Test]
    public async Task Nothing_registered_names_the_repositories_rather_than_counting_them()
    {
        // THE RULE LeaseGranted.WaitingOn ALREADY STATES: "a number says
        // something is wrong, a name says which credential to register". The
        // repositories are the parameters, joined, the way NoRunnerAdvertises
        // already joins the labels it names.
        var sentence = Reason.Sentence(
            ReasonKinds.NoCredentialRegistered, ["acme/widgets", "acme/payments"]);

        await Assert.That(sentence).Contains("acme/widgets");
        await Assert.That(sentence).Contains("acme/payments");
        await Assert.That(sentence).Contains("gg credential add")
            .Because("a refusal that names a problem and hides its remedy is half a sentence.");
    }

    [Test]
    public async Task An_unreadable_credential_names_the_locator_and_the_machine()
    {
        // THE MACHINE IS THE WHOLE REMEDY. The reference is correct and the
        // secret is somewhere else, so the actionable fact is WHICH machine
        // does not have it - `gg credential add` run again on the laptop that
        // already holds it changes nothing, and would look like it should.
        var sentence = Reason.Sentence(
            ReasonKinds.CredentialUnreadable, ["local:acme/widgets", "pool-dev-2"]);

        await Assert.That(sentence).Contains("local:acme/widgets");
        await Assert.That(sentence).Contains("pool-dev-2");
    }

    [Test]
    public async Task The_two_sentences_do_not_send_a_person_to_the_same_place()
    {
        // WithholdingReasonTests' assertion, for this pair: the failure mode is
        // not silence, it is the WRONG remedy told confidently. A registered
        // credential the runner cannot read must not be reported as one nobody
        // has registered, or a person goes and registers it a second time in
        // the one place it already works.
        var nothing = Reason.Sentence(ReasonKinds.NoCredentialRegistered, ["acme/widgets"]);
        var unreadable = Reason.Sentence(
            ReasonKinds.CredentialUnreadable, ["local:acme/widgets", "pool-dev-2"]);

        await Assert.That(unreadable).IsNotEqualTo(nothing);

        await Assert.That(unreadable).DoesNotContain("no credential is registered")
            .Because("one IS registered. The secret is on another machine, and sending "
                   + "somebody to register it again is the wrong remedy told confidently.");
    }
}
