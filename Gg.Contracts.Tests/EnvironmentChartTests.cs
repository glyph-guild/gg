using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The environment chart: the registry of names an envelope may select.
/// </summary>
/// <remarks>
/// <para>
/// <b>"Uncharted" is a refusal with a fix in it.</b> An envelope naming an
/// environment nobody charted is refused at apply, and the refusal names the
/// registry - the same word and the same shape as the repository-keyed
/// credential registry. These types are how the chart crosses the wire.
/// </para>
/// <para>
/// <b>The meaning is optional, and its absence IS the disposition.</b> An
/// entry with no meaning admits the label as <c>stated</c> - an advertised
/// claim - rather than refusing it. Registering a meaning is what earns
/// <c>measured</c>, and nothing in this slice enforces one.
/// </para>
/// </remarks>
public class EnvironmentChartTests
{
    [Test]
    public async Task The_chart_types_are_in_the_vocabulary()
    {
        await Assert.That(Vocabulary.Types).Contains(typeof(ChartEnvironmentRequest));
        await Assert.That(Vocabulary.Types).Contains(typeof(EnvironmentCharted));
        await Assert.That(Vocabulary.Types).Contains(typeof(EnvironmentChart));
    }

    [Test]
    public async Task The_chart_types_declare_their_members()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(ChartEnvironmentRequest)])
            .IsEquivalentTo((string[])["name", "meaning"]);
        await Assert.That(ProtocolSurface.JsonMembers[typeof(EnvironmentCharted)])
            .IsEquivalentTo((string[])["name", "meaning", "disposition", "chartedBy", "chartedAt"]);
        await Assert.That(ProtocolSurface.JsonMembers[typeof(EnvironmentChart)])
            .IsEquivalentTo((string[])["environments"]);
    }

    [Test]
    public async Task Charting_is_attributed_on_the_wire()
    {
        // Registration in v0 is unrestricted AND logged - the second half is
        // load-bearing. A chart entry that could not say who made it would be
        // an unaudited way to widen what every envelope may select.
        var charted = new EnvironmentCharted
        {
            Name = "aspire-payments",
            Meaning = null,
            Disposition = LabelDispositions.Stated,
            ChartedBy = "Priya N",
            ChartedAt = DateTimeOffset.UnixEpoch,
        };

        await Assert.That(charted.ChartedBy).IsEqualTo("Priya N");
        await Assert.That(charted.Disposition).IsEqualTo("stated")
            .Because("no meaning is registered, and stated is what that absence means.");
    }

    [Test]
    public async Task The_environments_prefix_is_governed()
    {
        await Assert.That(ProtocolSurface.GovernedPrefixes).Contains("/v1/environments")
            .Because("the chart decides what an envelope may say and what a fleet must satisfy; "
                   + "an undeclared route under it would be an unaudited way to widen every "
                   + "envelope - the same argument /v1/credentials came in on.");
    }

    [Test]
    public async Task The_chart_has_its_endpoints()
    {
        var chart = ProtocolSurface.Endpoints.Single(
            e => e.Method == "POST" && e.Path == "/v1/environments");
        var list = ProtocolSurface.Endpoints.Single(
            e => e.Method == "GET" && e.Path == "/v1/environments");

        await Assert.That(chart.Request).IsEqualTo(typeof(ChartEnvironmentRequest));
        await Assert.That(chart.Response).IsEqualTo(typeof(EnvironmentCharted));
        await Assert.That(chart.Audience).IsEqualTo(Audience.Developer);
        await Assert.That(chart.Statuses).Contains(400)
            .Because("a malformed name is refused with a diagnosis, not stored.");

        await Assert.That(list.Response).IsEqualTo(typeof(EnvironmentChart));
        await Assert.That(list.Audience).IsEqualTo(Audience.Developer);
    }
}
