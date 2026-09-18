using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A resident runner asks for any sweep it can serve, naming no watch, and
/// says which trackers it can reach.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found by running the walk, and the owner's words.</b> <i>"Today a watch
/// sweeps only while someone keeps that process running - we need to have the
/// runner automatically check."</i> The only pull was
/// <c>GET /v1/watches/{name}/actions</c>, scoped to one watch, so something had
/// to supply the name, and what did was a person at a shell running
/// <c>gg runner sweep &lt;watch&gt;</c> in the foreground. A watch whose
/// document says <c>pull-point: resident-runner</c> was pulled by no resident
/// runner at all.
/// </para>
/// <para>
/// <b>It names no watch, because flights do not either.</b> A runner claims a
/// flight without knowing which one exists; the control plane matches. This is
/// that shape for a sweep, and the alternative - learning the watch names from
/// a developer-audience read and polling each - is a runner reading the
/// airspace to find its own work.
/// </para>
/// <para>
/// <b>It says what it can reach, so it is only handed what it can do.</b> Rule
/// 18: a sweep presents a credential only where the runner's operator already
/// declared the pair. A runner that claimed a sweep it could not serve would
/// attest a FALSE <c>unreachable</c> - an outage that is really a routing
/// mistake. So the claim carries the (host, credential locator) pairs from the
/// runner's own declaration, and the control plane serves only a watch that
/// names one of them.
/// </para>
/// <para>
/// <b>On the claim, not the heartbeat.</b> Labels ride the heartbeat because
/// the control plane routes flights before anybody asks. A sweep is pulled, so
/// the pairs travel with the pull that uses them - and the control plane never
/// stores them and they cannot go stale between beats. A locator is a
/// reference rather than a secret, and the watch document already carries the
/// same one; nothing crosses that was not already here.
/// </para>
/// </remarks>
public class ARunnerClaimsASweepWithoutNamingOneTests
{
    private const string Method = "POST";
    private const string Route = "/v1/runner/sweeps/claim";

    private static SweepClaim AClaim(params (string Host, string Credential)[] pairs) => new()
    {
        Serves = [.. pairs.Select(p => new SweepServes { Host = p.Host, Credential = p.Credential })],
    };

    [Test]
    public async Task The_claim_is_a_runner_route_that_names_no_watch()
    {
        var declared = ProtocolSurface.Endpoints.SingleOrDefault(
            e => e.Method == Method && e.Path == Route);

        await Assert.That(declared).IsNotNull()
            .Because("a resident runner has to be able to ask for work without a person "
                   + "telling it which watch to ask about.");
        await Assert.That(declared!.Path.Contains('{')).IsFalse()
            .Because("a path parameter would put a watch name back in the request, which is "
                   + "the exact thing that needed a person.");
        await Assert.That(declared.Audience).IsEqualTo(Audience.Runner);
        await Assert.That(declared.RequiredHeaders).Contains(ProtocolSurface.RunnerHeader);
    }

    [Test]
    public async Task It_answers_with_the_sweeps_a_named_pull_already_answers_with()
    {
        // ONE SHAPE FOR A SERVED SWEEP. The runner already handles a
        // WatchActionList from the per-watch pull, so the claim returning one
        // means nothing downstream of it changes.
        var declared = ProtocolSurface.Endpoints.Single(e => e.Method == Method && e.Path == Route);

        await Assert.That(declared.Request).IsEqualTo(typeof(SweepClaim));
        await Assert.That(declared.Response).IsEqualTo(typeof(WatchActionList));
    }

    [Test]
    public async Task A_claim_says_which_trackers_this_runner_can_reach()
    {
        var claim = AClaim(("https://tracker.example/acme", "local:acme/triage"));

        await Assert.That(SweepClaim.Validate(claim)).IsNull();
        await Assert.That(claim.Serves.Single().Credential).IsEqualTo("local:acme/triage")
            .Because("the pair is what rule 18 compares - a host alone would let a runner holding "
                   + "a different credential for the same tracker claim a sweep it cannot serve.");
    }

    [Test]
    public async Task A_pair_with_no_host_or_no_credential_is_refused()
    {
        await Assert.That(SweepClaim.Validate(AClaim((" ", "local:acme/triage")))).IsNotNull();
        await Assert.That(SweepClaim.Validate(AClaim(("https://tracker.example/acme", ""))))
            .IsNotNull()
            .Because("half a pair matches nothing rule 18 would accept, and a claim that could "
                   + "match nothing is a request that looks like it asked for something.");
    }

    [Test]
    public async Task A_runner_that_can_reach_nothing_is_refused_rather_than_answered_empty()
    {
        // NOT AN EMPTY LIST. A runner with no pairs cannot serve any sweep, so
        // asking is a mistake in the runner rather than a quiet period - and
        // answering "nothing decided" would make those two indistinguishable.
        // The runner should not ask at all; the contract says so.
        await Assert.That(SweepClaim.Validate(AClaim())).IsNotNull();
    }

    [Test]
    public async Task The_claim_is_on_the_wire_in_its_own_words()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(SweepClaim)]).Contains("serves");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(SweepServes)])
            .IsEquivalentTo((string[])["host", "credential"]);
    }
}
