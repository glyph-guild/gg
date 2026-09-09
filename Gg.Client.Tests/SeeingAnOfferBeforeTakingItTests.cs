using Gg.Client;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Client.Tests;

/// <summary>
/// `gg config offered` and `gg config accept` — the whole attended path.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two verbs because seeing and taking are two acts.</b> The directed keys
/// are offerable ONLY because a person sees what is being repointed, so a single
/// verb that fetched and applied in one breath would remove the thing that made
/// them safe to offer — the person would have consented to whatever arrived
/// rather than to a document they read.
/// </para>
/// <para>
/// <b>Which is why accept NAMES A VERSION.</b> <c>gg config offered</c> prints
/// the line to run, version included; <c>gg config accept</c> refuses anything
/// else. Between the two commands a control plane can change what it offers, and
/// an accept with no version would silently apply the replacement. Naming it is
/// the difference between accepting a document and consenting to a channel.
/// </para>
/// <para>
/// <b>And offered refuses nothing.</b> A control plane offering something this
/// gg will not take is exactly what a person needs told, so the refusal is part
/// of the answer rather than an exception — the same split
/// <c>gg config validate</c> draws. <c>accept</c> throws, because it was asked
/// to do something and did not.
/// </para>
/// </remarks>
public class SeeingAnOfferBeforeTakingItTests
{
    private const string Relays = "stun:relay.invalid:3478";

    private static string ATempFile() =>
        Path.Combine(Path.GetTempPath(), $"gg-offer-{Guid.NewGuid():N}.json");

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

    private static OfferedView Seen(OfferedConfiguration? offered, Configuration? file) =>
        ((VerbResult.ConfigOffered)ConfigCommands.Offered(
            offered, file, path: "/somewhere/config.json")).Value;

    // ---- S35.9-04: a person can see what an offer would change ----

    [Test]
    public async Task Every_setting_is_shown_beside_what_it_would_replace()
    {
        var view = Seen(
            Offering("offer@7",
                (OfferableKeys.StunServers, Relays),
                (OfferableKeys.VcsHosts, "forge=git.invalid")),
            new Configuration { AcceptOffered = true, StunServers = "stun:old.invalid:3478" });

        await Assert.That(view.Version).IsEqualTo("offer@7");

        var relays = view.Changes.Single(c => c.Key == OfferableKeys.StunServers);

        await Assert.That(relays.Offered).IsEqualTo(Relays);
        await Assert.That(relays.Current).IsEqualTo("stun:old.invalid:3478")
            .Because("what is being replaced is the half of this a person cannot get "
                   + "anywhere else, and the whole reason the directed keys are offerable.");
        await Assert.That(relays.Changes).IsTrue();

        var forge = view.Changes.Single(c => c.Key == OfferableKeys.VcsHosts);

        await Assert.That(forge.Current).IsNull()
            .Because("nothing is being replaced, and null is how the rendering knows to say "
                   + "so rather than printing an empty column.");
    }

    [Test]
    public async Task A_setting_already_in_force_says_it_would_change_nothing()
    {
        var view = Seen(
            Offering("offer@7", (OfferableKeys.StunServers, Relays)),
            new Configuration { AcceptOffered = true, StunServers = Relays });

        await Assert.That(view.Changes.Single().Changes).IsFalse()
            .Because("a person reading a list where every line looks like a change cannot "
                   + "find the one that is.");
    }

    [Test]
    public async Task Nothing_offered_is_said_rather_than_shown_as_an_empty_list()
    {
        var view = Seen(null, new Configuration { AcceptOffered = true });

        await Assert.That(view.Version).IsNull();
        await Assert.That(view.Changes).IsEmpty();

        var text = VerbOutput.ToText(new VerbResult.ConfigOffered(view));

        await Assert.That(text).Contains("nothing", StringComparison.OrdinalIgnoreCase)
            .Because("a fleet nobody is reconfiguring is the ordinary state, and a person "
                   + "checking has to read an answer rather than interpret a blank.");
    }

    [Test]
    public async Task A_machine_that_accepts_nothing_still_sees_what_is_waiting()
    {
        // THE FEATURE IS NOT INVISIBLE UNTIL SOMEBODY TURNS IT ON. An operator
        // finds out there is an offer to look at by looking, and the posture
        // line is how they find out why nothing happened.
        var view = Seen(Offering("offer@7", (OfferableKeys.StunServers, Relays)), new Configuration());

        await Assert.That(view.Version).IsEqualTo("offer@7");
        await Assert.That(view.AcceptsOffered).IsFalse();
        await Assert.That(view.Changes).IsNotEmpty();

        var text = VerbOutput.ToText(new VerbResult.ConfigOffered(view));

        await Assert.That(text).Contains("accept-offered", StringComparison.Ordinal)
            .Because("the answer to 'why would nothing happen' is one key in one file, and "
                   + "naming it is cheaper than making somebody search for it.");
    }

    [Test]
    public async Task An_offer_this_gg_refuses_is_reported_rather_than_thrown()
    {
        var view = Seen(
            Offering("offer@7", ("executor-binary", "/tmp/whatever")),
            new Configuration { AcceptOffered = true });

        await Assert.That(view.Refused).IsNotNull();
        await Assert.That(view.Refused!).Contains("executor-binary", StringComparison.Ordinal)
            .Because("a control plane offering something this gg will not take is exactly "
                   + "what a person needs told, by name.");
    }

