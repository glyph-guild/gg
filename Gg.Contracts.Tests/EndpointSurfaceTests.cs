using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The declared endpoint surface is a contract, and the fingerprint cannot see it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found by predicting the guard would fire and watching it not.</b> ADR-0012
/// changed <c>POST /v1/flights/{ref}/decisions</c> from answering <c>200</c> with a
/// <c>DecisionRecorded</c> to answering <c>202</c> with nothing. That is a protocol
/// change by any reading - an integrator who wrote against the old declaration gets
/// no body - and <c>ContractSurfaceTests</c> stayed green.
/// </para>
/// <para>
/// <b>The surface hashes pinned types, their members, and closed vocabularies.</b>
/// It does not hash <see cref="ProtocolSurface.Endpoints"/> at all: not the path,
/// not the method, not the audience, not the statuses, not which type a route
/// answers with. So a route can be added, removed, re-audienced or re-shaped and
/// the version that is supposed to describe the contract does not move.
/// </para>
/// <para>
/// <b>This is the closed-vocabulary defect one layer up.</b> That one was written
/// because both fingerprints hashed types and property names, so a third
/// <c>DiffBasis</c> value moved neither - <i>the guard that exists to force this
/// conversation could not see the change that most needs one</i>. The same sentence
/// is true here, about the routes.
/// </para>
/// <para>
/// <b>What this file does and does not do.</b> It records the gap with a
/// fingerprint of its own and pins today's value, so an endpoint change is at least
/// LOUD from now on. Folding this into the contract surface proper is a change to
/// the fingerprint mechanism - it would move every recorded hash - and belongs in a
/// step that is about that, not inside a migration.
/// </para>
/// </remarks>
public class EndpointSurfaceTests
{
    /// <summary>Every declared route, rendered in a stable order.</summary>
    private static string SurfaceText()
    {
        var lines = ProtocolSurface.Endpoints
            .Select(e =>
                $"{e.Method} {e.Path} audience={e.Audience} "
              + $"request={e.Request?.Name ?? "none"} response={e.Response?.Name ?? "none"} "
              + $"statuses={string.Join(",", e.Statuses.Order())} "
              + $"headers={string.Join(",", e.RequiredHeaders.Order(StringComparer.Ordinal))}")
            .Order(StringComparer.Ordinal);

        return string.Join('\n', lines);
    }

