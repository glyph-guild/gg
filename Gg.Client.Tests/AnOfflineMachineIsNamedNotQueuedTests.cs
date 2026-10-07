using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A machine that was offline is named as not reached, and nothing is held for it.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.5-04, and the sentence gg already wrote about this.</b>
/// <c>SendOutcome.Offline</c> exists because <i>"a credential cannot be left somewhere
/// for a machine to collect later, because nothing stores it in between"</i> — which is
/// ADR-0037's problem statement, and still true: this slice builds no vault.
/// </para>
/// <para>
/// <b>So a broadcast must not look like a queue.</b> The failure mode is a push that
/// reports eight successes because it "sent" to eight machines, two of which were
/// offline and will never get it. A person who believed that would stop thinking about
/// those two, and the next thing they learn is a flight failing on a credential they
/// watched being distributed.
/// </para>
/// <para>
/// <b>Named BEFORE the push rather than discovered during it.</b> An offline machine is
/// a fact the fleet already reports — <c>RunnerStates.Offline</c>, with
/// <c>LastHeartbeatAt</c> beside it — so the audience can say so while a person is still
/// reading the list, rather than as an error afterwards.
/// </para>
/// <para>
/// <b>A machine is still IN the audience when it is offline.</b> Dropping it would be
/// the quiet omission S64.5-07 refuses for members, arriving by a different route: it
/// needs the credential, somebody declared that, and the honest row says so and says it
/// cannot be reached now.
/// </para>
/// </remarks>
public class AnOfflineMachineIsNamedNotQueuedTests
{
    private const string Locator = "local:acme/widgets";

    private static RunnerSummary ARunner(string id, string label, string state, string? profile) =>
        new()
        {
            RunnerId = id,
            Label = label,
            State = state,
            Profile = profile,
        };

    private static FleetProfileState AProfile(string name, params string[] credentials) =>
        new()
        {
            Name = name,
            Version = "v1",
            AppliedAt = DateTimeOffset.UnixEpoch,
            Profile = new FleetProfile { Environment = name, Credentials = credentials },
        };

    [Test]
    public async Task An_offline_machine_is_in_the_audience_and_not_reachable()
    {
        var audience = CredentialAudience.For(
            Locator,
            [ARunner("r1", "vmlinux001", RunnerStates.Offline, "dev")],
            [AProfile("dev", Locator)]);

        await Assert.That(audience.Count).IsEqualTo(1)
            .Because("it needs the credential and a document says so. Dropping it is the quiet "
                   + "omission that makes a list read as complete.");

        await Assert.That(audience[0].Reachable).IsFalse()
            .Because("nothing can reach it now, and nothing stores a credential in between - so "
                   + "promising it a push would be promising a thing gg cannot do.");
    }

    [Test]
    public async Task And_the_printed_line_says_it_was_not_reached()
    {
        var said = new List<string>();

        var offline = new CredentialAudienceRow(
            RunnerId: "r1", Label: "vmlinux001", Locator: Locator,
            Declared: true, Reported: false, Reachable: false, Through: null);

        CredentialBroadcast.Announce(Locator, [offline], said.Add);

        var line = said.Single(s => s.Contains("vmlinux001", StringComparison.Ordinal));

        await Assert.That(line).Contains("not")
            .Because("a line identical to a reachable machine's would report a push that did not "
                   + "happen. Said: " + line);
    }

    [Test]
    public async Task A_busy_machine_is_still_reachable()
    {
        // BUSY IS NOT OFFLINE, and conflating them would make a broadcast skip every
        // machine doing work - which is most of them, on a fleet that is being used.
        var audience = CredentialAudience.For(
            Locator,
            [ARunner("r1", "vmlinux001", RunnerStates.Busy, "dev")],
            [AProfile("dev", Locator)]);

        await Assert.That(audience[0].Reachable).IsTrue()
            .Because("a machine mid-flight answers its channel; the credential arrives and is "
                   + "written, which is what the resident path already does.");
    }

    [Test]
    public async Task An_idle_machine_is_reachable()
    {
        var audience = CredentialAudience.For(
            Locator,
            [ARunner("r1", "vmlinux001", RunnerStates.Idle, "dev")],
            [AProfile("dev", Locator)]);

        await Assert.That(audience[0].Reachable).IsTrue()
            .Because("the ordinary case, asserted so that a guard marking everything unreachable "
                   + "cannot pass the offline test and break the feature.");
    }

    [Test]
    public async Task Nothing_in_the_audience_promises_a_later_delivery()
    {
        // THE WORDS MATTER HERE, because "queued", "pending" and "will receive" are all
        // things a person would read as a promise. There is no store in between; the
        // only honest future tense is about what a person does next.
        var said = new List<string>();

        CredentialBroadcast.Announce(
            Locator,
            [new CredentialAudienceRow(
                RunnerId: "r1", Label: "vmlinux001", Locator: Locator,
                Declared: true, Reported: false, Reachable: false, Through: null)],
            said.Add);

        var all = string.Join(" ", said);

        foreach (var promise in (string[])["queued", "pending", "will be delivered", "when it comes back"])
        {
            await Assert.That(all).DoesNotContain(promise, StringComparison.OrdinalIgnoreCase)
                .Because($"'{promise}' says gg is holding the credential for it, and nothing stores "
                       + "one in between. Said: " + all);
        }
    }
}
