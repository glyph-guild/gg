using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The two reads a person watches a plan through, declared in the version the
/// control plane serves them in.
/// </summary>
/// <remarks>
/// <para>
/// <b>DECLARED AND SERVED IN ONE ROUND, which is narrower than it sounds.</b>
/// Slice fifty-three says both routes are declared before either is served, and
/// read as <i>a step early</i> that is wrong in a way this repository has
/// already paid for twice. A route no published contract declares is
/// unreachable to consumers for ever, because the endpoint surface is not in
/// the contract digest and the publish skips a shipped version - that is the
/// first failure, and it is why the declaration cannot come late. A route
/// declared here that nobody serves refuses the control plane's whole
/// conformance suite the moment its pin moves past this version, for any reason
/// and by anybody - that is the second, and it is why the declaration cannot
/// come early. The only order that avoids both is this one: declare in the
/// version the serving change pins, and merge the two back to back.
/// </para>
/// <para>
/// <b>Developer audience, and a runner opens neither.</b> An itinerary is every
/// piece of work a tenant has planned; a runner credential that could enumerate
/// it would read a tenant's intentions from a credential meant only to hold one
/// lease at a time. The board's own argument, one noun over.
/// </para>
/// <para>
/// <b>The board's page type rather than a second one.</b> An itinerary's legs
/// ARE nominations - there is nothing else they could be - so a shape of their
/// own would be a second thing to keep agreeing with the first.
/// </para>
/// </remarks>
public class TwoItineraryReadsAreDeclaredTests
{
    private static Endpoint Declared(string path) =>
        ProtocolSurface.Endpoints.Single(
            e => string.Equals(e.Path, path, StringComparison.Ordinal)
              && string.Equals(e.Method, "GET", StringComparison.Ordinal));

    [Test]
    public async Task Both_reads_are_declared()
    {
        foreach (var path in (string[])["/v1/itineraries", "/v1/itineraries/{ref}"])
        {
            var route = Declared(path);

            await Assert.That(route.Audience).IsEqualTo(Audience.Developer)
                .Because("a runner able to read this would enumerate a tenant's planned work "
                       + "from a credential meant only to hold one lease at a time.");

            await Assert.That(route.Request).IsNull();
            await Assert.That(route.Response).IsEqualTo(typeof(BoardPage))
                .Because("an itinerary's legs are nominations, so the board's page is what "
                       + "answers - a second shape would be a second thing to keep agreeing.");

            await Assert.That(route.Statuses).Contains(ProtocolSurface.ProtocolTooOld);
        }
    }

    [Test]
    public async Task One_itinerary_can_be_missing_and_the_list_cannot()
    {
        // A 404 rather than a 400 for a reference in neither form, for
        // /v1/flights/{ref}'s reason: "ITN-nope" names no itinerary in exactly
        // the way a well-formed id for somebody else's does. The LIST has no
        // 404 - a tenant with no plans has an empty page, and answering 404
        // would make "none" and "no such surface" the same reply.
        await Assert.That(Declared("/v1/itineraries/{ref}").Statuses).Contains(404);
        await Assert.That(Declared("/v1/itineraries").Statuses).DoesNotContain(404);
    }

    [Test]
    public async Task The_prefix_is_governed()
    {
        // Its own prefix rather than a path under /v1/board, because an
        // itinerary's legs are deliberately NOT on the board - nothing will
        // ever answer one - and a read hanging off the board's prefix would
        // read as the thing it is defined not to be. Governed for the board's
        // reason: these are every nomination a tenant's agents have made.
        await Assert.That(ProtocolSurface.GovernedPrefixes).Contains("/v1/itineraries");
    }

    [Test]
    public async Task Nothing_under_the_prefix_is_undeclared()
    {
        // What governing BUYS, asserted rather than assumed: within the prefix
        // the contract is closed, so an itinerary read nobody declared cannot
        // be served. The control plane holds the other direction.
        var underPrefix = ProtocolSurface.Endpoints
            .Where(e => e.Path.StartsWith("/v1/itineraries", StringComparison.Ordinal))
            .Select(e => $"{e.Method} {e.Path}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        await Assert.That(underPrefix).IsEquivalentTo((string[])
            ["GET /v1/itineraries", "GET /v1/itineraries/{ref}"]);
    }
}
