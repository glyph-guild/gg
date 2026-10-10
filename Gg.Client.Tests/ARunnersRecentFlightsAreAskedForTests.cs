using System.Net;
using System.Text;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The client asks for a runner's recent flights at the route the contract declares
/// (slice seventy-one, ADR-0039 Decision 10): Remote Control's runner screen reads it.
/// </summary>
public class ARunnersRecentFlightsAreAskedForTests
{
    private sealed class Answering : HttpMessageHandler
    {
        internal HttpRequestMessage? Asked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"flights":[{"flightId":"f1","number":1063,"kind":"implement","state":"landed","claimedAt":"2026-10-10T12:00:00Z","endedAt":"2026-10-10T12:30:00Z"}]}""",
                    Encoding.UTF8, "application/json"),
            });
        }
    }

    [Test]
    public async Task It_reads_the_runners_flights_newest_first_as_the_control_plane_sends_them()
    {
        var handler = new Answering();
        var client = new ControlPlaneClient(new HttpClient(handler) { BaseAddress = new Uri("https://cp.invalid/") });

        var flights = await client.RunnerFlightsAsync("session", "runner-2");

        await Assert.That(handler.Asked!.Method).IsEqualTo(HttpMethod.Get);
        await Assert.That(handler.Asked.RequestUri!.AbsolutePath).IsEqualTo("/v1/runners/runner-2/flights");
        var flight = flights.Flights.Single();
        await Assert.That(flight.Number).IsEqualTo(1063);
        await Assert.That(flight.State).IsEqualTo("landed");
    }
}
