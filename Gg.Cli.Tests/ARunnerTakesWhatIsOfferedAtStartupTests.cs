using Gg.Client;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// What a runner does with an offer, before it composes anything.
/// </summary>
/// <remarks>
/// <para>
/// <b>At startup, and deliberately not on the heartbeat that carries it.</b>
/// The runner reads its labels, its hold, its relays and its executor ONCE, into
/// locals handed to the loop — so a file written mid-run changes nothing until
/// the process restarts. Applying at startup means a machine brought up fresh is
/// configured before it claims anything, with no window in which it advertises
/// the wrong labels, and it needs no mechanism for mutating a live loop.
/// </para>
/// <para>
/// <b>Unattended, so only the unwatched tier can land.</b> Relay addresses and
/// runner labels may apply with nobody watching; a directed key changes where
/// code is fetched from or sent to and still waits for a person. Nothing here
/// weakens that — it asks <c>OfferedConfigurations.Accept</c> with
/// <c>attended: false</c>, which is the guard that was written when nothing
/// called it that way.
/// </para>
/// <para>
/// <b>And it is loud about what it did not do.</b> A runner has nobody at it, so
/// an offer it cannot take has to reach a log or it reaches nothing: an operator
/// who set a directed key fleet-wide and saw one host ignore it would otherwise
/// have no way to learn why.
/// </para>
/// <para>
/// <b>Pure, so the network is somebody else's problem.</b> The composition root
/// fetches; this decides. That split is what lets every case below be a unit
/// test rather than a source scan of a file of top-level statements.
/// </para>
/// </remarks>
public class ARunnerTakesWhatIsOfferedAtStartupTests
{
    private const string Path = "/somewhere/config.json";

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

    private static Configuration Accepting() => new() { AcceptOffered = true };

    [Test]
    public async Task Nothing_offered_leaves_everything_where_it_was()
    {
        var decided = OfferedAtStartup.Decide(null, Accepting(), Path);

        await Assert.That(decided.Write).IsNull();
        await Assert.That(decided.Note).IsNull()
            .Because("a fleet nobody is reconfiguring is the ordinary state, and a line "
                   + "about it on every boot is a line nobody reads.");
        await Assert.That(decided.InForce!.AcceptOffered).IsTrue();
    }

    [Test]
    public async Task An_unwatched_setting_is_taken_and_becomes_what_is_in_force()
    {
        var decided = OfferedAtStartup.Decide(
            Offering("offer@7", (OfferableKeys.StunServers, Relays)), Accepting(), Path);

        await Assert.That(decided.Write!.StunServers).IsEqualTo(Relays);
        await Assert.That(decided.Write.AcceptedOffer).IsEqualTo("offer@7");

        await Assert.That(decided.InForce).IsEqualTo(decided.Write)
            .Because("the composition below reads labels and relays from what this returns. "
                   + "Handing back the OLD document would write a file and compose from what "
                   + "it replaced - the feature doing nothing, quietly.");
    }

    [Test]
    public async Task A_directed_key_is_not_taken_and_the_reason_is_said()
    {
        var decided = OfferedAtStartup.Decide(
            Offering("offer@7", (OfferableKeys.VcsHosts, "forge=git.invalid")),
            Accepting(),
            Path);

        await Assert.That(decided.Write).IsNull()
            .Because("it repoints where code is fetched from, and there is nobody here.");

        await Assert.That(decided.Note).IsNotNull();
        await Assert.That(decided.Note!).Contains("gg config accept offer@7", StringComparison.Ordinal)
            .Because("a runner has nobody at it, so the log is the only place an operator "
                   + "learns why one host ignored what the fleet took - and it should say "
                   + "what would take it.");
    }

    [Test]
    public async Task A_machine_that_accepts_nothing_says_so_rather_than_ignoring_it()
    {
        var decided = OfferedAtStartup.Decide(
            Offering("offer@7", (OfferableKeys.StunServers, Relays)), new Configuration(), Path);

        await Assert.That(decided.Write).IsNull();
        await Assert.That(decided.Note!).Contains("accept-offered", StringComparison.Ordinal)
            .Because("the answer to 'why did nothing happen' is one key in one file, and a "
                   + "machine with nobody at it has to write it down.");
        await Assert.That(decided.Note!).Contains(Path, StringComparison.Ordinal)
            .Because("and where that file is, because whoever reads this log is not here.");
    }

    [Test]
    public async Task An_offer_this_gg_refuses_is_said_and_changes_nothing()
    {
        var decided = OfferedAtStartup.Decide(
            Offering("offer@7", ("executor-binary", "/tmp/whatever")), Accepting(), Path);

        await Assert.That(decided.Write).IsNull();
        await Assert.That(decided.Note!).Contains("executor-binary", StringComparison.Ordinal);
    }

    [Test]
    public async Task One_already_in_force_is_taken_quietly()
    {
        // THE STEADY STATE, and it is every boot after the first. A note here
        // would put a line in the log on every restart of every machine, which
        // is how a log stops being read.
        var decided = OfferedAtStartup.Decide(
            Offering("offer@7", (OfferableKeys.StunServers, Relays)),
            new Configuration
            {
                AcceptOffered = true,
                AcceptedOffer = "offer@7",
                StunServers = Relays,
            },
            Path);

        await Assert.That(decided.Write).IsNull()
            .Because("the file already says this. Rewriting it every boot would churn a "
                   + "document a person owns.");
        await Assert.That(decided.Note).IsNull();
        await Assert.That(decided.InForce!.StunServers).IsEqualTo(Relays);
    }

    [Test]
    public async Task A_reissue_of_values_already_in_force_records_the_version()
    {
        // Nothing moves and there is still something to write, or the same
        // offer arrives as new for ever.
        var decided = OfferedAtStartup.Decide(
            Offering("offer@8", (OfferableKeys.StunServers, Relays)),
            new Configuration
            {
                AcceptOffered = true,
                AcceptedOffer = "offer@7",
                StunServers = Relays,
            },
            Path);

        await Assert.That(decided.Write!.AcceptedOffer).IsEqualTo("offer@8");
        await Assert.That(decided.Write.StunServers).IsEqualTo(Relays);
    }

    [Test]
    public async Task A_machine_with_no_file_at_all_takes_nothing()
    {
        // A FRESH HOST WITH NOTHING WRITTEN. accept-offered has no environment
        // variable by design, so a machine nobody wrote a file for has not
        // opted in - and this is the case that makes the seed file necessary
        // rather than optional.
        var decided = OfferedAtStartup.Decide(
            Offering("offer@7", (OfferableKeys.StunServers, Relays)), file: null, path: Path);

        await Assert.That(decided.Write).IsNull();
        await Assert.That(decided.Note!).Contains("accept-offered", StringComparison.Ordinal);
        await Assert.That(decided.InForce).IsNull()
            .Because("no file is still no file. Inventing an empty one here would write a "
                   + "document nobody asked for on the first boot of every machine.");
    }
}