    private static string Fingerprint() =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SurfaceText())))
            .ToLowerInvariant();

    [Test]
    public async Task The_endpoint_surface_is_not_part_of_the_contract_fingerprint()
    {
        // ASSERTED ABSENT, because the absence is the finding. A reader who assumes
        // the contract version covers the routes is wrong, and until this is fixed
        // the honest thing is to say so where somebody will see it.
        //
        // The mechanism: ContractSurfaceTests.SurfaceText walks Vocabulary.Types
        // and ClosedVocabularies. Neither reaches ProtocolSurface.Endpoints.
        var surfaceSource = typeof(ContractSurfaceTests)
            .GetMethod("SurfaceText", BindingFlags.NonPublic | BindingFlags.Static);

        await Assert.That(surfaceSource).IsNotNull()
            .Because("if this method is renamed, the claim below is about nothing.");

        await Assert.That(ProtocolSurface.Endpoints.Count).IsGreaterThan(20)
            .Because("there are routes to have missed, so 'the fingerprint does not cover them' "
                   + "is a real gap rather than a vacuous one.");
    }

    [Test]
    public async Task An_endpoint_change_moves_this_fingerprint_even_though_it_moves_no_ledger()
    {
        // THE PIN. Changing a route's method, path, audience, statuses or response
        // type fails HERE - which is not the ledger, and is better than nothing.
        // Update this value in the same commit as the change, and say what moved.
        //
        // Moved for slice five: POST /v1/invitations arrives, and
        // /v1/invitations joins GovernedPrefixes so the declaration closes over
        // it. An invitation is the strongest capability the product issues -
        // whoever holds the link becomes a principal in a tenant - so an
        // undeclared route under that prefix would be an unaudited way to make
        // one.
        //
        // Moved for slice six: POST /v1/leases:claim stops answering a lease and
        // answers 202 with a request id, and GET /v1/leases/claims/{id} arrives
        // to say what became of it. 200 and 204 both go, which is the change:
        // a lease could not be decided inline once identity moved behind an
        // announcement, and 204 made an idle fleet and a fleet blocked on a
        // missing credential the same answer.
        //
        // Moved for ADR-0012 B2: the decisions route answers 202 with no body
        // rather than 200 with a DecisionRecorded, because the write became a
        // command and answering inline would mean waiting for its own event.
        // Moved for slice seven step 1: GET /v1/flights/{ref}/seed arrives, and it
        // is the route that stops handoff being machine-local. The seed was
        // composed on the machine that ran the flight, from a digest on that
        // machine's disk, and placed on that machine's clipboard - so "any flight
        // resumable by anyone" held only for whoever was at that keyboard.
        //
        // Developer audience, and the consequence is deliberate rather than an
        // oversight: a runner cannot fetch a seed, because one that could would
        // read what every flight in the tenant tried and ruled out from a
        // credential meant only to let it hold one lease. A resuming loop is
        // handed its seed on the lease instead.
        // Moved again for slice seven step 2, and this one REMOVES a route.
        // POST /v1/flights/{id}/takeover is gone; three replace it -
        // takeover:claim, takeover:renew and takeover:return. What went recorded a
        // takeover after the fact, carrying how long somebody had held the flight,
        // so two people on two machines could both take one stopped flight and both
        // find out afterwards. A record is not a hold.
        //
        // Deleted rather than kept beside the claim: a route that records a takeover
        // without holding anything, sitting next to the claim that replaces it, is a
        // shape somebody will build against. It was reachable only through a client
        // method nothing in the product called, because nothing in the product ever
        // took a flight over.
        //
        // Moved for slice eight: four reads arrive and a prefix closes. POST and
        // GET /v1/environments are the chart - what an envelope may select, and
        // the registry the "uncharted" apply refusal points people at - and
        // /v1/environments joins GovernedPrefixes because the chart decides what
        // every envelope may say, so an undeclared route under it would be an
        // unaudited way to widen every envelope (the /v1/credentials argument).
        // GET /v1/envelope/plan renders the tenant-level checklist against the
        // live fleet; GET /v1/flights/{ref}/checklist renders the one this flight
        // compiled at creation. All Developer: a runner is matched on labels, it
        // never reads the chart or prices an envelope.
        // Moved for slice nine: the topology's two routes and a prefix close.
        // POST /v1/airspace/names is the door the "no topology entry" apply
        // refusal points people at - a name is unreachable until declared, so
        // the door ships in the same contract as the refusal. GET
        // /v1/airspace/topology is what gg airspace show renders, root always
        // included because root is synthesized by the read. /v1/airspace
        // joins GovernedPrefixes because the topology decides what a tenant's
        // envelopes can REACH, the /v1/environments argument one level up.
        // Both Developer: a runner is leased onto work, it never reshapes the
        // estate.
        // Moved for slice nine's registry: POST and GET
        // /v1/airspace/repositories, under the already-governed prefix.
        // Registration is what makes a repository nameable at all and what
        // takes the host out of the flight's request path - the control plane
        // stops deriving a provider from whatever URI opened a flight, and
        // resolves identity through the registered entry instead. Both
        // Developer: a runner receives a provider KEY on its lease and maps
        // it to a host of its own; it never reads the registry.
        // Moved for slice ten: the three registration doors gain 202 - the
        // gated path. A registration widens what the tenant can reach, so it
        // may ride a flight, and the pending answer names the flight, the
        // approver and what widens. Nothing moved paths or audiences; only
        // the statuses grew.
        await Assert.That(Fingerprint())
            .IsEqualTo("1f8086cc82f814d2847d17e88ba5aac00a4ab64767a480c99496fa706aeda783")
            .Because("an endpoint moved. If that was deliberate, record what and why here - "
                   + "and note that the contract VERSION does not move for this, which is the "
                   + "gap the test above names.");
    }

    [Test]
    public async Task The_decisions_route_declares_what_ADR_0012_made_it()
    {
        var decisions = ProtocolSurface.Endpoints
            .Single(e => e.Path == "/v1/flights/{ref}/decisions" && e.Method == "POST");

        await Assert.That(decisions.Response).IsNull()
            .Because("the write is a command; there is nothing to answer with.");
        await Assert.That(decisions.Statuses).Contains(202);
        await Assert.That(decisions.Statuses).DoesNotContain(200)
            .Because("a declared surface that still promised a body would be a contract nobody "
                   + "serves - and the client would keep a branch nothing reaches.");
    }
}
