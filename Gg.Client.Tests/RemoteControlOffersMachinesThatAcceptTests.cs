using System.Net;
using System.Text;

namespace Gg.Client.Tests;

/// <summary>
/// Remote Control offers only machines whose last heartbeat said they accept ad hoc agent
/// sessions (slice seventy-one, ADR-0039 Decision 10).
/// </summary>
/// <remarks>
/// <b>Found on the first walk.</b> Every beating runner was listed, pool members included,
/// and a machine that never opted in refused only once it had been reached - a list of
/// choices most of which say no.
/// </remarks>
public class RemoteControlOffersMachinesThatAcceptTests
{
    private sealed class Fleet(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }

    [Test]
    public async Task Only_a_beating_machine_that_accepts_sessions_is_offered()
    {
        var fleet = """
            {"runners":[
              {"runnerId":"r2","label":"vmlinux002","state":"idle","acceptsAgentSessions":true},
              {"runnerId":"r3","label":"vmlinux003","state":"idle"},
              {"runnerId":"p1","label":"gg-pool-dev-1","state":"busy","acceptsAgentSessions":false},
              {"runnerId":"r9","label":"vmlinux009","state":"offline","acceptsAgentSessions":true}
            ]}
            """;
        var control = new ControlPlaneClient(new HttpClient(new Fleet(fleet)) { BaseAddress = new Uri("https://cp.invalid/") });
        var reach = new ReachAnAgent(control, new ConsoleChannel([], TimeSpan.FromSeconds(1)));

        var machines = await reach.MachinesAsync("session");

        await Assert.That(machines.Select(m => m.Label)).IsEquivalentTo(["vmlinux002"])
            .Because("vmlinux003 never said it takes sessions, the pool member said no, and an offline machine answers nothing.");
    }
}
