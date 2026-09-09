using Gg.Client;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Client.Tests;

/// <summary>
/// Two tiers of offerable key, and what separates them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured, not designed.</b> The first version of this offered two
/// data-only keys and refused five as instructions. The walk asked an operator
/// which values they actually wanted to change across a fleet, and three of the
/// five came back — <c>vcs-hosts</c>, <c>destination-apis</c> and
/// <c>control-plane</c>. A need that real is not answered by keeping the
/// refusal, and it is not answered by dropping it either.
/// </para>
/// <para>
/// <b>So the set grows and the guarantee splits.</b> Data keys could apply with
/// nobody watching: wrong relays degrade a connection and grant nothing, and
/// labels can only ever offer to do less or different work. Directed keys — the
/// ones that change where something is fetched from or sent to — may be offered
/// and <b>never apply unattended</b>. A person accepts each one and sees exactly
/// what is being repointed.
/// </para>
/// <para>
/// <b>Nothing calls this unattended today, and the tests do it anyway.</b>
/// <c>accept-unattended</c> was the switch and it is gone: it existed for a pool
/// member, and a pool member has no file to set it in — <c>DockerPoolAdapter</c>
/// creates members with no binds, so nothing from the host filesystem reaches
/// one. Every acceptance now goes through a person typing
/// <c>gg config accept</c>.
/// </para>
/// <para>
/// <b>Which is exactly why these assertions stay.</b> The guard defends a door
/// that is currently locked, and an unexercised guard on a door somebody later
/// unlocks is no guard at all — so <c>attended: false</c> is asked here even
/// though no caller asks it.
/// </para>
/// <para>
/// <b>Whole-offer, not per-key.</b> An offer carrying any directed key needs a
/// person for all of it. Applying the safe half and leaving the rest would make
/// a machine's state depend on which half of a document it agreed with, and
/// there would be no single answer to "is this offer in force". The consequence
/// — adding a directed key stops an offer applying unattended — is the rule
/// working: the offer now redirects something and should stop being automatic.
/// </para>
/// </remarks>
public class AnOfferThatRedirectsNeedsAPersonTests
{
    private static OfferedConfiguration Offering(params (string Key, string Value)[] settings) =>
        new()
        {
            Version = "offer@v1",
            OfferedAt = DateTimeOffset.UnixEpoch,
            Settings = [.. settings.Select(s => new OfferedSetting
            {
                Key = s.Key,
                Value = s.Value,
            })],
        };

    /// <summary>
    /// A machine that accepts offers, asked about one with nobody at it.
    /// </summary>
    /// <remarks>
    /// There is no longer a switch that lets this happen - <c>accept-unattended</c>
    /// was removed, because it was for a pool member and a pool member has no
    /// file to set it in. What remains is the GUARD, and these tests are what
    /// keep it live: it defends a door that is currently locked, and an
    /// unexercised guard on a door somebody later unlocks is no guard at all.
    /// </remarks>
    private static Configuration AcceptingOffers() => new() { AcceptOffered = true };

    [Test]
    public async Task The_three_the_walk_asked_for_are_offerable_now()
    {
        foreach (var key in (string[])["vcs-hosts", "destination-apis", "control-plane"])
        {
            await Assert.That(OfferableKeys.All).Contains(key)
                .Because($"an operator said they change '{key}' across a fleet by hand, and "
                       + "a need that real is not answered by keeping the refusal.");
        }
    }

    [Test]
    public async Task A_directed_key_never_applies_with_nobody_watching()
    {
        foreach (var key in OfferableKeys.All.Where(OfferableKeys.NeedsAPerson))
        {
            var taken = OfferedConfigurations.Accept(
                Offering((key, "forge=host.invalid")), AcceptingOffers(), attended: false);

            await Assert.That(taken.Configuration).IsNull()
                .Because($"'{key}' changes where something is fetched from or sent to, and "
                       + "nothing that does that applies while nobody is looking.");
            await Assert.That(taken.Waiting).IsTrue()
                .Because("it is not refused - it is waiting for somebody.");
        }
    }

    [Test]
    public async Task A_person_may_accept_the_same_offer()
    {
        var taken = OfferedConfigurations.Accept(
            Offering(("vcs-hosts", "forge=host.invalid")),
            new Configuration { AcceptOffered = true },
            attended: true);

        await Assert.That(taken.Refused).IsNull();
        await Assert.That(taken.Configuration!.VcsHosts).IsEqualTo("forge=host.invalid");
    }

    [Test]
    public async Task A_data_key_still_applies_unattended()
    {
        var taken = OfferedConfigurations.Accept(
            Offering(("stun-servers", "stun:relay.invalid:3478")), AcceptingOffers(), attended: false);

        await Assert.That(taken.Configuration!.StunServers)
            .IsEqualTo("stun:relay.invalid:3478");
    }

    [Test]
    public async Task One_directed_key_holds_the_whole_offer_back()
    {
        // WHOLE-OFFER, NOT PER-KEY. Applying the safe half would make a
        // machine's state depend on which half of a document it agreed with,
        // and leave no single answer to "is this offer in force".
        var taken = OfferedConfigurations.Accept(
            Offering(("stun-servers", "stun:relay.invalid:3478"),
                     ("vcs-hosts", "forge=host.invalid")),
            AcceptingOffers(),
            attended: false);

        await Assert.That(taken.Configuration).IsNull()
            .Because("the offer redirects something now, so it stops being automatic - "
                   + "including the half that would have been safe alone.");
    }

    [Test]
    public async Task The_data_tier_is_the_two_it_always_was()
    {
        // The split is stated rather than derived, so growing the offerable set
        // cannot quietly grow what applies unattended.
        await Assert.That(OfferableKeys.All.Where(k => !OfferableKeys.NeedsAPerson(k)))
            .IsEquivalentTo((string[])["stun-servers", "runner-labels"]);
    }

    [Test]
    public async Task An_offered_address_is_still_held_to_the_documents_own_rule()
    {
        // control-plane is offerable now, and an offer must not be able to write
        // something a person could not - including the mistake the address rule
        // exists for.
        var taken = OfferedConfigurations.Accept(
            Offering(("control-plane", "localhost:5199")),
            new Configuration { AcceptOffered = true },
            attended: true);

        await Assert.That(taken.Refused).IsNotNull();
        await Assert.That(taken.Configuration).IsNull();
    }
}
