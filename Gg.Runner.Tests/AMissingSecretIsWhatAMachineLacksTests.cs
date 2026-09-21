using Gg.Contracts;
using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// A tracker this machine cannot use is something it LACKS, not a reason it will
/// not start (slice forty-seven, step 3, rule 2).
/// </summary>
/// <remarks>
/// <para>
/// <b>What it does today.</b> <c>WiqlWorkItemSink</c> refuses an absent
/// credential at construction, and <c>TrackerConfiguration.FromEnvironment</c>
/// builds every declared sink at once - so one tracker with no resolvable secret
/// throws where the runner is composed. The justification written beside it is
/// that this fails "at start-up, in front of the person configuring the machine".
/// </para>
/// <para>
/// <b>On a fleet host that person is a systemd unit.</b> Once a profile can
/// offer <c>tracker-apis</c>, that throw is reachable by a document somebody
/// applied somewhere else entirely - and an offer that can stop a runner from
/// starting is worse than the hand-typing it replaces, because a machine that
/// will not start cannot be told anything, including that it was wrong.
/// </para>
/// <para>
/// <b>So the tracker is still declared and the WRITE is what refuses.</b> The
/// destination keeps its entry, which matters: the loop's own refusal for an
/// unknown destination says "this runner has no tracker declared for it", and
/// that sentence would be false here and send somebody to the wrong document.
/// </para>
/// </remarks>
public class AMissingSecretIsWhatAMachineLacksTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    private const string ATracker = "backlog=https://forge.example/acme|local:backlog-token";

    private static HttpClient AClient(string host) => new() { BaseAddress = new Uri(host) };

    /// <summary>Resolves nothing, which is the machine this is about.</summary>
    private static string? NoSecret(string locator) => null;

    [Test]
    public async Task A_tracker_whose_credential_does_not_resolve_leaves_the_runner_running()
    {
        var sinks = TrackerConfiguration.FromEnvironment(AClient, ATracker, NoSecret);

        await Assert.That(sinks.ContainsKey("backlog")).IsTrue()
            .Because("the destination IS declared - the profile said so - so it keeps its entry. "
                   + "Dropping it would make the loop answer 'no tracker declared for it', which "
                   + "is false and sends somebody to the wrong document.");
    }

    [Test]
    public async Task And_the_write_is_what_refuses_it_naming_the_reference()
    {
        var sinks = TrackerConfiguration.FromEnvironment(AClient, ATracker, NoSecret);

        var refused = await Assert.That(async () => await sinks["backlog"].PerformAsync(
                [new WorkItemProposal { Operation = "comment", Reason = "because" }], "a-key"))
            .Throws<InvalidOperationException>();

        await Assert.That(refused!.Message).Contains("local:backlog-token", StringComparison.Ordinal)
            .Because("the reference is the actionable half: somebody has to put that secret on "
                   + "this machine, and a message that does not name it says to go looking.");
    }

    [Test]
    public async Task A_declaration_this_build_cannot_parse_leaves_the_runner_running_too()
    {
        // HOW THIS ARRIVES: a value written by a NEWER contract, offered to an
        // older machine. Refusing to start is the one response that cannot be
        // corrected, because the correction arrives as configuration.
        var sinks = TrackerConfiguration.FromEnvironment(
            AClient, "this-is-not-an-entry", NoSecret);

        await Assert.That(sinks).IsEmpty()
            .Because("nothing could be keyed from it, so there is no destination to declare - "
                   + "but the runner is still running, which is the whole of rule 2.");
    }

    [Test]
    public async Task A_trackers_credential_is_measured_without_the_profile_naming_it_twice()
    {
        // DERIVED FROM THE ENTRY, so a profile states a tracker once. `credentials`
        // is the list a reader scans to see what a machine must hold, and a
        // derived reference is invisible there - which is the open question this
        // slice records rather than settles.
        var reading = await ProfileReadiness.MeasureAsync(
            AProfileWith(trackers: ["ado=https://forge.example/acme|local:ticket-token"]),
            declaredAgent: null,
            resolve: (reference, _) => Task.FromResult<string?>(
                reference == "local:ticket-token" ? "not in this machine's store." : null),
            reach: (_, _) => Task.FromResult<string?>(null),
            now: T0);

        await Assert.That(reading.Items.Any(i =>
                i.Kind == ReadinessKinds.Credential
                && i.Subject == "local:ticket-token"
                && !i.Met))
            .IsTrue()
            .Because("a machine told where to read with nothing to open it lacks something, and "
                   + "naming it is what a bring-up ask is for.");
    }

    [Test]
    public async Task A_provider_claimed_by_both_a_reader_and_a_host_is_still_refused()
    {
        // S47.1-04, moved here from step 1: the refusal lives in
        // IntentConfiguration, which Gg.Contracts.Tests cannot reference, and it
        // is about the MACHINE's reading rather than the document's shape.
        //
        // WHAT CHANGES IS WHERE THE VALUE CAME FROM. This refusal was written for
        // two shell variables an operator edited; one of them can now arrive from
        // a profile somebody applied elsewhere, and "an operator who edited the
        // variable they had in mind and saw no change at all" is the same bad
        // afternoon whichever side wrote it.
        var both = () => Gg.Local.IntentConfiguration.FromEnvironment(
            declaration: "ado=some-reader --flag",
            served: "ado=https://forge.example/acme|local:ticket-token");

        await Assert.That(both).Throws<InvalidOperationException>()
            .Because("no default settles which wins, so a key claimed twice is refused where it "
                   + "is read rather than resolved by a precedence nobody wrote down.");
    }

    private static FleetProfileState AProfileWith(IReadOnlyList<string> trackers) => new()
    {
        Name = "dev-worker",
        Version = "v1",
        AppliedAt = T0,
        Profile = new FleetProfile
        {
            Roles = [ProfileRoles.Run],
            Environment = "dev",
            Trackers = trackers,
        },
    };
}
