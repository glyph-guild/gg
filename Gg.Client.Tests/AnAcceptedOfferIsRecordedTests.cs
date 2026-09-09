using Gg.Client;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Client.Tests;

/// <summary>
/// What a machine remembers about an offer it took.
/// </summary>
/// <remarks>
/// <para>
/// <b>Without a record, the same offer is new for ever.</b> A control plane
/// offers one document per tenant and goes on offering it; a machine that keeps
/// no note of what it accepted has to re-derive "is this new" from the values,
/// and cannot tell a re-offer from a withdrawal-and-reissue at all. So the
/// version is written into the local file, and the file is the record.
/// </para>
/// <para>
/// <b>The file, and nowhere else. The control plane is not told.</b> A machine
/// that reported back would turn a carried offer into a tracked instruction —
/// an operator could see which machines complied, which is a fleet-management
/// feature wearing configuration's clothes, and it would need a write route
/// where there is deliberately only a read. Attribution is the file's own
/// location: it lives under one person's <c>XDG_CONFIG_HOME</c>, and whoever
/// ran the verb is whoever owns it.
/// </para>
/// <para>
/// <b>And the record itself is not offerable</b>, for <c>accept-offered</c>'s
/// reason one step along: a control plane able to write "your machine accepted
/// this" could make a machine stop being offered something it never took.
/// </para>
/// </remarks>
public class AnAcceptedOfferIsRecordedTests
{
    private const string Relays = "stun:relay.invalid:3478";

    private static OfferedConfiguration Offering(
        string version, params (string Key, string Value)[] settings) =>
        new()
        {
            Version = version,
            OfferedAt = DateTimeOffset.UnixEpoch,
            Settings = [.. settings.Select(s => new OfferedSetting
            {
                Key = s.Key,
                Value = s.Value,
            })],
        };

    private static Configuration AcceptingOffers() => new() { AcceptOffered = true };

    [Test]
    public async Task Taking_one_writes_down_which_one()
    {
        var taken = OfferedConfigurations.Accept(
            Offering("offer@7", (OfferableKeys.StunServers, Relays)), AcceptingOffers());

        await Assert.That(taken.Configuration!.AcceptedOffer).IsEqualTo("offer@7")
            .Because("a machine that kept no note of what it accepted would be offered the "
                   + "same document as new for ever.");
    }

    [Test]
    public async Task The_same_offer_again_proposes_nothing()
    {
        var already = new Configuration
        {
            AcceptOffered = true,
            AcceptedOffer = "offer@7",
            StunServers = Relays,
        };

        var taken = OfferedConfigurations.Accept(
            Offering("offer@7", (OfferableKeys.StunServers, Relays)), already);

        await Assert.That(taken.AlreadyAccepted).IsTrue();
        await Assert.That(taken.Configuration).IsNull()
            .Because("there is nothing to write. A proposal here would put a person back in "
                   + "front of a decision they already made.");
        await Assert.That(taken.Refused).IsNull()
            .Because("it is not wrong - it is done.");
        await Assert.That(taken.Waiting).IsFalse();
    }

    [Test]
    public async Task A_new_offer_of_values_already_in_force_is_still_recorded()
    {
        // NOTHING MOVES AND THERE IS STILL SOMETHING TO WRITE, which is the case
        // that decides what the caller keys off. A control plane that reissued
        // the same values under a new version has made a new document, and a
        // machine that declined to record it would be re-proposed the same
        // settings every time anybody asked.
        var already = new Configuration
        {
            AcceptOffered = true,
            AcceptedOffer = "offer@7",
            StunServers = Relays,
        };

        var taken = OfferedConfigurations.Accept(
            Offering("offer@8", (OfferableKeys.StunServers, Relays)), already);

        await Assert.That(taken.AlreadyAccepted).IsFalse();
        await Assert.That(taken.Configuration!.AcceptedOffer).IsEqualTo("offer@8");
        await Assert.That(taken.Changed).IsFalse()
            .Because("`Changed` is about the settings. The record moving is not a machine "
                   + "being reconfigured, and telling a person otherwise would make every "
                   + "reissue look like a change they should read.");
    }

    [Test]
    public async Task Everything_the_offer_did_not_name_is_left_exactly_as_it_was()
    {
        // S35.9-01. Asserted over the whole table rather than on a couple of
        // members, because the one that gets clobbered is the one nobody
        // thought to name in a test.
        var before = new Configuration
        {
            AcceptOffered = true,
            ControlPlane = "https://control.invalid",
            Editor = "hx",
            TakeCommand = "claude",
            IntentHosts = "tracker=work.invalid",
            IntentReaders = "tracker=read-work",
            VcsHosts = "forge=git.invalid",
            DestinationApis = "forge=api.git.invalid",
            ExecutorBinary = "/usr/local/bin/agent",
            RunnerLabels = "linux,arm64",
            RunnerHoldSeconds = 25,
            PoolEndpoint = "https://pool.invalid",
            StunServers = "stun:old.invalid:3478",
        };

        var taken = OfferedConfigurations.Accept(
            Offering("offer@9", (OfferableKeys.StunServers, Relays)), before);

        var after = taken.Configuration!;

        foreach (var member in Configuration.Members
                     .Where(m => !string.Equals(m.Key, OfferableKeys.StunServers, StringComparison.Ordinal)))
        {
            await Assert.That(member.Get(after)).IsEqualTo(member.Get(before))
                .Because($"'{member.Key}' was not offered, so accepting must not touch it.");
        }

        await Assert.That(after.StunServers).IsEqualTo(Relays);
        await Assert.That(after.AcceptOffered).IsEqualTo(before.AcceptOffered)
            .Because("the key that decides whether offers are accepted at all is the one an "
                   + "offer must never be able to move.");
    }

    [Test]
    public async Task A_control_plane_cannot_write_the_record_itself()
    {
        await Assert.That(OfferableKeys.All).DoesNotContain("accepted-offer")
            .Because("a control plane that could write 'your machine accepted this' could "
                   + "make a machine stop being offered something it never took.");

        var taken = OfferedConfigurations.Accept(
            Offering("offer@7", ("accepted-offer", "offer@6")), AcceptingOffers());

        await Assert.That(taken.Refused).IsNotNull();
        await Assert.That(taken.Configuration).IsNull();
    }

    [Test]
    public async Task A_blank_record_is_refused_like_every_other_blank()
    {
        var refusal = Configuration.Validate(new Configuration { AcceptedOffer = "  " });

        await Assert.That(refusal).IsNotNull()
            .Because("blank is not the same as unset here either: it would read as 'nothing "
                   + "accepted' while somebody had written a value.");
        await Assert.That(refusal).Contains("accepted-offer");
    }
}
