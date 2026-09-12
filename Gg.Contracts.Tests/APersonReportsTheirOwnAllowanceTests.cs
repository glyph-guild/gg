using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// Reporting a subscription you own, without lending a machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>The reading route is runner-only and nothing ever argued for that.</b>
/// The declaration beside it argues three adjacent choices at length — why a
/// reading is not a fact, why it does not ride the heartbeat, why 202 — and
/// the audience is justified nowhere. It was inherited from the opening words
/// of its own comment, "WHAT THIS MACHINE HAS SPENT", which presuppose a
/// machine rather than argue for one.
/// </para>
/// <para>
/// <b>And it defends nothing.</b> Registering a runner is a DEVELOPER act —
/// a runner that could mint runners is the privilege-escalation ladder the
/// authority tests name — so anybody with a session can already mint a
/// credential and post whatever reading they like. The refusal costs a step
/// and stops nothing, and the attribution is identical either way: the owner
/// of a runner's reading is the developer who registered it.
/// </para>
/// <para>
/// <b>What the restriction cost is real.</b> An allowance is a person's
/// subscription and the meter measures the PLAN. A laptop that reads its own
/// meter knows something true about that plan whether or not it ever takes a
/// flight — and until now the only way to say so was to run a fleet member.
/// </para>
/// <para>
/// <b>Its own path, not a second audience on the existing one.</b>
/// <see cref="Audience"/> is a closed vocabulary, so a combined value would
/// halt every reader that has one; and the two really are different acts. A
/// machine reports itself. A person reports the subscription they speak for.
/// </para>
/// </remarks>
public class APersonReportsTheirOwnAllowanceTests
{
    private const string Mine = "/v1/allowances/readings/mine";

    private static Endpoint Declared(string method, string path) =>
        ProtocolSurface.Endpoints.Single(
            e => string.Equals(e.Method, method, StringComparison.Ordinal)
                 && string.Equals(e.Path, path, StringComparison.Ordinal));

    [Test]
    public async Task A_person_may_report_the_subscription_they_speak_for()
    {
        var mine = Declared("POST", Mine);

        await Assert.That(mine.Audience).IsEqualTo(Audience.Developer)
            .Because("a person is more obviously entitled to speak for a subscription "
                   + "than a machine is, and the machine-only route was never argued for.");

        await Assert.That(mine.RequiredHeaders).Contains(ProtocolSurface.SessionHeader);
    }

    [Test]
    public async Task It_takes_the_same_document_a_machine_posts()
    {
        var mine = Declared("POST", Mine);
        var machine = Declared("POST", "/v1/allowances/readings");

        await Assert.That(mine.Request).IsEqualTo(typeof(AllowanceReading))
            .Because("the measurement is the same measurement. Only the attestation "
                   + "differs, and that rides the credential rather than the body.");

        await Assert.That(mine.Request).IsEqualTo(machine.Request);
        await Assert.That(mine.Statuses).IsEquivalentTo(machine.Statuses)
            .Because("same document, same refusals - a second shape here would be two "
                   + "ways to be wrong about one thing.");
    }

    [Test]
    public async Task The_machines_route_is_untouched()
    {
        var machine = Declared("POST", "/v1/allowances/readings");

        await Assert.That(machine.Audience).IsEqualTo(Audience.Runner)
            .Because("widening who may report does not widen what a MACHINE may do. A "
                   + "runner still acts with less than the person behind it, which is the "
                   + "rule the runner audience exists for.");
    }

    [Test]
    public async Task It_serves_under_a_governed_prefix_so_nothing_undeclared_joins_it()
    {
        await Assert.That(ProtocolSurface.GovernedPrefixes.Any(
                p => Mine.StartsWith(p, StringComparison.Ordinal)))
            .IsTrue()
            .Because("a reading is the input to decisions about who gets work, so a route "
                   + "under here that nobody declared could move that without an audit.");
    }
}