    [Test]
    public async Task One_already_accepted_says_so_instead_of_asking_again()
    {
        var view = Seen(
            Offering("offer@7", (OfferableKeys.StunServers, Relays)),
            new Configuration { AcceptOffered = true, AcceptedOffer = "offer@7", StunServers = Relays });

        await Assert.That(view.AlreadyAccepted).IsTrue();

        var text = VerbOutput.ToText(new VerbResult.ConfigOffered(view));

        await Assert.That(text).DoesNotContain("gg config accept offer@7", StringComparison.Ordinal)
            .Because("printing the line to run for a document already in force is asking "
                   + "somebody to make a decision twice.");
    }

    [Test]
    public async Task The_line_to_run_carries_the_version_a_person_just_read()
    {
        var text = VerbOutput.ToText(new VerbResult.ConfigOffered(Seen(
            Offering("offer@7", (OfferableKeys.StunServers, Relays)),
            new Configuration { AcceptOffered = true })));

        await Assert.That(text).Contains("gg config accept offer@7", StringComparison.Ordinal)
            .Because("accept refuses anything but the version offered, so the verb that "
                   + "shows the offer is what has to hand over the version.");
    }

    // ---- accept: the write, and what it refuses ----

    [Test]
    public async Task Accepting_writes_the_offer_and_the_record_together()
    {
        var path = ATempFile();

        try
        {
            ConfigurationFile.Write(
                new Configuration { AcceptOffered = true, Editor = "hx" }, path);

            var view = ((VerbResult.ConfigOffered)ConfigCommands.Accept(
                Offering("offer@7", (OfferableKeys.StunServers, Relays)),
                version: "offer@7",
                path: path)).Value;

            await Assert.That(view.Accepted).IsTrue();

            var after = ConfigurationFile.Read(path).Configuration!;

            await Assert.That(after.StunServers).IsEqualTo(Relays);
            await Assert.That(after.AcceptedOffer).IsEqualTo("offer@7");
            await Assert.That(after.Editor).IsEqualTo("hx")
                .Because("accepting rewrote the file, and everything the offer did not name "
                       + "has to survive that.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Accepting_a_version_that_is_not_what_is_offered_is_refused()
    {
        // THE RACE THE VERSION EXISTS FOR. Between reading an offer and taking
        // it, a control plane can offer something else - and an accept with no
        // version would apply the replacement to somebody who never saw it.
        var path = ATempFile();

        try
        {
            ConfigurationFile.Write(new Configuration { AcceptOffered = true }, path);

            var refused = Assert.Throws<ConfigurationRefused>(() => ConfigCommands.Accept(
                Offering("offer@8", (OfferableKeys.StunServers, Relays)),
                version: "offer@7",
                path: path));

            await Assert.That(refused!.Message).Contains("offer@8", StringComparison.Ordinal)
                .Because("the refusal has to name what IS offered, or somebody retypes the "
                       + "version they already had.");

            await Assert.That(ConfigurationFile.Read(path).Configuration!.StunServers).IsNull()
                .Because("nothing was written.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Accepting_with_nothing_offered_is_refused_rather_than_silent()
    {
        var path = ATempFile();

        try
        {
            ConfigurationFile.Write(new Configuration { AcceptOffered = true }, path);

            var refused = Assert.Throws<ConfigurationRefused>(
                () => ConfigCommands.Accept(null, version: "offer@7", path: path));

            await Assert.That(refused).IsNotNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Accepting_on_a_machine_that_accepts_nothing_is_refused_and_names_the_key()
    {
        var path = ATempFile();

        try
        {
            ConfigurationFile.Write(new Configuration { Editor = "hx" }, path);

            var refused = Assert.Throws<ConfigurationRefused>(() => ConfigCommands.Accept(
                Offering("offer@7", (OfferableKeys.StunServers, Relays)),
                version: "offer@7",
                path: path));

            await Assert.That(refused!.Message).Contains("accept-offered", StringComparison.Ordinal)
                .Because("typing the verb is not the same as opening the file, which is the "
                       + "deliberate friction on deciding to let something else configure "
                       + "this machine.");

            await Assert.That(ConfigurationFile.Read(path).Configuration!.StunServers).IsNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Accepting_one_already_in_force_writes_nothing_and_says_nothing_changed()
    {
        var path = ATempFile();

        try
        {
            ConfigurationFile.Write(
                new Configuration
                {
                    AcceptOffered = true,
                    AcceptedOffer = "offer@7",
                    StunServers = Relays,
                },
                path);

            var written = File.ReadAllText(path);

            var view = ((VerbResult.ConfigOffered)ConfigCommands.Accept(
                Offering("offer@7", (OfferableKeys.StunServers, Relays)),
                version: "offer@7",
                path: path)).Value;

            await Assert.That(view.AlreadyAccepted).IsTrue();
            await Assert.That(view.Accepted).IsFalse();
            await Assert.That(File.ReadAllText(path)).IsEqualTo(written)
                .Because("re-accepting a document in force is not an error and is also not "
                       + "a write - the file is untouched byte-for-byte.");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
