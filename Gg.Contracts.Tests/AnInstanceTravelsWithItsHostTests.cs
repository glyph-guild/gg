using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// What a host is hosting travels on the host's own row.
/// </summary>
/// <remarks>
/// <para>
/// <b>On <c>RunnerSummary</c> rather than in a listing of its own</b>, because
/// an environment instance belongs to exactly one machine: it is a UNIX user
/// there (ADR-0034), the socket is a path there, and only a runner that IS that
/// machine may be granted it (good-grief#627). A separate top-level list would
/// carry the machine as a foreign key and leave every reader to join it back.
/// </para>
/// <para>
/// <b>Optional, and it never becomes required.</b> Every tenant that hosts
/// nothing has none, which is all of them today, and a gg newer than the control
/// plane it is pointed at receives a payload without it. Absent has to stay
/// distinguishable from empty: "this control plane does not say" is not "this
/// host has no instances", and a renderer that drew the second from the first
/// would invent a fact about a machine.
/// </para>
/// </remarks>
public class AnInstanceTravelsWithItsHostTests
{
    [Test]
    public async Task A_runner_summary_carries_what_it_is_hosting()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RunnerSummary)])
            .Contains("instances")
            .Because("gg renders this listing and has no other way to learn which instance is "
                   + "on which machine - the socket is derived from the name, and the name is "
                   + "only meaningful beside its host.");
    }

    [Test]
    public async Task An_instance_says_where_it_is_and_what_it_holds()
    {
        var members = ProtocolSurface.JsonMembers[typeof(HostedInstance)];

        await Assert.That(members).Contains("environment")
            .Because("a grant is made against the environment, so a tenant with two of them "
                   + "cannot read a list of bare slot names.");
        await Assert.That(members).Contains("instance");
        await Assert.That(members).Contains("flightNumber")
            .Because("the number is what a person reads and what every other row of this "
                   + "listing already shows for a runner holding a flight.");
    }

    [Test]
    public async Task A_free_instance_carries_no_flight_rather_than_an_empty_one()
    {
        // NULL IS THE ANSWER FOR A FREE SLOT, on CurrentFlightNumber's terms one
        // type over. An empty string would render as a flight whose number
        // nobody knows, which is the one thing this listing must never say.
        var free = new HostedInstance { Environment = "ui", Instance = "gg-env-2" };

        await Assert.That(free.FlightNumber).IsNull();
    }
}
